// Copyright 2022-2024 Niantic.

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
        private const string TextureEnvironmentDepthPropertyName = "_EnvironmentDepth";
        private static readonly int s_textureEnvironmentDepthPropertyId =
            Shader.PropertyToID(TextureEnvironmentDepthPropertyName);

        // Resources
        private RenderTexture _depthRT;
        private Material _material;
        private static readonly int s_zBufferParamsZ = Shader.PropertyToID("_ZBufferParams_Z");
        private static readonly int s_zBufferParamsW = Shader.PropertyToID("_ZBufferParams_W");

        private Texture2D _confidenceTexture;
        private IntPtr _confidencePointer;

        protected override void OnEnable()
        {
            PostRenderCamera += OnPostRenderCamera;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            PostRenderCamera -= OnPostRenderCamera;
            base.OnDisable();
        }

        internal override void OnDestroy()
        {
            if (_depthRT != null)
                _depthRT.Release();

            base.OnDestroy();
        }

        internal static LightshipSimulationDepthTextureProvider AddTextureProviderToCamera(Camera simulationCamera, Camera xrCamera)
        {
            var cameraTextureProvider = simulationCamera.gameObject.AddComponent<LightshipSimulationDepthTextureProvider>();
            cameraTextureProvider.InitializeProvider(xrCamera, simulationCamera);

            return cameraTextureProvider;
        }

        protected override void InitializeProvider(Camera xrCamera, Camera simulationCamera)
        {
            base.InitializeProvider(xrCamera, simulationCamera);

            // The background camera
            XrCamera = xrCamera;
            // The helper depth camera
            SimulationRenderCamera = simulationCamera;

            // The shader that converts depth to metric depth (RFloat32)
            var shader = Shader.Find("Custom/LightshipSimulationDepthShader");
            _material = new Material(shader);

            // Depth texture
            // we invert x and y because the camera is always physically installed at landscape left and we rotate the camera -90 for portrait
            _depthRT = new RenderTexture
            (
                (int) SimulationRenderCamera.sensorSize.y,
                (int) SimulationRenderCamera.sensorSize.x,
                16,
                RenderTextureFormat.Depth
            );
            _depthRT.name = "Depth camera sensor";
            _depthRT.Create();

            // RFloat32 depth texture
            RenderTexture = new RenderTexture
            (
                _depthRT.width,
                _depthRT.height,
                0,
                RenderTextureFormat.RFloat
            );
            RenderTexture.Create();

            // Confidence Texture is all white (all ones, perfect confidence)
            _confidenceTexture = new Texture2D(_depthRT.width, _depthRT.height, TextureFormat.RFloat, false);
            Color[] pixels = Enumerable.Repeat(Color.white, _confidenceTexture.width * _confidenceTexture.height).ToArray();
            _confidenceTexture.SetPixels(pixels);
            _confidenceTexture.Apply();
            _confidencePointer = _confidenceTexture.GetNativeTexturePtr();

            SimulationRenderCamera.depthTextureMode = DepthTextureMode.Depth;
            SimulationRenderCamera.clearFlags = CameraClearFlags.Depth;
            SimulationRenderCamera.nearClipPlane = 0.1f;
            SimulationRenderCamera.targetTexture = _depthRT;

            // We set ZBufferParams from the depth camera
            var farDividedByNear = SimulationRenderCamera.farClipPlane / SimulationRenderCamera.nearClipPlane;
            _material.SetFloat(s_zBufferParamsZ, (-1 + farDividedByNear) / SimulationRenderCamera.farClipPlane);
            _material.SetFloat(s_zBufferParamsW, 1 / SimulationRenderCamera.farClipPlane);

            if (ProviderTexture == null)
            {
                ProviderTexture = new Texture2D(_depthRT.width, _depthRT.height, TextureFormat.RFloat, false)
                {
                    name = "Simulated Native Camera Texture",
                    hideFlags = HideFlags.HideAndDontSave
                };
                TexturePtr = ProviderTexture.GetNativeTexturePtr();
            }

            Initialized = true;
        }

        internal override bool TryGetTextureDescriptors(out NativeArray<XRTextureDescriptor> planeDescriptors,
            Allocator allocator)
        {
            var isValid = TryGetLatestImagePtr(out var nativePtr);

            var descriptors = new XRTextureDescriptor[1];
            if (isValid && TryGetTextureDescriptor(out var planeDescriptor))
            {
                descriptors[0] = planeDescriptor;
            }
            else
            {
                descriptors[0] = default;
                isValid = false;
            }

            planeDescriptors = new NativeArray<XRTextureDescriptor>(descriptors, allocator);
            return isValid;
        }

        internal bool TryGetTextureDescriptor(out XRTextureDescriptor planeDescriptor)
        {
            if (!TryGetLatestImagePtr(out var nativePtr))
            {
                planeDescriptor = default;
                return false;
            }

            planeDescriptor = new XRTextureDescriptor
            (
                nativePtr,
                ProviderTexture.width,
                ProviderTexture.height,
                ProviderTexture.mipmapCount,
                ProviderTexture.format,
                s_textureEnvironmentDepthPropertyId,
                0,
                TextureDimension.Tex2D
            );

            return true;
        }

        internal void TryGetConfidenceTextureDescriptor(out XRTextureDescriptor depthConfidenceDescriptor)
        {
            depthConfidenceDescriptor = new XRTextureDescriptor(_confidencePointer, _confidenceTexture.width, _confidenceTexture.height,
                _confidenceTexture.mipmapCount, _confidenceTexture.format, s_textureEnvironmentDepthPropertyId, 0,
                TextureDimension.Tex2D);
        }

        // Postprocess the image
        private void OnPostRenderCamera(Camera _)
        {
            // Here we convert the depth texture to RFloat32
            Graphics.Blit(_depthRT, RenderTexture, _material);
        }
    }
}
