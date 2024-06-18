// Copyright 2022-2024 Niantic.

using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Simulation
{
    internal class LightshipSimulationRgbCameraTextureProvider : LightshipSimulationTextureProvider
    {
        internal static LightshipSimulationRgbCameraTextureProvider AddTextureProviderToCamera(Camera simulationCamera, Camera xrCamera)
        {
            var cameraTextureProvider = simulationCamera.gameObject.AddComponent<LightshipSimulationRgbCameraTextureProvider>();
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

            // in simulation rgb camera we want to see a skybox
            simulationCamera.clearFlags = CameraClearFlags.Skybox;

            // we invert x and y because the camera is always physically installed at landscape left and we rotate the camera -90 for portrait
            var descriptor = new RenderTextureDescriptor((int) SimulationRenderCamera.sensorSize.y, (int) SimulationRenderCamera.sensorSize.x);

            // Need to make sure we set the graphics format to our valid format
            // or we will get an out of range value for the render texture format
            // when we try creating the render texture
            descriptor.graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            // Need to enable depth buffer if the target camera did not already have it.
            if (descriptor.depthBufferBits < 24)
                descriptor.depthBufferBits = 24;

            RenderTexture = new RenderTexture(descriptor)
            {
                name = "RGB camera sensor",
                hideFlags = HideFlags.HideAndDontSave,
            };

            if (RenderTexture.Create())
                SimulationRenderCamera.targetTexture = RenderTexture;

            if (ProviderTexture == null)
            {
                ProviderTexture = new Texture2D(descriptor.width, descriptor.height, descriptor.graphicsFormat, 1, TextureCreationFlags.None)
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
            if (isValid)
            {
                descriptors[0] = new XRTextureDescriptor(nativePtr, ProviderTexture.width, ProviderTexture.height,
                ProviderTexture.mipmapCount, ProviderTexture.format, LightshipSimulationCameraSubsystem.s_textureSinglePropertyNameId, 0,
                TextureDimension.Tex2D);
            }
            else
                descriptors[0] = default;

            planeDescriptors = new NativeArray<XRTextureDescriptor>(descriptors, allocator);
            return isValid;
        }
    }
}
