using System;

using Niantic.Lightship.AR.Subsystems;

using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PersistentAnchorSubsystem
{
    internal interface _IApi
    {
        public IntPtr Construct(IntPtr unityContext);

        public void Start(IntPtr persistentAnchorApiHandle);

        public void Stop(IntPtr persistentAnchorApiHandle);

        public void Configure(IntPtr persistentAnchorApiHandle);

        public void Destruct(IntPtr persistentAnchorApiHandle);

        public bool TryAddAnchor(IntPtr persistentAnchorApiHandle, Pose pose, out TrackableId anchorId);

        public bool TryRemoveAnchor(IntPtr persistentAnchorApiHandle, TrackableId anchorId);

        public bool TryRestoreAnchor(IntPtr persistentAnchorApiHandle, IntPtr anchorPayload, int payloadSize,
            out TrackableId anchorId);

        public bool TryLocalize(IntPtr persistentAnchorApiHandle, IntPtr anchorPayload, int payloadSize,
            out TrackableId anchorId);

        public IntPtr AcquireLatestChanges(IntPtr anchorApiHandle, out IntPtr addedPtr, out int addedCount,
            out IntPtr updatedPtr,
            out int updatedCount, out IntPtr removedPtr, out int removedCount);

        public void ReleaseLatestChanges(IntPtr latestChangesHandle);

        public bool TryExtractAnchorChange(IntPtr anchorIntPtr,
            out TrackableId trackableId,
            out Pose pose,
            out int trackingState,
            out int trackingStateReason,
            out IntPtr anchorPayloadPtr,
            out int anchorPayloadSize);

        public IntPtr AcquireNetworkStatus(IntPtr anchorApiHandle, out IntPtr networkStatusList, out int listCount);

        public void ReleaseNetworkStatus(IntPtr networkStatusHandle);

        public bool TryExtractNetworkStatus(
            IntPtr anchorChangeIntPtr,
            out Guid networkRequestId,
            out RequestStatus networkStatus,
            out RequestType typeOut,
            out ErrorCode errorOut,
            out UInt64 startTimeMsOut,
            out UInt64 endTimeMsOut
        );

        public IntPtr AcquireLocalizationStatus(IntPtr anchorApiHandle, out IntPtr localizationStatusList, out int listCount);

        public void ReleaseLocalizationStatus(IntPtr localizationStatusHandle);

        public bool TryExtractLocalizationStatus(IntPtr statusIntPtr,
            out Guid nodeId,
            out LocalizationStatus statusOut,
            out float confidenceOut);
    }
}
