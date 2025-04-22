// Copyright 2022-2025 Niantic.

using System;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Simulation
{
    internal class LightshipSimulationDepthTextureProvider : LightshipSimulationTextureProvider
    {
        // Shader properties
        private static readonly int s_environmentDepthTextureId = Shader.PropertyToID("_EnvironmentDepth");
        private static readonly int s_zBufferParamsZ = Shader.PropertyToID("_ZBufferParams_Z");
        private static readonly int s_zBufferParamsW = Shader.PropertyToID("_ZBufferParams_W");

        // Resources
        private RenderTexture _intermediateRenderTarget;
        private Material _material;
        private Texture2D _confidenceTexture;
        private IntPtr _confidencePointer;

        protected override int PropertyNameId => s_environmentDepthTextureId;

        protected override void OnConfigureCamera(Camera renderCamera)
        {
            renderCamera.depthTextureMode = DepthTextureMode.Depth;
            renderCamera.clearFlags = CameraClearFlags.Depth;

            // The simulated camera will render to this intermediate render target
            renderCamera.targetTexture = _intermediateRenderTarget;
        }

        protected override bool OnAllocateResources(out RenderTexture targetTexture, out Texture2D outputTexture)
        {
            // The intermediate render target is used to capture non-linear depth from the Unity camera
            // After rendering, we will blit this texture to the actual target texture, converting it to linear depth
            _intermediateRenderTarget = new RenderTexture
            (
                ImageWidth,
                ImageHeight,
                16,
                RenderTextureFormat.Depth
            ) {name = "Depth camera sensor"};

            if (!_intermediateRenderTarget.Create())
            {
                targetTexture = null;
                outputTexture = null;
                return false;
            }

            // The shader that converts non-linear depth to linear metric depth (RFloat32)
            var shader = Shader.Find("Custom/LightshipSimulationDepthShader");
            if (shader == null)
            {
                targetTexture = null;
                outputTexture = null;
                return false;
            }
            _material = new Material(shader);

            targetTexture = new RenderTexture
            (
                _intermediateRenderTarget.width,
                _intermediateRenderTarget.height,
                0,
                RenderTextureFormat.RFloat
            );

            if (!targetTexture.Create())
            {
                outputTexture = null;
                return false;
            }

            outputTexture = new Texture2D(_intermediateRenderTarget.width, _intermediateRenderTarget.height, TextureFormat.RFloat, false)
            {
                name = "Simulated Depth Texture", hideFlags = HideFlags.HideAndDontSave
            };

            // Confidence Texture is all white (all ones, perfect confidence)
            _confidenceTexture = new Texture2D(_intermediateRenderTarget.width, _intermediateRenderTarget.height, TextureFormat.RFloat, false);
            var pixels = Enumerable.Repeat(Color.white, _confidenceTexture.width * _confidenceTexture.height).ToArray();
            _confidenceTexture.SetPixels(pixels);
            _confidenceTexture.Apply();
            _confidencePointer = _confidenceTexture.GetNativeTexturePtr();

            return true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_intermediateRenderTarget != null)
            {
                _intermediateRenderTarget.Release();
            }

            if (_confidenceTexture != null)
            {
                Destroy(_confidenceTexture);
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }

        protected override void OnPostRenderCamera(Camera renderCamera, RenderTexture targetTexture)
        {
            // We set ZBufferParams from the depth camera
            var farDividedByNear = renderCamera.farClipPlane / renderCamera.nearClipPlane;
            _material.SetFloat(s_zBufferParamsZ, (-1 + farDividedByNear) / renderCamera.farClipPlane);
            _material.SetFloat(s_zBufferParamsW, 1 / renderCamera.farClipPlane);

            // Here we convert the non-linear depth texture to linear eye depth
            Graphics.Blit(_intermediateRenderTarget, targetTexture, _material);
            base.OnPostRenderCamera(renderCamera, targetTexture);
        }

        /// <summary>
        /// Tries to acquire the depth confidence image on GPU memory via a XRTextureDescriptor.
        /// </summary>
        /// <param name="depthConfidenceDescriptor">The XRTextureDescriptor for the depth confidence image.</param>
        /// <returns>True if the depth confidence image is successfully acquired, false otherwise.</returns>
        internal bool TryGetConfidenceTextureDescriptor(out XRTextureDescriptor depthConfidenceDescriptor)
        {
            if (_confidencePointer == IntPtr.Zero)
            {
                depthConfidenceDescriptor = default;
                return false;
            }

            depthConfidenceDescriptor = new XRTextureDescriptor(_confidencePointer, _confidenceTexture.width,
                _confidenceTexture.height,
                _confidenceTexture.mipmapCount, _confidenceTexture.format, s_environmentDepthTextureId, 0,
                TextureDimension.Tex2D);

            return true;
        }

        /// <summary>
        /// Tries to acquire the depth confidence image data on CPU memory.
        /// </summary>
        /// <param name="data">The depth confidence image data on cpu memory.</param>
        /// <param name="dimensions">The dimensions of the depth confidence image.</param>
        /// <param name="format">The format of the depth confidence image.</param>
        /// <returns>True if the depth confidence image data is successfully acquired, false otherwise.</returns>
        internal bool TryGetConfidenceCpuData(out NativeArray<byte> data, out Vector2Int dimensions,
            out TextureFormat format)
        {
            if (_confidencePointer == IntPtr.Zero)
            {
                data = default;
                dimensions = default;
                format = default;
                return false;
            }

            data = _confidenceTexture.GetPixelData<byte>(0);
            dimensions = new Vector2Int(_confidenceTexture.width, _confidenceTexture.height);
            format = _confidenceTexture.format;
            return data.IsCreated;
        }
    }
}
