// Copyright 2022-2024 Niantic.
using System;

using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.PersistentAnchor
{
    internal interface IApi
    {
        public IntPtr Construct(IntPtr unityContext);

        public void Start(IntPtr anchorProviderHandle);

        public void Stop(IntPtr anchorProviderHandle);

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
            int jpegCompressionQuality);

        public void Destruct(IntPtr anchorProviderHandle);

        public void TryCreateAnchor(IntPtr anchorProviderHandle, Pose pose, out TrackableId anchorId);

        public bool TryRemoveAnchor(IntPtr anchorProviderHandle, TrackableId anchorId);

        public bool TryTrackAnchor(IntPtr anchorProviderHandle, IntPtr anchorPayload, int payloadSize, out TrackableId anchorId);

        public IntPtr AcquireLatestChanges
        (
            IntPtr anchorProviderHandle,
            out IntPtr addedPtr,
            out int addedCount,
            out IntPtr updatedPtr,
            out int updatedCount,
            out IntPtr removedPtr,
            out int removedCount
        );

        public void ReleaseLatestChanges(IntPtr latestChangesHandle);

        public bool TryExtractAnchorChange
        (
            IntPtr anchorChangeHandle,
            out TrackableId trackableId,
            out Pose pose,
            out int trackingState,
            out int trackingStateReason,
            out float trackingConfidence,
            out IntPtr anchorPayloadPtr,
            out int anchorPayloadSize,
            out UInt64 timestampMs
        );

        public IntPtr AcquireNetworkStatus(IntPtr anchorProviderHandle, out IntPtr networkStatusList, out int listCount);

        public void ReleaseNetworkStatus(IntPtr networkStatusHandle);

        public bool TryExtractNetworkStatus
        (
            IntPtr networkStatusHandle,
            out Guid networkRequestId,
            out RequestStatus networkStatus,
            out RequestType typeOut,
            out ErrorCode errorOut,
            out UInt64 startTimeMsOut,
            out UInt64 endTimeMsOut,
            out UInt64 frameIdOut
        );

        public IntPtr AcquireLocalizationStatus(IntPtr anchorProviderHandle, out IntPtr localizationStatusList, out int listCount);

        public void ReleaseLocalizationStatus(IntPtr localizationStatusHandle);

        public bool TryExtractLocalizationStatus
        (
            IntPtr localizationStatusHandle,
            out Guid nodeId,
            out LocalizationStatus statusOut,
            out float confidenceOut,
            out UInt64 frameIdOut
        );

        public IntPtr AcquireFrameDiagnostics(IntPtr anchorProviderHandle, out IntPtr diagnosticsList, out int listCount);

        public void ReleaseFrameDiagnostics(IntPtr diagnosticsHandle);

        public bool TryExtractFrameDiagnostics
        (
            IntPtr diagnosticsHandle,
            out UInt64 frameId,
            out UInt64 timestampMs,
            out IntPtr labelNameList,
            out IntPtr labelScoreList,
            out UInt32 labelCount
        );

        public bool GetVpsSessionId(IntPtr anchorProviderHandle, out string vpsSessionId);
    }
}
