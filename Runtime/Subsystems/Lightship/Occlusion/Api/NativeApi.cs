// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Niantic.Lightship.AR.OcclusionSubsystem
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
            out uint frameId
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
                    out frameId
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

        public IntPtr DisposeResource(IntPtr nativeProviderHandle, IntPtr resourceHandle)
        {
            return Native.DisposeResource(nativeProviderHandle, resourceHandle);
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

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_GetEnvironmentDepth")]
            public static extern IntPtr GetEnvironmentDepth
            (
                IntPtr depthApiHandle,
                out IntPtr memoryBuffer,
                out int size,
                out int width,
                out int height,
                out TextureFormat format,
                out uint frameId
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

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_ReleaseResource")]
            public static extern IntPtr DisposeResource(IntPtr depthApiHandle, IntPtr resourceHandle);
        }
    }
}
