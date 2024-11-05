// Copyright 2022-2024 Niantic.
using System;
using System.Text;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.PersistentAnchor
{
    internal class NativeApi : IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            return Native.Construct(unityContext);
        }

        public void Start(IntPtr anchorProviderHandle)
        {
            Native.Start(anchorProviderHandle);
        }

        public void Stop(IntPtr anchorProviderHandle)
        {
            Native.Stop(anchorProviderHandle);
        }

        /// <summary>
        /// Defined in ardk_anchor_configuration.h file.
        /// Note: We need this because passing configurations as arguments breaks down
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct AnchorConfigurationCStruct
        {
            [MarshalAs(UnmanagedType.U1)]
            public bool enableContinuousLocalization;

            [MarshalAs(UnmanagedType.U1)]
            public bool enableTemporalFusion;

            [MarshalAs(UnmanagedType.U1)]
            public bool enableTransformSmoothing;

            [MarshalAs(UnmanagedType.U1)]
            public bool enableCloudLocalization;

            [MarshalAs(UnmanagedType.U1)]
            public bool enableSlickLocalization;

            [MarshalAs(UnmanagedType.U1)]
            public bool enableSlickLearnedFeatures;

            [MarshalAs(UnmanagedType.U1)]
            public bool forceCPULearnedFeatures;

            public float cloudLocalizerInitialRequestsPerSecond;

            public float cloudLocalizerContinuousRequestsPerSecond;

            public UInt32 slickLocalizerFps;

            public UInt32 cloudTemporalFusionWindowSize;

            public UInt32 slickTemporalFusionWindowSize;

            [MarshalAs(UnmanagedType.U1)]
            public bool enableDiagnostics;

            [MarshalAs(UnmanagedType.U1)]
            public bool limitedLocalizationsOnly;

            public Int32 jpegCompressionQuality;
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
            bool enableDiagnostics,
            bool limitedLocalizationsOnly,
            int jpegCompressionQuality)
        {
            var configurationCStruct = new AnchorConfigurationCStruct();
            configurationCStruct.enableContinuousLocalization = enableContinuousLocalization;
            configurationCStruct.enableTemporalFusion = enableTemporalFusion;
            configurationCStruct.enableTransformSmoothing = enableTransformSmoothing;
            configurationCStruct.enableCloudLocalization = enableCloudLocalization;
            configurationCStruct.enableSlickLocalization = enableSlickLocalization;
            configurationCStruct.enableSlickLearnedFeatures = enableSlickLearnedFeatures;
#if NIANTIC_LIGHTSHIP_ML2_ENABLED
            configurationCStruct.forceCPULearnedFeatures = true;
#else
            configurationCStruct.forceCPULearnedFeatures = useSlickCpuLearnedFeatures;
#endif
            configurationCStruct.cloudLocalizerInitialRequestsPerSecond = cloudLocalizerInitialRequestsPerSecond;
            configurationCStruct.cloudLocalizerContinuousRequestsPerSecond = cloudLocalizerContinuousRequestsPerSecond;
            configurationCStruct.slickLocalizerFps = Convert.ToUInt32(Math.Ceiling(slickLocalizerFps));
            configurationCStruct.cloudTemporalFusionWindowSize = cloudTemporalFusionWindowSize == 0
                ? XRPersistentAnchorConfiguration.DefaultCloudLocalizationTemporalFusionWindowSize
                : cloudTemporalFusionWindowSize;

            configurationCStruct.slickTemporalFusionWindowSize = slickTemporalFusionWindowSize == 0
                ? XRPersistentAnchorConfiguration.DefaultDeviceMappingLocalizationTemporalFusionWindowSize
                : slickTemporalFusionWindowSize;
            configurationCStruct.enableDiagnostics = enableDiagnostics;
            configurationCStruct.limitedLocalizationsOnly = limitedLocalizationsOnly;
            configurationCStruct.jpegCompressionQuality = jpegCompressionQuality;
            Native.Configure(anchorProviderHandle, configurationCStruct);
        }

        public void Destruct(IntPtr persistentAnchorApiHandle)
        {
            Native.Destruct(persistentAnchorApiHandle);
        }

        public void TryCreateAnchor(IntPtr anchorProviderHandle, Pose pose, out TrackableId anchorId)
        {
            Matrix4x4 poseMatrix = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
            float[] poseArray = MatrixConversionHelper.Matrix4x4ToInternalArray(poseMatrix.FromUnityToArdk());
            Native.TryCreateAnchor(anchorProviderHandle, poseArray, out anchorId);
        }

        public bool TryRemoveAnchor(IntPtr anchorProviderHandle, TrackableId anchorId)
        {
            return Native.TryRemoveAnchor(anchorProviderHandle, ref anchorId);
        }

        public bool TryTrackAnchor
        (
            IntPtr anchorProviderHandle,
            IntPtr anchorPayload,
            int payloadSize,
            out TrackableId anchorId
        )
        {
            return Native.TryTrackAnchor(anchorProviderHandle, anchorPayload, payloadSize, out anchorId);
        }

        public IntPtr AcquireLatestChanges
        (
            IntPtr anchorProviderHandle,
            out IntPtr addedPtr,
            out int addedCount,
            out IntPtr updatedPtr,
            out int updatedCount,
            out IntPtr removedPtr,
            out int removedCount
        )
        {
            return Native.AcquireLatestChanges
            (
                anchorProviderHandle,
                out addedPtr,
                out addedCount,
                out updatedPtr,
                out updatedCount,
                out removedPtr,
                out removedCount
            );
        }

        public void ReleaseLatestChanges(IntPtr context)
        {
            Native.ReleaseLatestChanges(context);
        }

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
        )
        {
            var poseArray = new NativeArray<float>(16, Allocator.Temp);
            bool success;
            unsafe
            {
                success = Native.TryExtractAnchorChange
                (
                    anchorChangeHandle,
                    out trackableId,
                    (IntPtr)poseArray.GetUnsafePtr(),
                    out trackingState,
                    out trackingStateReason,
                    out trackingConfidence,
                    out anchorPayloadPtr,
                    out anchorPayloadSize,
                    out timestampMs
                );
            }

            var matrix = MatrixConversionHelper.InternalToMatrix4x4(poseArray.ToArray()).FromArdkToUnity();
            var position = MatrixUtils.PositionFromMatrix(matrix);
            var rotation = MatrixUtils.RotationFromMatrix(matrix);
            pose = new Pose(position, rotation);

            return success;
        }

        public IntPtr AcquireNetworkStatus(IntPtr anchorProviderHandle, out IntPtr networkStatusList, out int listCount)
        {
            return Native.AcquireNetworkStatus(anchorProviderHandle, out networkStatusList, out listCount);
        }

        public void ReleaseNetworkStatus(IntPtr networkStatusHandle)
        {
            Native.ReleaseNetworkStatus(networkStatusHandle);
        }

        public bool TryExtractNetworkStatus
        (
            IntPtr networkStatusHandle,
            out Guid trackableId,
            out RequestStatus networkStatus,
            out RequestType typeOut,
            out ErrorCode errorOut,
            out ulong startTimeMsOut,
            out ulong endTimeMsOut,
            out UInt64 frameIdOut
        )
        {
            var success = Native.TryExtractNetworkStatus
            (
                networkStatusHandle,
                out trackableId,
                out var nativeNetworkStatus,
                out var nativeTypeOut,
                out var nativeErrorOut,
                out startTimeMsOut,
                out endTimeMsOut,
                out frameIdOut
            );

            networkStatus = (RequestStatus)nativeNetworkStatus;
            typeOut = (RequestType)nativeTypeOut;
            errorOut = (ErrorCode)nativeErrorOut;
            return success;
        }

        public IntPtr AcquireLocalizationStatus
        (
            IntPtr anchorProviderHandle,
            out IntPtr localizationStatusList,
            out int listCount
        )
        {
            return Native.AcquireLocalizationStatus(anchorProviderHandle, out localizationStatusList, out listCount);
        }

        public void ReleaseLocalizationStatus(IntPtr localizationStatusHandle)
        {
            Native.ReleaseLocalizationStatus(localizationStatusHandle);
        }

        public bool TryExtractLocalizationStatus
        (
            IntPtr localizationStatusHandle,
            out Guid nodeId,
            out LocalizationStatus statusOut,
            out float confidenceOut,
            out UInt64 frameIdOut
        )
        {
            var success =
                Native.TryExtractLocalizationStatus
                (
                    localizationStatusHandle,
                    out nodeId,
                    out var nativeStatusOut,
                    out confidenceOut,
                    out frameIdOut
                );

            statusOut = (LocalizationStatus)nativeStatusOut;
            return success;
        }

        public IntPtr AcquireFrameDiagnostics(IntPtr anchorProviderHandle, out IntPtr diagnosticsList, out int listCount)
        {
            return Native.AcquireFrameDiagnostics(anchorProviderHandle, out diagnosticsList, out listCount);
        }

        public void ReleaseFrameDiagnostics(IntPtr arrayHandle)
        {
            Native.ReleaseFrameDiagnostics(arrayHandle);
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
            var success =
                Native.TryExtractFrameDiagnostics
                (
                    diagnosticsHandle,
                    out frameId,
                    out timestampMs,
                    out labelNameList,
                    out labelScoreList,
                    out labelCount
                );

            return success;
        }

        public bool GetVpsSessionId(IntPtr anchorProviderHandle, out string vpsSessionId)
        {
            StringBuilder buffer = new StringBuilder(32); // sessionId is as 32 character hexidecimal upper-case string
            if (!Native.GetVpsSessionId(anchorProviderHandle, buffer))
            {
                vpsSessionId = default;
                return false;
            }

            vpsSessionId = buffer.ToString();
            return true;
        }


        private static class Native
        {
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Construct")]
            public static extern IntPtr Construct(IntPtr unityContext);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Start")]
            public static extern void Start(IntPtr anchorApiHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Stop")]
            public static extern void Stop(IntPtr anchorApiHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Configure")]
            public static extern void Configure
            (   IntPtr anchorProviderHandle,
                AnchorConfigurationCStruct config
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Destruct")]
            public static extern void Destruct(IntPtr anchorApiHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryCreateAnchor")]
            public static extern void TryCreateAnchor
                (IntPtr anchorApiHandle, float[] poseMatrix, out TrackableId anchorId);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryRemoveAnchor")]
            public static extern bool TryRemoveAnchor(IntPtr anchorApiHandle, ref TrackableId anchorId);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryTrackAnchor")]
            public static extern bool TryTrackAnchor
                (IntPtr persistentAnchorApiHandle, IntPtr anchorPayload, int payloadSize, out TrackableId anchorId);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_AcquireLatestChanges")]
            public static extern IntPtr AcquireLatestChanges
            (
                IntPtr anchorApiHandle,
                out IntPtr addedPtr,
                out int addedCount,
                out IntPtr updatedPtr,
                out int updatedCount,
                out IntPtr removedPtr,
                out int removedCount
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_ReleaseLatestChanges")]
            public static extern void ReleaseLatestChanges(IntPtr changesHandle);

            [DllImport
                (LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryExtractAnchorChange")]
            public static extern bool TryExtractAnchorChange
            (
                IntPtr anchorChangeIntPtr,
                out TrackableId trackableId,
                IntPtr poseIntPtr,
                out int trackingState,
                out int trackingStateReason,
                out float trackingConfidence,
                out IntPtr anchorPayloadPtr,
                out int anchorPayloadSize,
                out UInt64 timestampMs
            );

            [DllImport
            (
                LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_AcquireLatestNetworkRequestStates"
            )]
            public static extern IntPtr AcquireNetworkStatus
                (IntPtr anchorApiHandle, out IntPtr addedPtr, out int addedCount);

            [DllImport
            (
                LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_ReleaseLatestNetworkRequestStates"
            )]
            public static extern void ReleaseNetworkStatus(IntPtr arrayHandle);

            [DllImport
            (
                LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryExtractNetworkRequestState"
            )]
            public static extern bool TryExtractNetworkStatus
            (
                IntPtr anchorChangeIntPtr,
                out Guid trackableId,
                out byte networkStatus,
                out byte typeOut,
                out UInt32 errorOut,
                out UInt64 startTimeMsOut,
                out UInt64 endTimeMsOut,
                out UInt64 frameIdOut
            );

            [DllImport
            (
                LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_AcquireLatestLocalizationUpdates"
            )]
            public static extern IntPtr AcquireLocalizationStatus
            (
                IntPtr anchorApiHandle,
                out IntPtr addedPtr,
                out int addedCount
            );

            [DllImport
            (
                LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_ReleaseLatestLocalizationUpdates"
            )]
            public static extern void ReleaseLocalizationStatus(IntPtr arrayHandle);

            [DllImport
            (
                LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryExtractLocalizationUpdate"
            )]
            public static extern bool TryExtractLocalizationStatus
            (
                IntPtr anchorChangeIntPtr,
                out Guid trackableId,
                out byte status,
                out float confidence,
                out UInt64 frameId
            );

            [DllImport
            (
                LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_AcquireLatestVpsDiagnostics"
            )]
            public static extern IntPtr AcquireFrameDiagnostics
            (
                IntPtr anchorApiHandle,
                out IntPtr elementListPr,
                out int elementCount
            );

            [DllImport
            (
                LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_ReleaseLatestVpsDiagnostics"
            )]
            public static extern void ReleaseFrameDiagnostics(IntPtr arrayHandle);

            [DllImport
            (
                LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryExtractVpsDiagnostics"
            )]
            public static extern bool TryExtractFrameDiagnostics
            (
                IntPtr diagnosticsHandle,
                out UInt64 frameId,
                out UInt64 timestampMs,
                out IntPtr labelNameList,
                out IntPtr labelScoreList,
                out UInt32 labelCount
            );

            [DllImport
            (
                LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_GetVpsSessionId"
            )]
            public static extern bool GetVpsSessionId
            (
                IntPtr anchorChangeIntPtr,
                StringBuilder vpsSessionIdOut
            );
        }
    }
}
