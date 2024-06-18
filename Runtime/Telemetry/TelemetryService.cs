// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.ARDK.AR.Protobuf;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Protobuf;
using Niantic.Lightship.AR.Settings;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Niantic.Lightship.AR.Utilities;
using Niantic.Protobuf;

namespace Niantic.Lightship.AR.Telemetry
{
    internal class TelemetryService : IDisposable
    {
        private readonly ITelemetryPublisher _telemetryPublisher;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly MessageParser<ArdkNextTelemetryOmniProto> _protoMessageParser;
        private readonly string _lightshipApiKey;
        private readonly IntPtr _telemetryServiceHandle = IntPtr.Zero;
        private static readonly ConcurrentQueue<ArdkNextTelemetryOmniProto> s_messagesToBeSent;

        private readonly TimeSpan _publishLoopDelay = TimeSpan.FromSeconds(1);
        private readonly TimeSpan _nativePollingRate = TimeSpan.FromMilliseconds(500);

        static TelemetryService()
        {
            s_messagesToBeSent = new ConcurrentQueue<ArdkNextTelemetryOmniProto>();
        }

        public TelemetryService(IntPtr lightshipUnityContext, ITelemetryPublisher telemetryPublisher, string lightshipApiKey)
        {
            _protoMessageParser = new MessageParser<ArdkNextTelemetryOmniProto>(() => new ArdkNextTelemetryOmniProto());
            _telemetryPublisher = telemetryPublisher;
            _lightshipApiKey = lightshipApiKey;

            // Do not initialise native layer for Windows
            if (lightshipUnityContext.IsValidHandle())
            {
                _telemetryServiceHandle =
                    Lightship_ARDK_Unity_Telemetry_GetTelemetryServiceHandle(lightshipUnityContext);
            }

            MonoBehaviourEventDispatcher.OnApplicationFocusLost.AddListener(FlushTelemetry);
            _cancellationTokenSource = new CancellationTokenSource();
            // TODO: Once the dependency trees are better, use the Map SDKs async lib
            Task.Run(async () => await TimedPublish(_publishLoopDelay, _cancellationTokenSource.Token));
            Task.Run(async () => await TimedNativePoller(_nativePollingRate, _cancellationTokenSource.Token));
        }

        private async Task TimedNativePoller(TimeSpan nativePollingRate, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    EnqueueNativeEvents();

                    // will throw exception on cancellation. But otherwise, it will delay process termination by native polling rate.
                    await Task.Delay(nativePollingRate, cancellationToken);
                }
                catch (Exception e)
                {
                    if (e is TaskCanceledException or OperationCanceledException)
                    {
                        // expected exception. No need to log.
                        return;
                    }

                    Log.Debug($"Encountered exception: {e} while running the timed telemetry native poller");
                }
            }
        }

        public static void PublishEvent(ArdkNextTelemetryOmniProto ardkNextTelemetryOmniProto)
        {
            PublishEvent(ardkNextTelemetryOmniProto, requestId: string.Empty);
        }

        public static void PublishEvent(ArdkNextTelemetryOmniProto ardkNextTelemetryOmniProto, string requestId)
        {
            if (Metadata.AgeLevel == ARClientEnvelope.Types.AgeLevel.Minor)
            {
                // dont log the event
                return;
            }

            if(ardkNextTelemetryOmniProto.TimestampMs == default)
            {
                ardkNextTelemetryOmniProto.TimestampMs = GetCurrentUtcTimestamp();
            }

            ardkNextTelemetryOmniProto.ArCommonMetadata = Metadata.GetArCommonMetadata(requestId);
            s_messagesToBeSent.Enqueue(ardkNextTelemetryOmniProto);
        }

        private void EnqueueNativeEvents()
        {
            if (!_telemetryServiceHandle.IsValidHandle())
            {
                // not possible. But in the off chance this does happen, its better to fail gracefully and silently
                return;
            }

            IntPtr eventLengthsArrayPtr = IntPtr.Zero;
            IntPtr eventsAsByteArrayArrayPtr = IntPtr.Zero;
            int eventCount = 0;
            try
            {
                eventsAsByteArrayArrayPtr = Lightship_ARDK_Unity_Telemetry_GetPendingEventsAsArray(
                    _telemetryServiceHandle,
                    out eventLengthsArrayPtr, out eventCount);

                if (!eventLengthsArrayPtr.IsValidHandle() || !eventsAsByteArrayArrayPtr.IsValidHandle())
                {
                    // not possible. But lets fail silently.
                    return;
                }

                // no need to go through all this if you dont have events. Just clear out the resources and move on.
                if (eventCount > 0)
                {
                    unsafe
                    {
                        NativeArray<int> eventLengths =
                            NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(
                                eventLengthsArrayPtr.ToPointer(),
                                eventCount, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        AtomicSafetyHandle atomicSafetyHandle = AtomicSafetyHandle.Create();
                        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref eventLengths, atomicSafetyHandle);
#endif

                        for (int i = 0; i < eventCount; i++)
                        {
                            int eventLength = eventLengths[i];
                            IntPtr byteArrayPtr = Marshal.ReadIntPtr(eventsAsByteArrayArrayPtr, i * IntPtr.Size);
                            NativeArray<byte> bytesArray =
                                NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(
                                    byteArrayPtr.ToPointer(),
                                    eventLength, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref bytesArray, atomicSafetyHandle);
#endif

                            if (TryParseEvent(bytesArray, out var omniProto))
                            {
                                // Add metadata fields here to avoid more things being transmitted between C++ and C#
                                string userId = omniProto.ArCommonMetadata.UserId;

                                omniProto.ArCommonMetadata =
                                    Metadata.GetArCommonMetadata(omniProto.ArCommonMetadata.RequestId);
                                omniProto.DeveloperKey = _lightshipApiKey;

                                // userId can be changed by the time the event has made it from the C++ layer to the C# layer
                                omniProto.ArCommonMetadata.UserId = userId;

                                s_messagesToBeSent.Enqueue(omniProto);
                            }

                            bytesArray.Dispose();
                        }

                        eventLengths.Dispose();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        AtomicSafetyHandle.Release(atomicSafetyHandle);
#endif
                    }
                }

            }
            catch (Exception e)
            {
                Log.Debug($"Encountered exception: {e} while running the GetArray call");
            }
            finally
            {
                // nested try catch because code in a finally block can still throw exceptions.
                try
                {
                    if (eventLengthsArrayPtr.IsValidHandle() && eventsAsByteArrayArrayPtr.IsValidHandle())
                    {
                        Lightship_ARDK_Unity_Telemetry_ReleasePendingEventsArray(eventsAsByteArrayArrayPtr,
                            eventLengthsArrayPtr, eventCount);
                    }
                }
                catch {}
            }
        }

        private bool TryParseEvent(NativeArray<byte> serialisedPayload, out ArdkNextTelemetryOmniProto ardkNextTelemetryOmniProto)
        {
            try
            {
                ardkNextTelemetryOmniProto = _protoMessageParser.ParseFrom(serialisedPayload.ToArray());
                return true;
            }
            catch (Exception e)
            {
                Log.Debug($"Error while parsing event: {e}");
            }

            ardkNextTelemetryOmniProto = null;
            return false;
        }

        private async Task TimedPublish(TimeSpan ts, CancellationToken cancellationToken)
        {
            // in case the publish task dies for some reason, try again.
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    PublishQueuedEvents();

                    // will throw exception on cancellation. But otherwise, it will delay process termination by polling time delay.
                    await Task.Delay(ts, cancellationToken);
                }
                catch (Exception e)
                {
                    if (e is TaskCanceledException or OperationCanceledException)
                    {
                        // expected exception. No need to log.
                        return;
                    }

                    Log.Debug($"Encountered exception: {e} while running the timed telemetry loop");
                }
            }
        }

        private void PublishQueuedEvents()
        {
            while (!s_messagesToBeSent.IsEmpty)
            {
                if (s_messagesToBeSent.TryDequeue(out var eventToPublish))
                {
                    eventToPublish.DeveloperKey = _lightshipApiKey;
                    _telemetryPublisher.RecordEvent(eventToPublish);
                }
            }
        }

        private static long GetCurrentUtcTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void Dispose()
        {
            MonoBehaviourEventDispatcher.OnApplicationFocusLost.RemoveListener(FlushTelemetry);

            // stop async tasks
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            // Flush any remaining events
            // Note: This does not guarantee that all events will be sent
            FlushTelemetry();

            // stop the native telemetry service
            if (_telemetryServiceHandle != default)
            {
                Lightship_ARDK_Unity_Telemetry_ReleaseTelemetryServiceHandle(_telemetryServiceHandle);
            }

            if (!s_messagesToBeSent.IsEmpty)
            {
                // Log.Warning($"Events to be dropped: {s_messagesToBeSent.Count}");
            }
        }

        private void FlushTelemetry()
        {
            EnqueueNativeEvents();
            PublishQueuedEvents();
        }

        // returns the service handle for the telemetry service
        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Telemetry_GetTelemetryServiceHandle(IntPtr unityContextHandle);

        // releases the telemetry service handle while disposing the service
        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_Telemetry_ReleaseTelemetryServiceHandle(IntPtr unityContextHandle);

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Telemetry_GetPendingEventsAsArray(IntPtr telemetryServiceHandle, out IntPtr array, out int arrayLength);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_Telemetry_ReleasePendingEventsArray(IntPtr eventsAsByteArrayArrayPtr, IntPtr lengthsArray, int eventCount);
    }
}
