// Copyright 2022-2024 Niantic.
using UnityEngine;
using UnityEngine.Rendering;
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
        // The render pass to perform after the background rendering
        private MainPass _mainPass;

        // The render pass to grab the depth map of the fused mesh
        private FusedDepthPass _fusedDepthRenderPass;

        public override void Create()
        {
            // Allocate render passes
            _mainPass = new MainPass();
            _fusedDepthRenderPass = new FusedDepthPass();
        }

        // Invoked when it is time to schedule the render pass
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var currentCamera = renderingData.cameraData.camera;
            if (currentCamera == null || currentCamera.cameraType != CameraType.Game)
            {
                return;
            }

            // Look for lightship components on the camera
            var occlusionExt = currentCamera.gameObject.GetComponent<LightshipOcclusionExtension>();
            var fusedDepthCamera = currentCamera.gameObject.GetComponent<LightshipFusedDepthCamera>();

            // Set up the occlusion extension main pass
            if (occlusionExt != null && occlusionExt.enabled && occlusionExt.IsRenderingActive)
            {
                var cameraBackground = currentCamera.gameObject.GetComponent<ARCameraBackground>();
                if (cameraBackground == null || !cameraBackground.enabled || !cameraBackground.backgroundRenderingEnabled)
                    return;

                if (!_mainPass.TrySetRenderingMode(cameraBackground.currentRenderingMode))
                    return;

                var cameraManager = cameraBackground.GetComponent<ARCameraManager>();
                if (cameraManager != null || cameraManager.subsystem != null)
                {
                    _mainPass.InvertCulling = cameraManager.subsystem.invertCulling;
                }

                _mainPass.Configure(occlusionExt.BackgroundMaterial, renderer.cameraColorTarget);
                renderer.EnqueuePass(_mainPass);
            }

            // Set up the fused depth render pass
            else if (fusedDepthCamera != null && fusedDepthCamera.enabled)
            {
                _fusedDepthRenderPass.Configure(fusedDepthCamera.Material, fusedDepthCamera.GpuTexture);
                renderer.EnqueuePass(_fusedDepthRenderPass);
            }
        }

        private sealed class MainPass : FullScreenBlitPass
        {
            public MainPass()
                : base("Lightship Occlusion Extension (Main)", RenderPassEvent.BeforeRenderingOpaques) { }

            public override void Configure(Material material, RenderTargetIdentifier target)
            {
                base.Configure(material, target);
                ConfigureClear(ClearFlag.None, Color.clear);
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

        private sealed class FusedDepthPass : FullScreenBlitPass
        {
            public FusedDepthPass()
                : base("Lightship Occlusion Extension (Fused Depth)", RenderPassEvent.AfterRendering) { }

            public override void Configure(Material material, RenderTargetIdentifier target)
            {
                base.Configure(material, target);
                ConfigureClear(ClearFlag.None, Color.clear);
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
        }
#endif // MODULE_URP_ENABLED
    }
}
