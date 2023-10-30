// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems.Occlusion
{
    internal interface IApi
    {
        IntPtr Construct(IntPtr unityContext);
        void Start(IntPtr nativeProviderHandle);
        void Stop(IntPtr nativeProviderHandle);
        void Configure(IntPtr nativeProviderHandle, byte mode, uint frameRate);
        void Destruct(IntPtr nativeProviderHandle);

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
        );

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
        );

        public IntPtr Blit
        (
            IntPtr nativeProviderHandle,
            IntPtr depthResourceHandle,
            int targetWidth,
            int targetHeight,
            out IntPtr memoryBuffer,
            out int size
        );

        public IntPtr DisposeResource(IntPtr nativeProviderHandle, IntPtr resourceHandle);
    }
}
