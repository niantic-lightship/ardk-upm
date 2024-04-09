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
        private const string k_TextureEnvironmentDepthPropertyName = "_EnvironmentDepth";
        private static readonly int k_TextureEnvironmentDepthPropertyId =
            Shader.PropertyToID(k_TextureEnvironmentDepthPropertyName);

        // Resources
        private RenderTexture _depthRT;
        private Material _material;
        private static readonly int _zBufferParamsZ = Shader.PropertyToID("_ZBufferParams_Z");
        private static readonly int _zBufferParamsW = Shader.PropertyToID("_ZBufferParams_W");

        private Texture2D _confidenceTexture;
        private IntPtr _confidencePointer;

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
            m_XrCamera = xrCamera;
            // The helper depth camera
            m_SimulationRenderCamera = simulationCamera;

            // The shader that converts depth to metric depth (RFloat32)
            var shader = Shader.Find("Custom/LightshipSimulationDepthShader");
            _material = new Material(shader);

            // Depth texture
            // we invert x and y because the camera is always physically installed at landscape left and we rotate the camera -90 for portrait
            _depthRT = new RenderTexture
            (
                (int) m_SimulationRenderCamera.sensorSize.y,
                (int) m_SimulationRenderCamera.sensorSize.x,
                16,
                RenderTextureFormat.Depth
            );
            _depthRT.name = "Depth camera sensor";
            _depthRT.Create();

            // RFloat32 depth texture
            m_RenderTexture = new RenderTexture
            (
                _depthRT.width,
                _depthRT.height,
                0,
                RenderTextureFormat.RFloat
            );
            m_RenderTexture.Create();

            // Confidence Texture is all white (all ones, perfect confidence)
            _confidenceTexture = new Texture2D(_depthRT.width, _depthRT.height);
            Color[] pixels = Enumerable.Repeat(Color.white, _confidenceTexture.width * _confidenceTexture.height).ToArray();
            _confidenceTexture.SetPixels(pixels);
            _confidenceTexture.Apply();
            _confidencePointer = _confidenceTexture.GetNativeTexturePtr();

            m_SimulationRenderCamera.depthTextureMode = DepthTextureMode.Depth;
            m_SimulationRenderCamera.clearFlags = CameraClearFlags.Depth;
            m_SimulationRenderCamera.nearClipPlane = 0.1f;
            m_SimulationRenderCamera.targetTexture = _depthRT;

            // We set ZBufferParams from the depth camera
            var farDividedByNear = m_SimulationRenderCamera.farClipPlane / m_SimulationRenderCamera.nearClipPlane;
            _material.SetFloat(_zBufferParamsZ, (-1 + farDividedByNear) / m_SimulationRenderCamera.farClipPlane);
            _material.SetFloat(_zBufferParamsW, 1 / m_SimulationRenderCamera.farClipPlane);

            if (m_ProviderTexture == null)
            {
                m_ProviderTexture = new Texture2D(_depthRT.width, _depthRT.height, TextureFormat.RFloat, false)
                {
                    name = "Simulated Native Camera Texture",
                    hideFlags = HideFlags.HideAndDontSave
                };
                m_TexturePtr = m_ProviderTexture.GetNativeTexturePtr();
            }

            _initialized = true;
        }

        internal override bool TryGetTextureDescriptors(out NativeArray<XRTextureDescriptor> planeDescriptors,
            Allocator allocator)
        {
            var isValid = TryGetLatestImagePtr(out var nativePtr);

            var descriptors = new XRTextureDescriptor[1];
            if (isValid)
            {
                TryGetTextureDescriptor(out var planeDescriptor);
                descriptors[0] = planeDescriptor;
            }
            else
                descriptors[0] = default;

            planeDescriptors = new NativeArray<XRTextureDescriptor>(descriptors, allocator);
            return isValid;
        }

        internal void TryGetTextureDescriptor(out XRTextureDescriptor planeDescriptor)
        {
            var isValid = TryGetLatestImagePtr(out var nativePtr);

            planeDescriptor = new XRTextureDescriptor(nativePtr, m_ProviderTexture.width, m_ProviderTexture.height,
                m_ProviderTexture.mipmapCount, m_ProviderTexture.format, k_TextureEnvironmentDepthPropertyId, 0,
                TextureDimension.Tex2D);
        }

        internal void TryGetConfidenceTextureDescriptor(out XRTextureDescriptor depthConfidenceDescriptor)
        {
            depthConfidenceDescriptor = new XRTextureDescriptor(_confidencePointer, _confidenceTexture.width, _confidenceTexture.height,
                _confidenceTexture.mipmapCount, _confidenceTexture.format, k_TextureEnvironmentDepthPropertyId, 0,
                TextureDimension.Tex2D);
        }

        // Postprocess the image
        private void OnRenderImage (RenderTexture source, RenderTexture destination)
        {
            // The first pass blits into a dedicated depth texture (16 bit depth channel)
            Graphics.Blit (source, destination);

            // Here we convert the depth texture to RFloat32
            Graphics.Blit (_depthRT, m_RenderTexture, _material);
        }
    }
}
