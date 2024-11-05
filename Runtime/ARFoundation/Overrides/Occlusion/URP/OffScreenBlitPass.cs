#if MODULE_URP_ENABLED

// Copyright 2022-2024 Niantic.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// Renders an image using the provided material to an off-screen render texture.
    /// </summary>
    internal sealed class OffScreenBlitPass : FullScreenBlitPass
    {
        // The target render texture
        private RenderTexture _target;

        public OffScreenBlitPass(string name, RenderPassEvent renderPassEvent)
            : base(name, renderPassEvent) { }

        /// <summary>
        /// Call this before enqueueing the render pass.
        /// </summary>
        /// <param name="material">The material used to render the image.</param>
        /// <param name="target">The target to render the image to.</param>
        public void Setup(Material material, RenderTexture target)
        {
            SetMaterial(material);
            _target = target;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);

            // TODO(ahegedus): Make this use RTHandle instead of RenderTexture
            ConfigureTarget(_target);
        }
    }
}
#endif // MODULE_URP_ENABLED
