// Copyright 2022-2025 Niantic.
#if MODULE_URP_ENABLED
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// Renders an image using the provided material to an off-screen render texture.
    /// </summary>
    internal sealed class OffScreenBlitPass : FullScreenBlitPass
    {
        /// <summary>
        /// The target render texture.
        /// </summary>
        public RTHandle Target { get; set; }

        public OffScreenBlitPass(string name, RenderPassEvent renderPassEvent)
            : base(name, renderPassEvent)
        {
        }

        [Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.", false)]
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) =>
            ConfigureTarget(Target);

#if UNITY_6000_0_OR_NEWER
        private class PassData
        {
            public UniversalCameraData CameraData;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using var builder =
                renderGraph.AddRasterRenderPass<PassData>(Name, out var passData, profilingSampler);

            // Frame data
            passData.CameraData = frameData.Get<UniversalCameraData>();

            // Set render target
            builder.SetRenderAttachment(renderGraph.ImportTexture(Target), 0);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                var cmd = context.cmd;
                cmd.SetInvertCulling(InvertCulling);

                // Set matrix
                cmd.SetViewProjectionMatrices(
                    Matrix4x4.identity,
                    renderPassEvent == RenderPassEvent.BeforeRenderingOpaques
                        ? BeforeOpaquesProjection
                        : AfterOpaquesProjection);

                cmd.DrawMesh(
                    renderPassEvent == RenderPassEvent.BeforeRenderingOpaques
                        ? FullScreenNearClipMesh
                        : FullScreenFarClipMesh,
                    Matrix4x4.identity, Material);

                cmd.SetViewProjectionMatrices(
                    data.CameraData.camera.worldToCameraMatrix,
                    data.CameraData.camera.projectionMatrix);
            });
        }
#endif
    }
}
#endif // MODULE_URP_ENABLED
