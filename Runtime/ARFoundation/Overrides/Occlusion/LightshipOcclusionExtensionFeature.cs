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
        private OcclusionExtensionPass _occlusionExtensionPass;

        public override void Create()
        {
            // Allocate render pass
            _occlusionExtensionPass = new OcclusionExtensionPass();
        }

        // Invoked when it is time to schedule the render pass
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var currentCamera = renderingData.cameraData.camera;
            if (currentCamera == null || currentCamera.cameraType != CameraType.Game)
                return;

            // Don't want to add this pass to a secondary camera, e.g. the fused depth camera
            if (!currentCamera.CompareTag("MainCamera"))
                return;

            var cameraBackground = currentCamera.gameObject.GetComponent<ARCameraBackground>();
            if (cameraBackground == null || !cameraBackground.enabled)
                return;

            var occlusionExt = currentCamera.gameObject.GetComponent<LightshipOcclusionExtension>();
            if (occlusionExt == null || !occlusionExt.enabled)
                return;

            if (cameraBackground.backgroundRenderingEnabled && occlusionExt.IsRenderingActive)
            {
                if (_occlusionExtensionPass.TrySetState(
                        occlusionExt.BackgroundMaterial,
                        invertCulling: cameraBackground.GetComponent<ARCameraManager>()?.subsystem?.invertCulling ??
                        false,
                        renderingMode: cameraBackground.currentRenderingMode))
                {
                    renderer.EnqueuePass(_occlusionExtensionPass);
                }
            }
        }

        private class OcclusionExtensionPass : ScriptableRenderPass
        {
            /// <summary>
            /// The name for the custom render pass which will display in graphics debugging tools.
            /// </summary>
            private const string KRenderPassName = "Lightship Occlusion Extension (URP)";

            /// <summary>
            /// The material used for performing operations on depth, custom to Lightship features.
            /// </summary>
            private Material _material;

            /// <summary>
            /// Whether the culling mode should be inverted.
            /// </summary>
            private bool _invertCulling;

            /// <summary>
            /// Sets the custom lightship material reference and other settings for the pass. Returns false if the
            /// provided rendering mode is not supported.
            /// </summary>
            public bool TrySetState(Material material, bool invertCulling, XRCameraBackgroundRenderingMode renderingMode)
            {
                if (renderingMode == XRCameraBackgroundRenderingMode.BeforeOpaques)
                {
                    renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
                }
                else if (renderingMode == XRCameraBackgroundRenderingMode.AfterOpaques)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                }
                else
                {
                    return false;
                }

                _material = material;
                _invertCulling = invertCulling;

                // Do not clear anything, we overdraw where eligible
                ConfigureClear(ClearFlag.None, Color.clear);

                return true;
            }

            /// <inheritdoc />
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Acquire a command buffer
                var cmd = CommandBufferPool.Get(KRenderPassName);

                // Start profiling the render pass
                cmd.BeginSample(KRenderPassName);

                // Push state
                cmd.SetInvertCulling(_invertCulling);
                cmd.SetViewProjectionMatrices(Matrix4x4.identity,
                    renderPassEvent == RenderPassEvent.BeforeRenderingOpaques
                        ? BeforeOpaquesProjection
                        : AfterOpaquesProjection);

                // Draw
                cmd.DrawMesh(
                    renderPassEvent == RenderPassEvent.BeforeRenderingOpaques
                        ? FullScreenNearClipMesh
                        : FullScreenFarClipMesh,
                    Matrix4x4.identity, _material);

                // Pop state
                cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix,
                    renderingData.cameraData.camera.projectionMatrix);

                // Finish profiling the render pass
                cmd.EndSample(KRenderPassName);

                // Commit
                context.ExecuteCommandBuffer(cmd);

                // Release command buffer
                CommandBufferPool.Release(cmd);
            }

            #region Utils

            /// <summary>
            /// The orthogonal projection matrix for the before opaque background rendering.
            /// </summary>
            private static Matrix4x4 BeforeOpaquesProjection { get; } = Matrix4x4.Ortho(0f, 1f, 0f, 1f, -0.1f, 9.9f);

            /// <summary>
            /// The orthogonal projection matrix for the after opaque background rendering.
            /// </summary>
            private static Matrix4x4 AfterOpaquesProjection { get; } = Matrix4x4.Ortho(0, 1, 0, 1, 0, 1);

            /// <summary>
            /// A mesh that is placed near the near-clip plane
            /// </summary>
            private static Mesh FullScreenNearClipMesh
            {
                get
                {
                    if (!s_initializedNearClipMesh)
                    {
                        s_nearClipMesh = BuildFullscreenMesh(0.1f);
                        s_initializedNearClipMesh = s_nearClipMesh != null;
                    }

                    return s_nearClipMesh;
                }
            }

            /// <summary>
            /// A mesh that is placed near the far-clip plane
            /// </summary>
            private static Mesh FullScreenFarClipMesh
            {
                get
                {
                    if (!s_initializedFarClipMesh)
                    {
                        s_farClipMesh = BuildFullscreenMesh(-1f);
                        s_initializedFarClipMesh = s_farClipMesh != null;
                    }

                    return s_farClipMesh;
                }
            }

            private static bool s_initializedNearClipMesh;
            private static bool s_initializedFarClipMesh;
            private static Mesh s_nearClipMesh;
            private static Mesh s_farClipMesh;

            private static Mesh BuildFullscreenMesh(float zVal)
            {
                const float bottomV = 0f;
                const float topV = 1f;
                var mesh = new Mesh
                {
                    vertices = new[]
                    {
                        new Vector3(0f, 0f, zVal),
                        new Vector3(0f, 1f, zVal),
                        new Vector3(1f, 1f, zVal),
                        new Vector3(1f, 0f, zVal),
                    },
                    uv = new[]
                    {
                        new Vector2(0f, bottomV),
                        new Vector2(0f, topV),
                        new Vector2(1f, topV),
                        new Vector2(1f, bottomV),
                    },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 }
                };

                mesh.UploadMeshData(false);
                return mesh;
            }

            #endregion
        }
#endif // MODULE_URP_ENABLED
    }
}
