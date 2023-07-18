using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PersistentAnchorSubsystem
{
    public class _NativeApi : _IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            if (unityContext == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call Construct with IntPtr.Zero");
            }

            return Native.Construct(unityContext);
        }

        public void Start(IntPtr persistentAnchorApiHandle)
        {
            if (persistentAnchorApiHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call Start with IntPtr.Zero");
            }

            Native.Start(persistentAnchorApiHandle);
        }

        public void Stop(IntPtr persistentAnchorApiHandle)
        {
            if (persistentAnchorApiHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call Stop with IntPtr.Zero");
            }

            Native.Stop(persistentAnchorApiHandle);
        }

        public void Configure(IntPtr persistentAnchorApiHandle)
        {
            if (persistentAnchorApiHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call Configure with IntPtr.Zero");
            }

            Native.Configure(persistentAnchorApiHandle);
        }

        public void Destruct(IntPtr persistentAnchorApiHandle)
        {
            if (persistentAnchorApiHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call Destruct with IntPtr.Zero");
            }

            Native.Destruct(persistentAnchorApiHandle);
        }

        public bool TryAddAnchor(IntPtr persistentAnchorApiHandle, Pose pose, out TrackableId anchorId)
        {
            if (persistentAnchorApiHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call TryAddAnchor with IntPtr.Zero");
            }

            Matrix4x4 poseMatrix = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
            float[] poseArray = _Convert.Matrix4x4ToInternalArray(poseMatrix.FromUnityToArdk());
            return Native.TryCreateAnchor(persistentAnchorApiHandle, poseArray, out anchorId);
        }

        public bool TryRemoveAnchor(IntPtr persistentAnchorApiHandle, TrackableId anchorId)
        {
            // Return false instead of exception due to shutdown OnDestroy from ARPersistentAnchors
            if (persistentAnchorApiHandle == IntPtr.Zero)
            {
                return false;
            }

            return Native.TryRemoveAnchor(persistentAnchorApiHandle, ref anchorId);
        }

        public bool TryRestoreAnchor(IntPtr persistentAnchorApiHandle, IntPtr anchorPayload, int payloadSize,
            out TrackableId anchorId)
        {
            if (persistentAnchorApiHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call TryRestoreAnchor with IntPtr.Zero");
            }

            return Native.TryTrackAnchor(persistentAnchorApiHandle, anchorPayload, payloadSize, out anchorId);
        }

        public bool TryLocalize(IntPtr persistentAnchorApiHandle, IntPtr anchorPayload, int payloadSize,
            out TrackableId anchorId)
        {
            if (persistentAnchorApiHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call TryLocalize with IntPtr.Zero");
            }

            return Native.TryTrackAnchor(persistentAnchorApiHandle, anchorPayload, payloadSize, out anchorId);
        }

        public IntPtr AcquireLatestChanges(IntPtr anchorApiHandle, out IntPtr addedPtr, out int addedCount,
            out IntPtr updatedPtr,
            out int updatedCount, out IntPtr removedPtr, out int removedCount)
        {
            if (anchorApiHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call AcquireLatestChanges with IntPtr.Zero");
            }

            return Native.AcquireLatestChanges(anchorApiHandle, out addedPtr, out addedCount, out updatedPtr,
                out updatedCount, out removedPtr, out removedCount);
        }

        public void ReleaseLatestChanges(IntPtr context)
        {
            if (context == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call ReleaseLatestChanges with IntPtr.Zero");
            }

            Native.ReleaseLatestChanges(context);
        }

        public bool TryExtractAnchorChange(IntPtr anchorChangeIntPtr, out TrackableId trackableId, out Pose pose,
            out int trackingState, out int trackingStateReason, out IntPtr anchorPayloadPtr, out int anchorPayloadSize)
        {
            if (anchorChangeIntPtr == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call TryExtractAnchorChange with IntPtr.Zero");
            }

            var poseArray = new NativeArray<float>(16, Allocator.Temp);
            bool success;
            int internalTrackingState;
            unsafe
            {
                success = Native.TryExtractAnchorChange(anchorChangeIntPtr, out trackableId,
                    (IntPtr)poseArray.GetUnsafePtr(), out trackingState, out trackingStateReason,
                    out anchorPayloadPtr, out anchorPayloadSize);
            }

            if (trackingState == (int)TrackingState.Tracking)
            {
                var matrix = _Convert.InternalToMatrix4x4(poseArray.ToArray()).FromArdkToUnity();
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

        public IntPtr AcquireNetworkStatus
        (
            IntPtr anchorApiHandle,
            out IntPtr networkStatusList,
            out int listCount
        )
        {
            if (anchorApiHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call AcquireNetworkStatus with IntPtr.Zero");
            }
            return Native.AcquireNetworkStatus
            (
                anchorApiHandle,
                out networkStatusList,
                out listCount
            );
        }

        public void ReleaseNetworkStatus(IntPtr networkStatusHandle)
        {
            if (networkStatusHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call ReleaseNetworkStatus with IntPtr.Zero");
            }

            Native.ReleaseNetworkStatus(networkStatusHandle);
        }

        public bool TryExtractNetworkStatus
        (
            IntPtr anchorChangeIntPtr,
            out Guid trackableId,
            out RequestStatus networkStatus,
            out RequestType typeOut,
            out ErrorCode errorOut,
            out ulong startTimeMsOut,
            out ulong endTimeMsOut
        )
        {
            if (anchorChangeIntPtr == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call TryExtractNetworkStatus with IntPtr.Zero");
            }

            var success = Native.TryExtractNetworkStatus(anchorChangeIntPtr,
                out trackableId,
                out var nativeNetworkStatus,
                out var nativeTypeOut,
                out var nativeErrorOut,
                out startTimeMsOut,
                out endTimeMsOut);

            networkStatus = (RequestStatus)nativeNetworkStatus;
            typeOut = (RequestType)nativeTypeOut;
            errorOut = (ErrorCode)nativeErrorOut;
            return success;
        }

        public IntPtr AcquireLocalizationStatus
        (
            IntPtr anchorApiHandle,
            out IntPtr localizationStatusList,
            out int listCount
        )
        {
            if (anchorApiHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call AcquireLocalizationStatus with IntPtr.Zero");
            }

            return Native.AcquireLocalizationStatus
            (
                anchorApiHandle,
                out localizationStatusList,
                out listCount
            );
        }

        public void ReleaseLocalizationStatus(IntPtr localizationStatusHandle)
        {
            if (localizationStatusHandle == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call ReleaseLocalizationStatus with IntPtr.Zero");
            }

            Native.ReleaseLocalizationStatus(localizationStatusHandle);
        }

        public bool TryExtractLocalizationStatus
        (
            IntPtr statusIntPtr,
            out Guid nodeId,
            out LocalizationStatus statusOut,
            out float confidenceOut
        )
        {
            if (statusIntPtr == IntPtr.Zero)
            {
                throw new ArgumentException("Tried to call TryExtractLocalizationStatus with IntPtr.Zero");
            }

            var success = Native.TryExtractLocalizationStatus
            (
                statusIntPtr,
                out nodeId,
                out var nativeStatusOut,
                out confidenceOut
            );

            statusOut = (LocalizationStatus)nativeStatusOut;
            return success;
        }

        private static class Native
        {
            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Construct")]
            public static extern IntPtr Construct(IntPtr unityContext);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Start")]
            public static extern void Start(IntPtr anchorApiHandle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Stop")]
            public static extern void Stop(IntPtr anchorApiHandle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Configure")]
            public static extern void Configure(IntPtr anchorApiHandle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_Destruct")]
            public static extern void Destruct(IntPtr anchorApiHandle);

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryCreateAnchor")]
            public static extern bool TryCreateAnchor(IntPtr anchorApiHandle, float[] poseMatrix,
                out TrackableId anchorId);

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryRemoveAnchor")]
            public static extern bool TryRemoveAnchor(IntPtr anchorApiHandle, ref TrackableId anchorId);

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryTrackAnchor")]
            public static extern bool TryTrackAnchor(IntPtr persistentAnchorApiHandle, IntPtr anchorPayload,
                int payloadSize, out TrackableId anchorId);

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_AcquireLatestChanges")]
            public static extern IntPtr AcquireLatestChanges(IntPtr anchorApiHandle, out IntPtr addedPtr,
                out int addedCount,
                out IntPtr updatedPtr,
                out int updatedCount, out IntPtr removedPtr, out int removedCount);

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_ReleaseLatestChanges")]
            public static extern void ReleaseLatestChanges(IntPtr changesHandle);

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryExtractAnchorChange")]
            public static extern bool TryExtractAnchorChange(IntPtr anchorChangeIntPtr, out TrackableId trackableId,
                IntPtr poseIntPtr, out int trackingState, out int trackingStateReason, out IntPtr anchorPayloadPtr, out int anchorPayloadSize);

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_AcquireLatestNetworkRequestStates")]
            public static extern IntPtr AcquireNetworkStatus(IntPtr anchorApiHandle, out IntPtr addedPtr,
                out int addedCount);

            [DllImport(_LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_ReleaseLatestNetworkRequestStates")]
            public static extern void ReleaseNetworkStatus(IntPtr arrayHandle);

            [DllImport
            (
                _LightshipPlugin.Name,
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
                _LightshipPlugin.Name,
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
                _LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_ReleaseLatestLocalizationUpdates"
            )]
            public static extern void ReleaseLocalizationStatus(IntPtr arrayHandle);

            [DllImport
            (
                _LightshipPlugin.Name,
                EntryPoint = "Lightship_ARDK_Unity_AnchorProvider_TryExtractLocalizationUpdate"
            )]
            public static extern bool TryExtractLocalizationStatus
            (
                IntPtr anchorChangeIntPtr,
                out Guid trackableId,
                out byte status,
                out float confidence
            );
        }
    }
}
