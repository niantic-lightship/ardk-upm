// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

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

        public Matrix4x4 AcquireSamplerMatrix
        (
            IntPtr nativeProviderHandle,
            IntPtr resourceHandle,
            XRCameraParams? cameraParams,
            Matrix4x4? currentPose,
            int imageWidth,
            int imageHeight
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

        public bool TryGetLatestIntrinsicsMatrix(IntPtr nativeProviderHandle, out Matrix4x4 intrinsicsMatrix);

        public bool TryGetLatestExtrinsicsMatrix(IntPtr nativeProviderHandle, out Matrix4x4 extrinsicsMatrix);

        public bool TryGetLatestEnvironmentDepthResolution(IntPtr nativeProviderHandle, out Vector2Int resolution);

        public void DisposeResource(IntPtr nativeProviderHandle, IntPtr resourceHandle);
    }
}
