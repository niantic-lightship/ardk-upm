using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems.Camera
{
    /// <summary>
    /// Defines the native camera subsystem API.
    /// </summary>
    internal static class NativeApi
    {
        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_Construct")]
        public static extern IntPtr Construct();

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_Destruct")]
        public static extern void Destruct(IntPtr handle);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_Initialize")]
        public static extern bool Initialize(IntPtr handle);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_Start")]
        public static extern void Start(IntPtr handle);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_Stop")]
        public static extern void Stop(IntPtr handle);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_TryAcquireLatestImageRGBA")]
        public static extern IntPtr TryAcquireLatestImageRGBA(IntPtr handle, out IntPtr buffer, out int size, out int width,
            out int height, out TextureFormat format, out ulong timestampMs);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_TryAcquireLatestImageYUV")]
        public static extern IntPtr TryAcquireLatestImageYUV(IntPtr handle, out IntPtr plane0, out int size0,
            out IntPtr plane1, out int size1, out IntPtr plane2, out int size2, out int width, out int height,
            out int yuvFormat, out ulong timestampMs);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_TryGetLatestIntrinsics")]
        public static extern bool TryGetLatestIntrinsics(IntPtr nativeProviderHandle, float[] outMatrix3X3);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_TryGetSensorResolution")]
        public static extern bool TryGetSensorResolution(IntPtr nativeProviderHandle, out int width, out int height);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_TryGetLensOffset")]
        public static extern bool TryGetLensOffset(IntPtr nativeProviderHandle, float[] outVector3, float[] outQuaternion);

        /// <summary>
        /// Defines APIs for managing native camera resources.
        /// </summary>
        public static class Resource
        {
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_Resource_GetMetaData")]
            public static extern bool GetMetaData(IntPtr resourceHandle, out int width, out int height,
                out TextureFormat format, out ulong timestampMs);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_Resource_GetIntrinsics")]
            public static extern bool GetIntrinsics(IntPtr resourceHandle, float[] outMatrix3X3);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_CameraSubsystem_Resource_Release")]
            public static extern void Release(IntPtr resourceHandle);
        }
    }
}
