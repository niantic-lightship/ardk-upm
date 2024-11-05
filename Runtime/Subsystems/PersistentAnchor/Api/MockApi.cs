// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.PersistentAnchor
{
    internal class MockApi : IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            throw new NotImplementedException();
        }

        public void Start(IntPtr anchorProviderHandle)
        {
            throw new NotImplementedException();
        }

        public void Stop(IntPtr anchorProviderHandle)
        {
            throw new NotImplementedException();
        }

        public void Configure(IntPtr anchorProviderHandle,
            bool enableContinuousLocalization,
            bool enableTemporalFusion,
            bool enableTransformSmoothing,
            bool enableCloudLocalization,
            bool enableSlickLocalization,
            bool enableSlickLearnedFeatures,
            bool useSlickCpuLearnedFeatures,
            float cloudLocalizerInitialRequestsPerSecond,
            float cloudLocalizerContinuousRequestsPerSecond,
            float slickLocalizerFps,
            UInt32 cloudTemporalFusionWindowSize,
            UInt32 slickTemporalFusionWindowSize,
            bool diagnosticsEnabled,
            bool limitedLocalizationsOnly,
            int jpegCompressionQuality)
        {
            throw new NotImplementedException();
        }

        public void Destruct(IntPtr persistentAnchorApiHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryAddMap(IntPtr anchorProviderHandle, byte[] dataBytes)
        {
            throw new NotImplementedException();
        }

        public bool TryAddGraph(IntPtr anchorProviderHandle, byte[] dataBytes)
        {
            throw new NotImplementedException();
        }

        public void TryCreateAnchor(IntPtr anchorProviderHandle, Pose pose, out TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveAnchor(IntPtr anchorProviderHandle, TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public bool TryTrackAnchor(IntPtr anchorProviderHandle, IntPtr anchorPayload, int payloadSize,
            out TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public bool TryLocalize(IntPtr persistentAnchorApiHandle, IntPtr anchorPayload, int payloadSize,
            out TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public IntPtr AcquireLatestChanges(IntPtr anchorProviderHandle, out IntPtr addedPtr, out int addedCount,
            out IntPtr updatedPtr,
            out int updatedCount, out IntPtr removedPtr, out int removedCount)
        {
            throw new NotImplementedException();
        }

        public void ReleaseLatestChanges(IntPtr context)
        {
            throw new NotImplementedException();
        }

        public bool TryExtractAnchorChange(IntPtr anchorChangeHandle, out TrackableId trackableId, out Pose pose,
            out int trackingState, out int trackingStateReason, out float trackingConfidence, out IntPtr anchorPayloadPtr, out int anchorPayloadSize, out UInt64 timestampMs)
        {
            throw new NotImplementedException();
        }

        public IntPtr AcquireNetworkStatus
            (IntPtr anchorProviderHandle, out IntPtr networkStatusList, out int listCount) =>
            throw new NotImplementedException();

        public void ReleaseNetworkStatus(IntPtr latestChangesHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryExtractNetworkStatus
        (
            IntPtr networkStatusHandle,
            out Guid requestId,
            out RequestStatus networkStatus,
            out RequestType typeOut,
            out ErrorCode errorOut,
            out ulong startTimeMsOut,
            out ulong endTimeMsOut,
            out UInt64 frameIdOut
        )
        {
            throw new NotImplementedException();
        }

        public IntPtr AcquireLocalizationStatus
            (IntPtr anchorProviderHandle, out IntPtr localizationStatusList, out int listCount)
        {
            throw new NotImplementedException();
        }

        public void ReleaseLocalizationStatus(IntPtr latestChangesHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryExtractLocalizationStatus
            (IntPtr localizationStatusHandle, out Guid nodeId, out LocalizationStatus statusOut, out float confidenceOut,
            out UInt64 frameIdOut)
        {
            throw new NotImplementedException();
        }

        public IntPtr AcquireFrameDiagnostics(IntPtr anchorProviderHandle, out IntPtr diagnosticsList, out int listCount)
        {
            throw new NotImplementedException();
        }

        public void ReleaseFrameDiagnostics(IntPtr diagnosticsHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryExtractFrameDiagnostics
        (
            IntPtr diagnosticsHandle,
            out UInt64 frameId,
            out UInt64 timestampMs,
            out IntPtr labelNameList,
            out IntPtr labelScoreList,
            out UInt32 labelCount
        )
        {
            throw new NotImplementedException();
        }

        public bool GetVpsSessionId (IntPtr anchorProviderHandle, out string vpsSessionId)
        {
            throw new NotImplementedException();
        }
    }
}
