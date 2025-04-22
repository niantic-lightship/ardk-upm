// Copyright 2022-2025 Niantic.

using UnityEngine;

namespace Niantic.Lightship.AR.Occlusion.Features
{
    /// <summary>
    /// Shader property IDs used by the occlusion features.
    /// </summary>
    internal static class ShaderProperties
    {
        /// <summary>
        /// Property ID for the depth texture inferred from the camera image.
        /// </summary>
        public static readonly int DepthTextureId = Shader.PropertyToID("_Depth");

        /// <summary>
        /// Property ID for the <see cref="DepthTextureId"/> display matrix.
        /// </summary>
        public static readonly int DepthTransformId = Shader.PropertyToID("_DepthTransform");

        /// <summary>
        /// Property ID for the extrinsics matrix bundled with the <see cref="DepthTextureId"/> texture.
        /// </summary>
        public static readonly int ExtrinsicsId = Shader.PropertyToID("_Extrinsics");

        /// <summary>
        /// Property ID for the intrinsics matrix bundled with the <see cref="DepthTextureId"/> texture.
        /// </summary>
        public static readonly int IntrinsicsId = Shader.PropertyToID("_Intrinsics");

        /// <summary>
        /// Property ID for the width of the <see cref="DepthTextureId"/> texture.
        /// </summary>
        public static readonly int ImageWidthId = Shader.PropertyToID("_ImageWidth");

        /// <summary>
        /// Property ID for the height of the <see cref="DepthTextureId"/> texture.
        /// </summary>
        public static readonly int ImageHeightId = Shader.PropertyToID("_ImageHeight");

        /// <summary>
        /// Property ID for the shader parameter that describes the <see cref="DepthTextureId"/> texture.
        /// </summary>
        public static readonly int DepthTextureParamsId = Shader.PropertyToID("_DepthTextureParams");

        /// <summary>
        /// Property ID for the shader parameter for the semantics texture.
        /// </summary>
        public static readonly int SuppressionTextureId = Shader.PropertyToID("_Suppression");

        /// <summary>
        /// Property ID for the shader parameter for the semantics display matrix.
        /// </summary>
        public static readonly int SuppressionTransformId = Shader.PropertyToID("_SuppressionTransform");

        /// <summary>
        /// Property ID for the Unity camera forward scale.
        /// </summary>
        public static readonly int CameraForwardScaleId = Shader.PropertyToID("_UnityCameraForwardScale");

        /// <summary>
        /// Property ID for the mesh depth texture.
        /// </summary>
        public static readonly int FusedDepthTextureId = Shader.PropertyToID("_FusedDepth");

        /// <summary>
        /// Property ID for the mesh depth display matrix.
        /// </summary>
        public static readonly int FusedDepthTransformId = Shader.PropertyToID("_FusedDepthTransform");

        /// <summary>
        /// Property ID for the stabilization threshold.
        /// </summary>
        public static readonly int StabilizationThreshold = Shader.PropertyToID("_StabilizationThreshold");

        /// <summary>
        /// Property ID for the color mask.
        /// </summary>
        public static readonly int ColorMaskId = Shader.PropertyToID("_ColorMask");
    }
}
