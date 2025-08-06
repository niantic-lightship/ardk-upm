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
        private Mesh _mesh;

        internal MeshRenderingPass(string name, RenderPassEvent renderPassEvent) : base(name, renderPassEvent)
        {
        }

        public void SetMesh(Mesh mesh)
        {
            _mesh = mesh;
        }

        protected override void OnExecute(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Draw
            cmd.DrawMesh(_mesh, Matrix4x4.identity, Material);
        }

#if UNITY_6000_0_OR_NEWER
        /// <summary>
        /// Invoked when the render pass is executed when using Render Graph.
        /// </summary>
        protected override void OnExecute(RasterGraphContext context, PassData renderingData)
        {
            var cmd = context.cmd;
            cmd.DrawMesh(_mesh, Matrix4x4.identity, Material);
        }
#endif
    }
}
#endif // MODULE_URP_ENABLED
