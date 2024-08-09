using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// A utility class to generate a point cloud from a depth texture.
    /// </summary>
    public sealed class LightshipPointCloudGenerator : IDisposable
    {
        #region Shader Handles

        private const string ShaderName  = "DepthToPointCloud";
        private const string KernelName = "CSMain";

        private static readonly int s_textureHandle = Shader.PropertyToID("depthTexture");
        private static readonly int s_textureWidth = Shader.PropertyToID("textureWidth");
        private static readonly int s_textureHeight = Shader.PropertyToID("textureHeight");
        private static readonly int s_intrinsics = Shader.PropertyToID("intrinsics");
        private static readonly int s_extrinsics = Shader.PropertyToID("extrinsics");
        private static readonly int s_points = Shader.PropertyToID("worldPoints");

        #endregion

        // Resources
        private readonly ComputeShader _compute;
        private ComputeBuffer _worldPoints;

        private readonly int _kernel;
        private readonly uint _kernelThreadsX;
        private readonly uint _kernelThreadsY;

        public LightshipPointCloudGenerator()
        {
            // Load the compute shader
            _compute = Resources.Load<ComputeShader>(ShaderName);
            if (_compute == null)
            {
                throw new InvalidOperationException
                (
                    $"Could not find shader named '{ShaderName}' required for depth processing."
                );
            }

            // Load the kernel
            _kernel = _compute.FindKernel(KernelName);
            _compute.GetKernelThreadGroupSizes(_kernel, out _kernelThreadsX, out _kernelThreadsY, out _);
        }

        /// <summary>
        /// Generate a point cloud from a depth texture using the given intrinsics and extrinsics.
        /// </summary>
        /// <param name="depth">The depth image.</param>
        /// <param name="intrinsics">The camera intrinsic parameters for the depth image.</param>
        /// <param name="extrinsics">The camera to world transform.</param>
        /// <returns>A collection of 3D points generated from the image.</returns>
        /// <remarks>This is an allocating, blocking call.</remarks>
        public Vector4[] Generate(Texture depth, XRCameraIntrinsics intrinsics, Matrix4x4 extrinsics)
        {
            var result = new Vector4[depth.width * depth.height];
            Generate(depth, XRCameraIntrinsicsToMatrix4X4(intrinsics), extrinsics, ref result);
            return result;
        }

        /// <summary>
        /// Generate a point cloud from a depth texture using the given intrinsics and extrinsics.
        /// </summary>
        /// <param name="depth">The depth image.</param>
        /// <param name="intrinsics">The camera intrinsic parameters for the depth image.</param>
        /// <param name="extrinsics">The camera to world transform.</param>
        /// <returns>A collection of 3D points generated from the image.</returns>
        /// <remarks>This is an allocating, blocking call.</remarks>
        public Vector4[] Generate(Texture depth, Matrix4x4 intrinsics, Matrix4x4 extrinsics)
        {
            var result = new Vector4[depth.width * depth.height];
            Generate(depth, intrinsics, extrinsics, ref result);
            return result;
        }

        /// <summary>
        /// Generate a point cloud from a depth texture using the given intrinsics and extrinsics.
        /// </summary>
        /// <param name="depth">The depth image.</param>
        /// <param name="intrinsics">The camera intrinsic parameters for the depth image.</param>
        /// <param name="extrinsics">The camera to world transform.</param>
        /// <param name="result">The pre-allocated container for the resulting 3D points.
        /// The array length be the number of pixels in the depth image.</param>
        /// <remarks>This is a non-allocating, blocking call.</remarks>
        public void Generate(Texture depth, XRCameraIntrinsics intrinsics, Matrix4x4 extrinsics, ref Vector4[] result)
        {
            Generate(depth, XRCameraIntrinsicsToMatrix4X4(intrinsics), extrinsics, ref result);
        }

        /// <summary>
        /// Generate a point cloud from a depth texture using the given intrinsics and extrinsics.
        /// </summary>
        /// <param name="depth">The depth image.</param>
        /// <param name="intrinsics">The camera intrinsic parameters for the depth image.</param>
        /// <param name="extrinsics">The camera to world transform.</param>
        /// <param name="result">The pre-allocated container for the resulting 3D points.
        /// The array length be the number of pixels in the depth image.</param>
        /// <remarks>This is a non-allocating, blocking call.</remarks>
        public void Generate(Texture depth, Matrix4x4 intrinsics, Matrix4x4 extrinsics, ref Vector4[] result)
        {
            // Inspect the texture
            var width = depth.width;
            var height = depth.height;
            var length = width * height;

            // Resize the result array if necessary
            if (result == null || result.Length != length)
            {
                result = new Vector4[length];
            }

            // Bind the texture
            _compute.SetInt(s_textureWidth, width);
            _compute.SetInt(s_textureHeight, height);
            _compute.SetTexture(_kernel, s_textureHandle, depth);

            // Bind matrices
            _compute.SetMatrix(s_intrinsics, intrinsics);
            _compute.SetMatrix(s_extrinsics, extrinsics);

            // Set output
            if (_worldPoints != null && _worldPoints.count != length)
            {
                _worldPoints.Dispose();
                _worldPoints = null;
            }

            _worldPoints ??= new ComputeBuffer(length, sizeof(float) * 4);
            _compute.SetBuffer(_kernel, s_points, _worldPoints);

            int threadGroupX = Mathf.CeilToInt(width / (float) _kernelThreadsX);
            int threadGroupY = Mathf.CeilToInt(height / (float) _kernelThreadsY);
            int threadGroupZ = 1;

            _compute.Dispatch(_kernel, threadGroupX, threadGroupY, threadGroupZ);
            _worldPoints.GetData(result);
        }

        /// <summary>
        /// A coroutine to generate a point cloud from a depth texture using the given intrinsics and extrinsics.
        /// </summary>
        /// <param name="depth">The depth image.</param>
        /// <param name="intrinsics">The camera intrinsic parameters for the depth image.</param>
        /// <param name="extrinsics">The camera to world transform.</param>
        /// <param name="onComplete">Callback for when the task completes.</param>
        /// <remarks>This is an allocating, non-blocking call.</remarks>
        public IEnumerator GenerateAsync(Texture depth, Matrix4x4 intrinsics, Matrix4x4 extrinsics, Action<Vector4[]> onComplete)
        {
            // Inspect the texture
            var width = depth.width;
            var height = depth.height;
            var length = width * height;

            // Bind the texture
            _compute.SetInt(s_textureWidth, width);
            _compute.SetInt(s_textureHeight, height);
            _compute.SetTexture(_kernel, s_textureHandle, depth);

            // Bind matrices
            _compute.SetMatrix(s_intrinsics, intrinsics);
            _compute.SetMatrix(s_extrinsics, extrinsics);

            // Set output
            var points = new ComputeBuffer(length, sizeof(float) * 4);
            _compute.SetBuffer(_kernel, s_points, points);

            int threadGroupX = Mathf.CeilToInt(width / (float) _kernelThreadsX);
            int threadGroupY = Mathf.CeilToInt(height / (float) _kernelThreadsY);
            int threadGroupZ = 1;

            _compute.Dispatch(_kernel, threadGroupX, threadGroupY, threadGroupZ);
            var request = AsyncGPUReadback.Request(points);
            while (!request.done)
            {
                yield return null;
            }

            onComplete?.Invoke(request.GetData<Vector4>().ToArray());
            points.Dispose();
        }

        public void Dispose()
        {
            _worldPoints?.Dispose();
        }

        private static Matrix4x4 XRCameraIntrinsicsToMatrix4X4(XRCameraIntrinsics intrinsics)
        {
            var result = Matrix4x4.identity;
            result[0, 0] = intrinsics.focalLength.x;
            result[1, 1] = intrinsics.focalLength.y;
            result[0, 2] = intrinsics.principalPoint.x;
            result[1, 2] = intrinsics.principalPoint.y;
            return result;
        }
    }
}
