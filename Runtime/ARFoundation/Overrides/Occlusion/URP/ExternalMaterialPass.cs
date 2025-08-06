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
    /// Base class for render passes that use an external material to draw.
    /// </summary>
    internal abstract class ExternalMaterialPass : ScriptableRenderPass
    {
        /// <summary>
        /// The name for the custom render pass which will display in graphics debugging tools.
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// Profiling sampler for the render pass.
        /// </summary>
        private readonly ProfilingSampler _profilingSampler;

        /// <summary>
        /// Whether the culling mode should be inverted.
        /// </summary>
        internal bool InvertCulling { get; set; }

        /// <summary>
        /// The material used for performing operations on the input image.
        /// </summary>
        protected Material Material { get; private set; }

        protected ExternalMaterialPass(string name, RenderPassEvent renderPassEvent)
        {
            _name = name;
            _profilingSampler = new ProfilingSampler(name);
            this.renderPassEvent = renderPassEvent;
        }

        /// <summary>
        /// Sets the material to draw with.
        /// </summary>
        public void SetMaterial(Material material)
        {
            Material = material;
        }

#if UNITY_6000_0_OR_NEWER
        protected class PassData
        {
            public UniversalCameraData CameraData;
            public UniversalResourceData ResourceData;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(_name, out var passData, profilingSampler))
            {
                // Populate pass data
                passData.CameraData = frameData.Get<UniversalCameraData>();
                passData.ResourceData = frameData.Get<UniversalResourceData>();

                // Set render targets
                builder.SetRenderAttachment(passData.ResourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(passData.ResourceData.activeDepthTexture, AccessFlags.Write);

                // Assign the draw function
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Set culling
                    context.cmd.SetInvertCulling(InvertCulling);

                    // Draw
                    OnExecute(context, data);
                });
            }
        }

        /// <summary>
        /// Invoked when the render pass is executed when using Render Graph.
        /// </summary>
        protected abstract void OnExecute(RasterGraphContext context, PassData renderingData);
#endif

#if UNITY_6000_0_OR_NEWER
        [Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.", false)]
#endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Acquire a command buffer
            var cmd = CommandBufferPool.Get(_name);
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                // Set culling
                cmd.SetInvertCulling(InvertCulling);

                // Draw
                OnExecute(cmd, ref renderingData);
            }

            // Commit and release
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// Invoked when the render pass is executed.
        /// </summary>
        /// <param name="cmd">The temporary command buffer used to issue draw commands.</param>
        /// <param name="renderingData">Current rendering state information</param>
        protected abstract void OnExecute(CommandBuffer cmd, ref RenderingData renderingData);
    }
}
#endif // MODULE_URP_ENABLED
