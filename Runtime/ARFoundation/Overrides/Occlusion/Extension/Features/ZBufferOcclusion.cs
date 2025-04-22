// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.Occlusion.Features
{
    /// <summary>
    /// Implements an occlusion technique that writes the depth texture into the z-buffer of the frame.
    /// </summary>
    internal sealed class ZBufferOcclusion : OcclusionComponent
    {
        /// <summary>
        /// The shader required for the ZBufferOcclusion feature.
        /// </summary>
        public const string RequiredShaderName = "Lightship/ZBufferOcclusion";

        // Resources
        private Texture2D _defaultDepthTexture;

        // Helpers
        private float _cameraForwardScale;
        private bool _silenceDepthTextureWarning;

        protected override void OnMaterialAttach(Material mat)
        {
            base.OnMaterialAttach(mat);

            // The default texture bound to the depth property on the material.
            // It lets every pixel pass through until the real depth texture is ready to use.
            if (_defaultDepthTexture == null)
            {
                const float maxDistance = 1000.0f;
                _defaultDepthTexture = new Texture2D(2, 2, TextureFormat.RFloat, mipChain: false);
                _defaultDepthTexture.SetPixelData(new[] {maxDistance, maxDistance, maxDistance, maxDistance}, 0);
                _defaultDepthTexture.Apply(false);
            }

            // Set material to defaults
            mat.SetTexture(ShaderProperties.DepthTextureId, _defaultDepthTexture);
            mat.SetMatrix(ShaderProperties.DepthTransformId, Matrix4x4.identity);
        }

        protected override void OnUpdate(Camera camera)
        {
            base.OnUpdate(camera);

            // Set scale: this computes the affect the camera's localToWorld has on the length of the
            // forward vector, i.e., how much farther from the camera are things than with unit scale.
            var forward = camera.transform.localToWorldMatrix.GetColumn(2);
            _cameraForwardScale = forward.magnitude;
        }

        protected override void OnMaterialUpdate(Material mat)
        {
            base.OnMaterialUpdate(mat);

            // Get the depth texture
            var depthTexture = GPUDepth;

            // Do nothing if the depth texture is not available
            if (depthTexture == null)
            {
                if (!_silenceDepthTextureWarning)
                {
                    Log.Info("ZBufferOcclusion: No depth texture available.");
                    _silenceDepthTextureWarning = true;
                }
                return;
            }
            _silenceDepthTextureWarning = false;

            // Set the depth texture and transform
            mat.SetTexture(ShaderProperties.DepthTextureId, depthTexture);
            mat.SetMatrix(ShaderProperties.DepthTransformId, DepthTransform);
            mat.SetFloat(ShaderProperties.CameraForwardScaleId, _cameraForwardScale);
        }

        protected override void OnReleaseResources()
        {
            base.OnReleaseResources();

            if (_defaultDepthTexture != null)
            {
                Object.Destroy(_defaultDepthTexture);
            }
        }
    }
}
