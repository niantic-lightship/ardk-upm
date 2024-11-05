// Copyright 2022-2024 Niantic.
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

#if MODULE_URP_ENABLED
using UnityEngine.Rendering.Universal;
#else
using ScriptableRendererFeature = UnityEngine.ScriptableObject;
#endif

namespace Niantic.Lightship.AR.Occlusion
{
    public class LightshipOcclusionExtensionFeature : ScriptableRendererFeature
    {
#if MODULE_URP_ENABLED
        // The render pass to perform the main occlusion rendering
        private ZBufferPass _zBufferPass;

        public override void Create()
        {
            // Allocate render passes
            _zBufferPass = new ZBufferPass();
        }

        // Invoked when it is time to schedule the render pass
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var currentCamera = renderingData.cameraData.camera;
            if (currentCamera == null || currentCamera.cameraType != CameraType.Game)
            {
                return;
            }

            // Evaluate whether we should enqueue the render passes for the current camera
            var shouldEnqueueRenderPasses = false;
            if (currentCamera.TryGetComponent<LightshipOcclusionExtension>(out var occlusionExtension))
            {
                shouldEnqueueRenderPasses = occlusionExtension.enabled && occlusionExtension.IsRenderingActive;
            }

            // Skip this camera
            if (!shouldEnqueueRenderPasses)
            {
                return;
            }

            if (currentCamera.TryGetComponent<ARCameraBackground>(out var cameraBackground))
            {
                // Skip the camera if the ARCameraBackground is disabled or background rendering is disabled
                if (!cameraBackground.enabled || !cameraBackground.backgroundRenderingEnabled)
                {
                    return;
                }

                // Check if rendering mode is supported
                if (!_zBufferPass.TrySetRenderingMode(cameraBackground.currentRenderingMode))
                {
                    return;
                }

                // Invert culling if necessary
                _zBufferPass.InvertCulling =
                    cameraBackground.GetComponent<ARCameraManager>()?.subsystem?.invertCulling ?? false;
            }
            else
            {
                // Configure the z-buffer pass for the default rendering mode
                _zBufferPass.TrySetRenderingMode(XRCameraBackgroundRenderingMode.BeforeOpaques);
                _zBufferPass.InvertCulling = false;
            }

            // Enqueue the z-buffer pass
            if (occlusionExtension.Material != null)
            {
                _zBufferPass.SetMaterial(occlusionExtension.Material);
                renderer.EnqueuePass(_zBufferPass);
            }
        }

        /// <summary>
        /// A render pass that mimics CommandBuffer.Blit in the legacy pipeline.
        /// We use it to write the z-buffer of the frame.
        /// </summary>
        private sealed class ZBufferPass : FullScreenBlitPass
        {
            public ZBufferPass()
                : base("Lightship Occlusion Extension (ZBuffer)", RenderPassEvent.BeforeRenderingOpaques)
            {
            }

            public bool TrySetRenderingMode(XRCameraBackgroundRenderingMode renderingMode)
            {
                switch (renderingMode)
                {
                    case XRCameraBackgroundRenderingMode.BeforeOpaques:
                        renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
                        return true;
                    case XRCameraBackgroundRenderingMode.AfterOpaques:
                        renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                        return true;
                    default:
                        return false;
                }
            }
        }

#endif // MODULE_URP_ENABLED
    }
}
