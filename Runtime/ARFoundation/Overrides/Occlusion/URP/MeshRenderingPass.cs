#if MODULE_URP_ENABLED

// Copyright 2022-2024 Niantic.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
    }
}
#endif // MODULE_URP_ENABLED
