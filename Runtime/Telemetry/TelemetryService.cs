using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AOT;
using Google.Protobuf;
using Niantic.ARDK.AR.Protobuf;
using Niantic.Lightship.AR.Protobuf;
using Niantic.Lightship.AR.Settings.User;
using UnityEngine;

namespace Telemetry
{
    internal class TelemetryService : IDisposable
    {
        private readonly ITelemetryPublisher _telemetryPublisher;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _apiKey;

        private static readonly
            ConcurrentQueue<KeyValuePair<ArdkNextTelemetryOmniProto, ARClientEnvelope.Types.AgeLevel>>
            s_messagesToBeSent;
        private static readonly MessageParser<ArdkNextTelemetryOmniProto> s_protoMessageParser;

        private static readonly TimeSpan s_publishLoopDelay = TimeSpan.FromSeconds(1);

        static TelemetryService()
        {
            s_messagesToBeSent = new ConcurrentQueue<KeyValuePair<ArdkNextTelemetryOmniProto, ARClientEnvelope.Types.AgeLevel>>();
            s_protoMessageParser = new MessageParser<ArdkNextTelemetryOmniProto>(() => new ArdkNextTelemetryOmniProto());
        }

        public TelemetryService(ITelemetryPublisher telemetryPublisher, string apiKey)
        {
            _telemetryPublisher = telemetryPublisher;
            _apiKey = apiKey;

            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(async () => { await TimedPublish(s_publishLoopDelay); });
        }

        public static void PublishEvent(ArdkNextTelemetryOmniProto ardkNextTelemetryOmniProto)
        {
            PublishEvent(ardkNextTelemetryOmniProto, requestId: string.Empty);
        }

        public static void PublishEvent(ArdkNextTelemetryOmniProto ardkNextTelemetryOmniProto, string requestId)
        {
            if(ardkNextTelemetryOmniProto.TimestampMs == default)
                ardkNextTelemetryOmniProto.TimestampMs = GetCurrentUtcTimestamp();

            ardkNextTelemetryOmniProto.ArCommonMetadata = Metadata.GetArCommonMetadata(requestId);
            s_messagesToBeSent.Enqueue(
                new KeyValuePair<ArdkNextTelemetryOmniProto, ARClientEnvelope.Types.AgeLevel>(
                    ardkNextTelemetryOmniProto, Metadata.AgeLevel));
        }

        // Has to be internal since we provide it for nar system initialization in StartupSystems
        internal delegate void _ARDKTelemetry_Callback(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] requestId, UInt32 requestIdLength,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] serialisedProto, UInt32 length);

        [MonoPInvokeCallback(typeof(_ARDKTelemetry_Callback))]
        internal static void OnNativeRecordTelemetry(byte[] requestId, UInt32 requestIdLength, byte[] serialisedPayload,
            UInt32 payloadLength)
        {
            try
            {
                var omniProtoObject = s_protoMessageParser.ParseFrom(serialisedPayload);
                if (omniProtoObject.TimestampMs == default)
                {
                    omniProtoObject.TimestampMs = GetCurrentUtcTimestamp();
                }

                var requestIdString = string.Empty;
                try
                {
                    // GetString() can throw NullRef, Argument and Decoding exceptions. We cannot do anything about it.
                    // so we log the exception and move on.
                    if (requestIdLength > 0)
                    {
                        requestIdString = Encoding.UTF8.GetString(requestId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarningFormat("Getting requestId failed with {0}", ex);
                    requestIdString = string.Empty;
                }

                omniProtoObject.ArCommonMetadata = Metadata.GetArCommonMetadata(requestIdString);

                s_messagesToBeSent.Enqueue(
                    new KeyValuePair<ArdkNextTelemetryOmniProto, ARClientEnvelope.Types.AgeLevel>(omniProtoObject,
                        Metadata.AgeLevel));
            }
            catch (Exception e)
            {
                // failing silently and not bothering the users
                Debug.LogWarningFormat("Sending telemetry failed: {0}.", e);
            }
        }

        private async Task TimedPublish(TimeSpan ts)
        {
            // in case the publish task dies for some reason, try again.
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    while (!s_messagesToBeSent.IsEmpty)
                    {
                        if (s_messagesToBeSent.TryDequeue(out var eventToPublish))
                        {
                            eventToPublish.Key.DeveloperKey = _apiKey;
                            _telemetryPublisher.RecordEvent(eventToPublish.Key, eventToPublish.Value);
                        }
                    }

                    await Task.Delay(ts, _cancellationTokenSource.Token);
                }
                catch (Exception e)
                {
                    // for local debugging only.
                    //Debug.Log($"Encountered exception: {e} while running the timed telemetry loop");
                }
            }
        }

        private static long GetCurrentUtcTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            if (!s_messagesToBeSent.IsEmpty)
            {
                // for local debugging only.
                // Debug.LogWarning($"Events to be dropped: {s_messagesToBeSent.Count}");
            }
        }
    }
}
