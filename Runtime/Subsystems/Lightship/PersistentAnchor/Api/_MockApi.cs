using System;
using Niantic.Lightship.AR.Subsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PersistentAnchorSubsystem
{
    public class _MockApi : _IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            throw new NotImplementedException();
        }

        public void Start(IntPtr persistentAnchorApiHandle)
        {
            throw new NotImplementedException();
        }

        public void Stop(IntPtr persistentAnchorApiHandle)
        {
            throw new NotImplementedException();
        }

        public void Configure(IntPtr persistentAnchorApiHandle)
        {
            throw new NotImplementedException();
        }

        public void Destruct(IntPtr persistentAnchorApiHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryAddAnchor(IntPtr persistentAnchorApiHandle, Pose pose, out TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveAnchor(IntPtr persistentAnchorApiHandle, TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public bool TryRestoreAnchor(IntPtr persistentAnchorApiHandle, IntPtr anchorPayload, int payloadSize,
            out TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public bool TryLocalize(IntPtr persistentAnchorApiHandle, IntPtr anchorPayload, int payloadSize,
            out TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public IntPtr AcquireLatestChanges(IntPtr anchorApiHandle, out IntPtr addedPtr, out int addedCount,
            out IntPtr updatedPtr,
            out int updatedCount, out IntPtr removedPtr, out int removedCount)
        {
            throw new NotImplementedException();
        }

        public void ReleaseLatestChanges(IntPtr context)
        {
            throw new NotImplementedException();
        }

        public bool TryExtractAnchorChange(IntPtr anchorIntPtr, out TrackableId trackableId, out Pose pose,
            out int trackingState, out int trackingStateReason, out IntPtr anchorPayloadPtr, out int anchorPayloadSize)
        {
            throw new NotImplementedException();
        }

        public IntPtr AcquireNetworkStatus
            (IntPtr anchorApiHandle, out IntPtr networkStatusList, out int listCount) =>
            throw new NotImplementedException();

        public void ReleaseNetworkStatus(IntPtr latestChangesHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryExtractNetworkStatus
        (
            IntPtr anchorChangeIntPtr,
            out Guid requestId,
            out RequestStatus networkStatus,
            out RequestType typeOut,
            out ErrorCode errorOut,
            out ulong startTimeMsOut,
            out ulong endTimeMsOut
        ) =>
            throw new NotImplementedException();

        public IntPtr AcquireLocalizationStatus
            (IntPtr anchorApiHandle, out IntPtr localizationStatusList, out int listCount) =>
            throw new NotImplementedException();

        public void ReleaseLocalizationStatus(IntPtr latestChangesHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryExtractLocalizationStatus
            (IntPtr statusIntPtr, out Guid nodeId, out LocalizationStatus statusOut, out float confidenceOut) =>
            throw new NotImplementedException();
    }
}
