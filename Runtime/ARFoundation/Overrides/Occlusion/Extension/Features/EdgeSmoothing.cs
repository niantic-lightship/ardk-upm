// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Common;
using UnityEngine;

namespace Niantic.Lightship.AR.Occlusion.Features
{
    internal sealed class EdgeSmoothing : RenderComponent
    {


        /// <summary>
        /// Shader keyword for edge smoothing.
        /// </summary>
        protected override string Keyword
        {
            get => "FEATURE_EDGE_SMOOTHING";
        }

        private bool _textureParamsSet;

        protected override void OnMaterialUpdate(Material mat)
        {
            base.OnMaterialUpdate(mat);

            if (_textureParamsSet)
            {
                return;
            }

            // Wait for the depth texture to be available
            var depthTexture = GetTexture(ShaderProperties.DepthTextureId);
            if (depthTexture == null)
            {
                return;
            }

            // Set the depth texture parameters
            var width = depthTexture.width;
            var height = depthTexture.height;
            mat.SetVector(ShaderProperties.DepthTextureParamsId, new Vector4(1.0f / width, 1.0f / height, width, height));
            _textureParamsSet = true;
        }
    }
}
