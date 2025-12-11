// Copyright 2022-2025 Niantic.
#if MODULE_URP_ENABLED
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// Renders a mesh with the provided material.
    /// </summary>
    internal sealed class MeshRenderingPass : ExternalMaterialPass
    {
        /// <summary>
        /// The mesh resource to render.
        /// </summary>
        public Mesh Mesh { get; set; }

        internal MeshRenderingPass(string name, RenderPassEvent renderPassEvent)
            : base(name, renderPassEvent) { }

        /// <summary>
        /// Invoked when the render pass is executed.
        /// </summary>
        /// <param name="cmd">The temporary command buffer used to issue draw commands.</param>
        /// <param name="renderingData">Current rendering state information</param>
        /// <remarks>When built using Unity 6, this only gets called in compatibility mode.</remarks>
        protected override void OnExecuteCompatibilityMode(CommandBuffer cmd, ref RenderingData renderingData) =>
            cmd.DrawMesh(Mesh, Matrix4x4.identity, Material);

#if UNITY_6000_0_OR_NEWER
        /// <summary>
        /// Contains the rendering context.
        /// </summary>
        private class PassData { }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using var builder = renderGraph.AddRasterRenderPass(Name, out PassData _, profilingSampler);

            var resourceData = frameData.Get<UniversalResourceData>();
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

            builder.SetRenderFunc((PassData _, RasterGraphContext context) =>
                context.cmd.DrawMesh(Mesh, Matrix4x4.identity, Material));
        }
#endif
    }
}
#endif // MODULE_URP_ENABLED
