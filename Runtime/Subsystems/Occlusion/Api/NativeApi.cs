// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Occlusion;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

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

        public bool TryGetLatestExtrinsicsMatrix(IntPtr nativeProviderHandle, out Matrix4x4 extrinsicsMatrix)
        {
            float[] extrinsics = new float[16];
            bool gotIntrinsics = Native.TryGetLatestExtrinsics(nativeProviderHandle, extrinsics);

            if (!gotIntrinsics)
            {
                extrinsicsMatrix = default;
                return false;
            }

            extrinsicsMatrix = extrinsics.FromColumnMajorArray().FromArdkToUnity();
            return true;
        }

        public bool TryGetLatestEnvironmentDepthResolution(IntPtr nativeProviderHandle, out Vector2Int resolution)
        {
            bool gotResolution = Native.TryGetLatestEnvironmentDepthResolution(nativeProviderHandle, out int width, out int height);

            if (!gotResolution)
            {
                resolution = default;
                return false;
            }

            resolution = new Vector2Int(width, height);
            return true;
        }

        /// <summary>
        /// Returns a 3x3 transformation matrix for converting between
        /// normalized image coordinates and a coordinate space appropriate
        /// for rendering the image onscreen.
        /// </summary>
        /// <param name="nativeProviderHandle">The handle to the native provider.</param>
        /// <param name="resourceHandle">The handle to the native image buffer resource.</param>
        /// <param name="cameraParams">Describes the viewport.</param>
        /// <param name="imageWidth">The width of the image container.</param>
        /// <param name="imageHeight">The height of the image container.</param>
        /// <returns>The transformation used to display the image on the viewport.</returns>
        public Matrix4x4 AcquireSamplerMatrix
        (
            IntPtr nativeProviderHandle,
            IntPtr resourceHandle,
            XRCameraParams? cameraParams,
            Matrix4x4? currentPose,
            int imageWidth,
            int imageHeight
        )
        {
            // Bypass the transform until the viewport becomes available
            if (!cameraParams.HasValue)
            {
                return Matrix4x4.identity;
            }

            // Extract the viewport
            var viewport = cameraParams.Value;

            Matrix4x4 result;
            if (currentPose.HasValue)
            {
                // If the pose is available, calculate a viewport mapping with warping included
                TryCalculateSamplerMatrix
                (
                    nativeProviderHandle,
                    resourceHandle,
                    viewport,
                    currentPose.Value,
                    XRDisplayContext.OccludeeEyeDepth,
                    out result
                );
            }
            else
            {
                // If the pose is unavailable, calculate only a viewport mapping
                result = CameraMath.CalculateDisplayMatrix
                (
                    imageWidth,
                    imageHeight,
                    (int)viewport.screenWidth,
                    (int)viewport.screenHeight,
                    viewport.screenOrientation,
                    invertVertically: true
                );
            }

            return result;
        }

        /// <summary>
        /// Calculates a 3x3 transformation matrix that when applied to the image,
        /// aligns its pixels such that the image was taken from the specified pose.
        /// </summary>
        /// <param name="nativeProviderHandle">The handle to the occlusion native API </param>
        /// <param name="resourceHandle">The handle to the semantics buffer resource.</param>
        /// <param name="cameraParams">Describes the viewport.</param>
        /// <param name="pose">The camera pose the image needs to align with.</param>
        /// <param name="backProjectionPlane">The distance from the camera to the plane that
        /// the image should be projected onto (in meters).</param>
        /// <param name="result"></param>
        /// <returns>True, if the matrix could be calculated, otherwise false (in case the </returns>
        private bool TryCalculateSamplerMatrix
        (
            IntPtr nativeProviderHandle,
            IntPtr resourceHandle,
            XRCameraParams cameraParams,
            Matrix4x4 pose,
            float backProjectionPlane,
            out Matrix4x4 result
        )
        {
            var outMatrix = new float[9];
            var poseArray = MatrixConversionHelper.Matrix4x4ToInternalArray(pose.FromUnityToArdk());

            var gotMatrix =
                Native.CalculateSamplerMatrix
                (
                    nativeProviderHandle,
                    resourceHandle,
                    (int)cameraParams.screenWidth,
                    (int)cameraParams.screenHeight,
                    cameraParams.screenOrientation.FromUnityToArdk(),
                    poseArray,
                    backProjectionPlane,
                    outMatrix
                );

            if (gotMatrix)
            {
                result = new Matrix4x4
                (
                    new Vector4(outMatrix[0], outMatrix[1], outMatrix[2], 0),
                    new Vector4(outMatrix[3], outMatrix[4], outMatrix[5], 0),
                    new Vector4(outMatrix[6], outMatrix[7], outMatrix[8], 0),
                    new Vector4(0, 0, 0, 1)
                );

                return true;
            }

            Log.Warning("Interpolation matrix for depth prediction could not be calculated.");
            result = Matrix4x4.identity;
            return false;
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

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_TryGetLatestExtrinsics")]
            public static extern bool TryGetLatestExtrinsics(IntPtr nativeProviderHandle, float[] extrinsics);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_TryGetLatestEnvironmentDepthResolution")]
            public static extern bool TryGetLatestEnvironmentDepthResolution(IntPtr nativeProviderHandle, out int width, out int height);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_CalculateSamplerMatrix")]
            public static extern bool CalculateSamplerMatrix
            (
                IntPtr nativeProviderHandle,
                IntPtr nativeResourceHandle,
                int viewportWidth,
                int viewportHeight,
                uint orientation,
                float[] poseMatrix,
                float backProjectionPlane,
                float[] outMatrix3X3
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_OcclusionProvider_ReleaseResource")]
            public static extern void DisposeResource(IntPtr depthApiHandle, IntPtr resourceHandle);
        }
    }
}
