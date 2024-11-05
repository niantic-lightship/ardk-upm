#if MODULE_URP_ENABLED

// Copyright 2022-2024 Niantic.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// Renders a full screen quad with the provided material.
    /// </summary>
    internal class FullScreenBlitPass : ExternalMaterialPass
    {
        protected FullScreenBlitPass(string name, RenderPassEvent renderPassEvent) : base(name, renderPassEvent)
        {
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        /// <summary>
        /// Invoked when the render pass is executed.
        /// </summary>
        /// <param name="cmd">The temporary command buffer used to issue draw commands.</param>
        /// <param name="renderingData">Current rendering state information</param>
        protected override void OnExecute(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Push matrix
            cmd.SetViewProjectionMatrices(Matrix4x4.identity,
                renderPassEvent == RenderPassEvent.BeforeRenderingOpaques
                    ? BeforeOpaquesProjection
                    : AfterOpaquesProjection);

            // Draw
            cmd.DrawMesh(
                renderPassEvent == RenderPassEvent.BeforeRenderingOpaques
                    ? FullScreenNearClipMesh
                    : FullScreenFarClipMesh,
                Matrix4x4.identity, Material);

            // Pop matrix
            cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix,
                renderingData.cameraData.camera.projectionMatrix);
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
                    new Vector3(0f, 0f, zVal), new Vector3(0f, 1f, zVal), new Vector3(1f, 1f, zVal),
                    new Vector3(1f, 0f, zVal),
                },
                uv = new[]
                {
                    new Vector2(0f, bottomV), new Vector2(0f, topV), new Vector2(1f, topV),
                    new Vector2(1f, bottomV),
                },
                triangles = new[] {0, 1, 2, 0, 2, 3}
            };

            mesh.UploadMeshData(false);
            return mesh;
        }

        #endregion
    }
}
#endif // MODULE_URP_ENABLED
