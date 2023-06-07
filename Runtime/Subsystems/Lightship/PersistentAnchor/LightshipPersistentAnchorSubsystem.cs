using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.PersistentAnchorSubsystem;
using Niantic.Lightship.AR.Subsystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// The Lightship implementation of the <c>XRPersistentAnchorSubsystem</c>. Do not create this directly.
    /// Use the <c>SubsystemManager</c> instead.
    /// </summary>
    [Preserve]
    public sealed class LightshipPersistentAnchorSubsystem : XRPersistentAnchorSubsystem
    {
        internal class LightshipProvider : Provider
        {
            /// <summary>
            /// The VPS error states
            /// </summary>
            public enum VpsError
            {
                Unknown,
                None,
                NetworkingConnection,
                DevicePermissionNeeded,
                NoApiKey,
                BadApiKey,
                UnsupportedGpsLocation,
                AnchorTooFarFromGpsLocation,
                AnchorPermissionDenied,
                LocalizationTimeout,
                RequestsLimitExceeded,
                InvalidServerResponse,
                InternalServer,
                InternalClient
            }

            private _IApi _api;

            /// <summary>
            /// The handle to the native version of the provider
            /// </summary>
            private IntPtr nativeProviderHandle;

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipProvider()
            {
                Debug.Log("LightshipPersistentAnchorSubsystem.LightshipProvider construct");
                _api = new _NativeApi();
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                nativeProviderHandle = _api.Construct(LightshipUnityContext.UnityContextHandle);
#endif
                Debug.Log("LightshipPersistentAnchorSubsystem got nativeProviderHandle: " + nativeProviderHandle);
            }

            // Destruct the native provider and replace it with the provided (or default mock) provider
            // Used for testing and mocking
            internal bool _SwitchToMockImplementation(_IApi api = null)
            {
                if (nativeProviderHandle != IntPtr.Zero)
                {
                    _api.Stop(nativeProviderHandle);
                    _api.Destruct(nativeProviderHandle);
                }

                _api = api ?? new _MockApi();
                return true;
            }

            public override void Start()
            {
                _api.Start(nativeProviderHandle);
            }

            public override void Stop()
            {
                _api.Stop(nativeProviderHandle);
            }

            public override void Destroy()
            {
                _api.Destruct(nativeProviderHandle);
                nativeProviderHandle = IntPtr.Zero;;
            }

            public void Configure(IntPtr persistentAnchorApiHandle)
            {
                // TO DO: Expose configuration
                _api.Configure(persistentAnchorApiHandle);
            }

            public override TrackableChanges<XRPersistentAnchor> GetChanges(XRPersistentAnchor defaultAnchor,
                Allocator allocator)
            {
                var changes_handle = _api.AcquireLatestChanges(nativeProviderHandle,
                    out IntPtr addedPtr, out int addedCount,
                    out IntPtr updatedPtr, out int updatedCount,
                    out IntPtr removedPtr, out int removedCount);
                try
                {
                    unsafe
                    {
                        var trackablesAddedArray = new NativeArray<XRPersistentAnchor>(addedCount, Allocator.Temp);
                        var trackablesUpdatedArray = new NativeArray<XRPersistentAnchor>(updatedCount, Allocator.Temp);
                        var trackablesRemovedArray = new NativeArray<TrackableId>(removedCount, Allocator.Temp);

                        var addedIntPtrNativeArray = NativeCopyUtility.PtrToNativeArrayWithDefault(IntPtr.Zero,
                            addedPtr.ToPointer(), sizeof(IntPtr),
                            addedCount, Allocator.Temp);

                        var updatedIntPtrNativeArray = NativeCopyUtility.PtrToNativeArrayWithDefault(IntPtr.Zero,
                            updatedPtr.ToPointer(), sizeof(IntPtr),
                            updatedCount, Allocator.Temp);

                        var removedIntPtrNativeArray = NativeCopyUtility.PtrToNativeArrayWithDefault(IntPtr.Zero,
                            removedPtr.ToPointer(), sizeof(IntPtr),
                            removedCount, Allocator.Temp);

                        for (int i = 0; i < addedCount; i++)
                        {
                            trackablesAddedArray[i] = CreateXRPersistentAnchor(addedIntPtrNativeArray[i]);
                        }

                        for (int i = 0; i < updatedCount; i++)
                        {
                            trackablesUpdatedArray[i] = CreateXRPersistentAnchor(updatedIntPtrNativeArray[i]);
                        }

                        for (int i = 0; i < removedCount; i++)
                        {
                            var xrPersistentAnchor = CreateXRPersistentAnchor(removedIntPtrNativeArray[i]);
                            trackablesRemovedArray[i] = xrPersistentAnchor.trackableId;
                        }

                        return TrackableChanges<XRPersistentAnchor>.CopyFrom(trackablesAddedArray,
                            trackablesUpdatedArray,
                            trackablesRemovedArray, Allocator.Persistent);
                    }
                }
                finally
                {
                    _api.ReleaseLatestChanges(changes_handle);
                }
            }

            public override bool GetNetworkStatusUpdate(out XRPersistentAnchorNetworkRequestStatus[] statuses)
            {
                var handle = _api.AcquireNetworkStatus
                    (nativeProviderHandle, out var statusList, out var listCount);

                if (listCount == 0)
                {
                    statuses = default;
                    _api.ReleaseNetworkStatus(handle);
                    return false;
                }

                try
                {
                    statuses = new XRPersistentAnchorNetworkRequestStatus[listCount];
                    NativeArray<IntPtr> statusPtrList;
                    unsafe
                    {
                        statusPtrList = NativeCopyUtility.PtrToNativeArrayWithDefault
                        (
                            IntPtr.Zero,
                            statusList.ToPointer(),
                            sizeof(IntPtr),
                            listCount,
                            Allocator.Temp
                        );
                    }

                    for (int i = 0; i < listCount; i++)
                    {
                        statuses[i] = GetNetworkStatus(statusPtrList[i]);
                    }
                }
                finally
                {
                    _api.ReleaseNetworkStatus(handle);
                }

                return true;
            }

            public override bool GetLocalizationStatusUpdate(out XRPersistentAnchorLocalizationStatus[] statuses)
            {
                var handle = _api.AcquireLocalizationStatus
                    (nativeProviderHandle, out var statusList, out var listCount);

                if (listCount == 0)
                {
                    statuses = default;
                    _api.ReleaseLocalizationStatus(handle);
                    return false;
                }

                try
                {
                    statuses = new XRPersistentAnchorLocalizationStatus[listCount];
                    NativeArray<IntPtr> statusPtrList;
                    unsafe
                    {
                        statusPtrList = NativeCopyUtility.PtrToNativeArrayWithDefault
                        (
                            IntPtr.Zero,
                            statusList.ToPointer(),
                            sizeof(IntPtr),
                            listCount,
                            Allocator.Temp
                        );
                    }

                    for (int i = 0; i < listCount; i++)
                    {
                        statuses[i] = GetLocalizationStatus(statusPtrList[i]);
                    }
                }
                finally
                {
                    _api.ReleaseLocalizationStatus(handle);
                }

                return true;

            }

            public override bool TryAddAnchor(Pose pose, out XRPersistentAnchor anchor)
            {
                bool success = _api.TryAddAnchor(nativeProviderHandle, pose, out var anchorId);
                if (success)
                {
                    anchor = new XRPersistentAnchor(anchorId);
                }
                else
                {
                    anchor = default;
                }

                return success;
            }

            public override bool TryRemoveAnchor(TrackableId anchorId)
            {
                return _api.TryRemoveAnchor(nativeProviderHandle, anchorId);
            }

            public override bool TryRestoreAnchor(XRPersistentAnchorPayload anchorPayload,
                out XRPersistentAnchor anchor)
            {
                bool success =
                    _api.TryRestoreAnchor(nativeProviderHandle, anchorPayload.nativePtr, anchorPayload.size,
                        out var anchorId);
                if (success)
                {
                    anchor = new XRPersistentAnchor(anchorId);
                }
                else
                {
                    anchor = default;
                }

                return success;
            }

            public override bool TryLocalize(XRPersistentAnchorPayload anchorPayload, out XRPersistentAnchor anchor)
            {
                var success = _api.TryLocalize(nativeProviderHandle, anchorPayload.nativePtr, anchorPayload.size, out var anchorId);
                if (success)
                {
                    anchor = new XRPersistentAnchor(anchorId);
                }
                else
                {
                    Debug.LogError($"Failed to localize.");
                    anchor = default;
                }
                return success;
            }

            private XRPersistentAnchor CreateXRPersistentAnchor(IntPtr anchorChangeIntPtr)
            {
                bool success = _api.TryExtractAnchorChange(anchorChangeIntPtr,
                    out var trackableId,
                    out var pose, out int trackingState, out int trackingStateReason,
                    out var xrPersistentAnchorPayloadIntPtr, out int payloadSize);
                if (success)
                {
                    var xrPersistentAnchorPayload =
                        new XRPersistentAnchorPayload(xrPersistentAnchorPayloadIntPtr, payloadSize);
                    var xrPersistentAnchor = new XRPersistentAnchor(trackableId,
                        pose,
                        (TrackingState)trackingState,
                        (TrackingStateReason)trackingStateReason,
                        xrPersistentAnchorPayload);
                    return xrPersistentAnchor;
                }
                else
                {
                    Debug.LogError($"Failed to create XR Persistent Anchor.");
                    return default;
                }
            }

            private XRPersistentAnchorNetworkRequestStatus GetNetworkStatus(IntPtr statusIntPtr)
            {
                var success = _api.TryExtractNetworkStatus
                (
                    statusIntPtr,
                    out var requestId,
                    out var status,
                    out var type,
                    out var error,
                    out var startTime,
                    out var endTime
                );

                if (!success)
                {
                    Debug.LogError("Failed to extract network status");
                    return default;
                }

                return new XRPersistentAnchorNetworkRequestStatus
                {
                    RequestId = requestId,
                    Status = (RequestStatus)status,
                    Type = (RequestType)type,
                    Error = (ErrorCode)error,
                    StartTimeMs = startTime,
                    EndTimeMs = endTime
                };
            }

            private XRPersistentAnchorLocalizationStatus GetLocalizationStatus(IntPtr statusIntPtr)
            {
                var success = _api.TryExtractLocalizationStatus
                (
                    statusIntPtr,
                    out var requestId,
                    out var status,
                    out var confidence
                );

                if (!success)
                {
                    Debug.LogError("Failed to extract localization status");
                    return default;
                }

                return new XRPersistentAnchorLocalizationStatus
                {
                    NodeId = requestId,
                    Status = status,
                    confidence = confidence
                };
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor()
        {
            var cinfo = new XRPersistentAnchorSubsystemDescriptor.Cinfo
            {
                id = "Lightship-PersistentAnchor",
                providerType = typeof(LightshipProvider),
                subsystemTypeOverride = typeof(LightshipPersistentAnchorSubsystem),
                supportsTrackableAttachments = true
            };

            XRPersistentAnchorSubsystemDescriptor.Create(cinfo);
        }
    }
}
