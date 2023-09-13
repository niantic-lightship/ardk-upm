using System;
using System.Text;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PersistentAnchorSubsystem
{
    public class NativeApi : IApi
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

        public void Configure(IntPtr anchorProviderHandle)
        {
            Native.Configure(anchorProviderHandle);
        }

        public void Destruct(IntPtr persistentAnchorApiHandle)
        {
            Native.Destruct(persistentAnchorApiHandle);
        }

        public bool TryCreateAnchor(IntPtr anchorProviderHandle, Pose pose, out TrackableId anchorId)
        {
            Matrix4x4 poseMatrix = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
            float[] poseArray = MatrixConversionHelper.Matrix4x4ToInternalArray(poseMatrix.FromUnityToArdk());
            return Native.TryCreateAnchor(anchorProviderHandle, poseArray, out anchorId);
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
                    out anchorPayloadPtr,
                    out anchorPayloadSize,
                    out timestampMs
                );
            }

            if (trackingState == (int)TrackingState.Tracking)
            {
                var matrix = MatrixConversionHelper.InternalToMatrix4x4(poseArray.ToArray()).FromArdkToUnity();
                var position = MatrixUtils.PositionFromMatrix(matrix);
                var rotation = MatrixUtils.RotationFromMatrix(matrix);
                pose = new Pose(position, rotation);
            }
            else
            {
                pose = default;
            }

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
            out ulong endTimeMsOut
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
                out endTimeMsOut
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
            out float confidenceOut
        )
        {
            var success =
                Native.TryExtractLocalizationStatus
                (
                    localizationStatusHandle,
                    out nodeId,
                    out var nativeStatusOut,
                    out confidenceOut
                );

            statusOut = (LocalizationStatus)nativeStatusOut;
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
            public static extern void Configure(IntPtr anchorApiHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Destruct")]
            public static extern void Destruct(IntPtr anchorApiHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryCreateAnchor")]
            public static extern bool TryCreateAnchor
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
                out UInt64 endTimeMsOut
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
                out float confidence
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
