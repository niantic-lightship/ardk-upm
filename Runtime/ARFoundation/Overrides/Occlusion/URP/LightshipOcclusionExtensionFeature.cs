// Copyright 2022-2025 Niantic.
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
        private MeshRenderingPass _occlusionMeshPass;

        public override void Create()
        {
            // Allocate render passes
            _zBufferPass = new ZBufferPass();
            _occlusionMeshPass = new MeshRenderingPass(
                name: "Lightship Occlusion Extension (Occlusion Mesh)",
                renderPassEvent: RenderPassEvent.BeforeRenderingOpaques);
        }

        // Invoked when it is time to schedule the render pass
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Skip cameras that are not a game camera
            var currentCamera = renderingData.cameraData.camera;
            if (currentCamera == null || currentCamera.cameraType != CameraType.Game)
            {
                return;
            }

            // Evaluate whether we should enqueue the render passes for the current camera
            var isCameraRenderingActive = false;
            if (currentCamera.TryGetComponent<LightshipOcclusionExtension>(out var occlusionExtension))
            {
                isCameraRenderingActive = occlusionExtension.enabled && occlusionExtension.IsRenderingActive;
            }

            // Skip this camera
            if (!isCameraRenderingActive)
            {
                return;
            }

            // If the camera background is present, then it is a requirement
            var isCameraBackgroundPresent = false;
            if (currentCamera.gameObject.TryGetComponent<ARCameraBackground>(out var cameraBackground))
            {
                isCameraBackgroundPresent = true;

                // If the background is not rendering
                if (!cameraBackground.enabled || !cameraBackground.backgroundRenderingEnabled)
                {
                    // Even if it is present, the camera background is not a requirement
                    // on Magic Leap 2, because it doesn't render anything
#if UNITY_EDITOR || !NIANTIC_LIGHTSHIP_ML2_ENABLED
                    return;
#endif
                }
            }

            // Set up the occlusion extension main pass
            switch (occlusionExtension.CurrentOcclusionTechnique)
            {
                // Set up the z-buffer pass
                case LightshipOcclusionExtension.OcclusionTechnique.ZBuffer:
                {
                    // Reset the z-buffer pass to its default state
                    _zBufferPass.Reset();

                    if (isCameraBackgroundPresent)
                    {
                        // If the rendering mode is not supported
                        if (!_zBufferPass.TrySetRenderingMode(cameraBackground.currentRenderingMode))
                        {
                            break;
                        }

                        // Invert culling?
                        if (cameraBackground.TryGetComponent<ARCameraManager>(out var cameraManager))
                        {
                            _zBufferPass.InvertCulling = cameraManager.subsystem?.invertCulling ?? false;
                        }
                    }

                    if (occlusionExtension.Material != null)
                    {
                        _zBufferPass.SetMaterial(occlusionExtension.Material);
                        renderer.EnqueuePass(_zBufferPass);
                    }

                    break;
                }

                // Set up the occlusion mesh pass
                case LightshipOcclusionExtension.OcclusionTechnique.OcclusionMesh:
                {
                    if (occlusionExtension.Material != null &&
                        occlusionExtension.OccluderMesh != null)
                    {
                        _occlusionMeshPass.SetMaterial(occlusionExtension.Material);
                        _occlusionMeshPass.SetMesh(occlusionExtension.OccluderMesh);
                        renderer.EnqueuePass(_occlusionMeshPass);
                    }

                    break;
                }
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

            /// <summary>
            /// Reset the pass to its default state.
            /// </summary>
            public void Reset()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
                InvertCulling = false;
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
