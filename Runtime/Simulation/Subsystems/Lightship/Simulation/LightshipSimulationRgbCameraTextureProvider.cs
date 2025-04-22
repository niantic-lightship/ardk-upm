// Copyright 2022-2025 Niantic.

using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Niantic.Lightship.AR.Simulation
{
    internal class LightshipSimulationRgbCameraTextureProvider : LightshipSimulationTextureProvider
    {
        protected override int PropertyNameId => LightshipSimulationCameraSubsystem.s_textureSinglePropertyNameId;
        private RenderTexture _renderTarget;

        protected override void OnConfigureCamera(Camera renderCamera)
        {
            renderCamera.clearFlags = CameraClearFlags.Skybox;

            // Set the target texture to the camera
            renderCamera.targetTexture = _renderTarget;
        }

        protected override bool OnAllocateResources(out RenderTexture targetTexture,
            out Texture2D outputTexture)
        {
            var descriptor = new RenderTextureDescriptor(ImageWidth, ImageHeight)
            {
                // Need to make sure we set the graphics format to our valid format,
                // or we will get an out of range value for the render texture format
                // when we try creating the render texture
                graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR)
            };

            // Need to enable depth buffer if the target camera did not already have it.
            if (descriptor.depthBufferBits < 24)
            {
                descriptor.depthBufferBits = 24;
            }

            targetTexture = new RenderTexture(descriptor)
            {
                name = "RGB camera sensor", hideFlags = HideFlags.HideAndDontSave,
            };

            if (!targetTexture.Create())
            {
                outputTexture = null;
                return false;
            }
            _renderTarget = targetTexture;

            outputTexture = new Texture2D(descriptor.width, descriptor.height, descriptor.graphicsFormat, 1,
                TextureCreationFlags.None)
            {
                name = "Simulated Native Camera Texture", hideFlags = HideFlags.HideAndDontSave
            };

            return true;
        }
    }
}
