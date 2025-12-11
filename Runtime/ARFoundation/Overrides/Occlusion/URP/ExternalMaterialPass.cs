// Copyright 2022-2025 Niantic.
#if MODULE_URP_ENABLED
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        protected readonly string Name;

        /// <summary>
        /// Whether the culling mode should be inverted.
        /// </summary>
        internal bool InvertCulling { get; set; }

        /// <summary>
        /// The material used for performing operations on the input image.
        /// </summary>
        public Material Material { get; set; }

        protected ExternalMaterialPass(string name, RenderPassEvent renderPassEvent)
        {
            Name = name;
            profilingSampler = new ProfilingSampler(name);
            this.renderPassEvent = renderPassEvent;
        }

#if UNITY_6000_0_OR_NEWER
        [Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.", false)]
#endif
        public sealed override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Acquire a command buffer
            var cmd = CommandBufferPool.Get(Name);
            using (new ProfilingScope(cmd, profilingSampler))
            {
                // Set culling
                cmd.SetInvertCulling(InvertCulling);

                // Draw
                OnExecuteCompatibilityMode(cmd, ref renderingData);
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
        /// <remarks>When built using Unity 6, this only gets called in compatibility mode.</remarks>
        protected abstract void OnExecuteCompatibilityMode(CommandBuffer cmd, ref RenderingData renderingData);
    }
}
#endif // MODULE_URP_ENABLED
