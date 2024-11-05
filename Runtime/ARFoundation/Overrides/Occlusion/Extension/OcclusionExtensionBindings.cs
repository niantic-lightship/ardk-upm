using UnityEngine;

namespace Niantic.Lightship.AR.Occlusion
{
    public partial class LightshipOcclusionExtension
    {
        /// <summary>
        /// Property ID for the shader parameter for the depth texture.
        /// </summary>
        private static readonly int s_depthTextureId = Shader.PropertyToID("_Depth");

        /// <summary>
        /// Property ID for the shader parameter for the semantics texture.
        /// </summary>
        private static readonly int s_suppressionTextureId = Shader.PropertyToID("_Suppression");

        /// <summary>
        /// Property ID for the shader parameter for the fused depth texture (generated from the mesh).
        /// </summary>
        private static readonly int s_fusedDepthTextureId = Shader.PropertyToID("_FusedDepth");

        /// <summary>
        /// Property ID for the shader parameter for the depth display matrix.
        /// </summary>
        private static readonly int s_depthTransformId = Shader.PropertyToID("_DepthTransform");

        /// <summary>
        /// Property ID for the shader parameter for the semantics display matrix.
        /// </summary>
        private static readonly int s_suppressionTransformId = Shader.PropertyToID("_SuppressionTransform");

        /// <summary>
        /// Property ID for the shader parameter that describes the depth texture.
        /// </summary>
        private static readonly int s_depthTextureParams = Shader.PropertyToID("_DepthTextureParams");

        /// <summary>
        /// Property ID for the shader parameter that controls the blending between instant depth and fused depth.
        /// </summary>
        private static readonly int s_stabilizationThreshold = Shader.PropertyToID("_StabilizationThreshold");

        /// <summary>
        /// Property ID for the shader parameter for the Unity camera forward scale.
        /// </summary>
        private static readonly int s_cameraForwardScaleId = Shader.PropertyToID("_UnityCameraForwardScale");
    }
}
