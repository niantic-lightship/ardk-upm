// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems.Occlusion
{
    internal class NativeApi : IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            return Native.Construct(unityContext);
        }

        public void Start(IntPtr nativeProviderHandle)
        {
            Native.Start(nativeProviderHandle);
        }

        public void Stop(IntPtr nativeProviderHandle)
        {
            Native.Stop(nativeProviderHandle);
        }

        public void Configure(IntPtr nativeProviderHandle, byte mode, uint frameRate)
        {
            Native.Configure(nativeProviderHandle, mode, frameRate);
        }

        public void Destruct(IntPtr nativeProviderHandle)
        {
            Native.Destruct(nativeProviderHandle);
        }

        public IntPtr GetEnvironmentDepth
        (
            IntPtr nativeProviderHandle,
            out IntPtr memoryBuffer,
            out int size,
            out int width,
            out int height,
            out TextureFormat format,
            out uint frameId,
            out ulong frameTimestamp
        )
        {
            return Native.GetEnvironmentDepth
                (
                    nativeProviderHandle,
                    out memoryBuffer,
                    out size,
                    out width,
                    out height,
                    out format,
                    out frameId,
                    out frameTimestamp
                );
        }

        public IntPtr Warp
        (
            IntPtr nativeProviderHandle,
            IntPtr depthResourceHandle,
            float[] poseMatrix,
            int targetWidth,
            int targetHeight,
            float backProjectionPlane,
            out IntPtr memoryBuffer,
            out int size
        )
        {
            return Native.Warp
            (
                nativeProviderHandle,
                depthResourceHandle,
                poseMatrix,
                targetWidth,
                targetHeight,
                backProjectionPlane,
                out memoryBuffer,
                out size
            );
        }

        public IntPtr Blit
        (
            IntPtr nativeProviderHandle,
            IntPtr depthResourceHandle,
            int targetWidth,
            int targetHeight,
            out IntPtr memoryBuffer,
            out int size
        )
        {
            return Native.Blit
            (
                nativeProviderHandle,
                depthResourceHandle,
                targetWidth,
                targetHeight,
                out memoryBuffer,
                out size
            );
        }

        public bool TryGetLatestIntrinsicsMatrix(IntPtr nativeProviderHandle, out Matrix4x4 intrinsicsMatrix)
        {
            float[] intrinsics = new float[9];
            bool gotIntrinsics = Native.TryGetLatestIntrinsics(nativeProviderHandle, intrinsics);

            if (!gotIntrinsics)
            {
                intrinsicsMatrix = default;
                return false;
            }

            intrinsicsMatrix = new Matrix4x4
            (
                new Vector4(intrinsics[0], intrinsics[1], intrinsics[2], 0),
                new Vector4(intrinsics[3], intrinsics[4], intrinsics[5], 0),
                new Vector4(intrinsics[6], intrinsics[7], intrinsics[8], 0),
                new Vector4(0, 0, 0, 1)
            );
            return true;
        }

        public void DisposeResource(IntPtr nativeProviderHandle, IntPtr resourceHandle)
        {
            Native.DisposeResource(nativeProviderHandle, resourceHandle);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Construct")]
            public static extern IntPtr Construct(IntPtr unityContext);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Start")]
            public static extern void Start(IntPtr depthApiHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Stop")]
            public static extern void Stop(IntPtr depthApiHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Configure")]
            public static extern void Configure(IntPtr depthApiHandle, byte mode, uint frameRate);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Destruct")]
            public static extern void Destruct(IntPtr depthApiHandle);

            /// Returns the ExternalHandle to the memoryBuffer
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_GetEnvironmentDepth")]
            public static extern IntPtr GetEnvironmentDepth
            (
                IntPtr depthApiHandle,
                out IntPtr memoryBuffer,
                out int size,
                out int width,
                out int height,
                out TextureFormat format,
                out uint frameId,
                out ulong frameTimestamp
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Warp")]
            public static extern IntPtr Warp
            (
                IntPtr depthApiHandle,
                IntPtr depthResourceHandle,
                float[] poseMatrix,
                int targetWidth,
                int targetHeight,
                float backProjectionPlane,
                out IntPtr memoryBuffer,
                out int size
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_Blit")]
            public static extern IntPtr Blit
            (
                IntPtr depthApiHandle,
                IntPtr depthResourceHandle,
                int targetWidth,
                int targetHeight,
                out IntPtr memoryBuffer,
                out int size
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_TryGetLatestIntrinsics")]
            public static extern bool TryGetLatestIntrinsics(IntPtr nativeProviderHandle, float[] intrinsics);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_ReleaseResource")]
            public static extern void DisposeResource(IntPtr depthApiHandle, IntPtr resourceHandle);
        }
    }
}
