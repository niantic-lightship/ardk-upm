using UnityEngine;

namespace Niantic.Lightship.AR.Occlusion
{
    public partial class LightshipOcclusionExtension
    {
        /// <summary>
        /// Name of the default Lightship Occlusion Extension shader.
        /// </summary>
        public const string DefaultShaderName = "Lightship/OcclusionExtension";

        /// <summary>
        /// Name for the custom rendering command buffer.
        /// </summary>
        private const string k_CustomRenderPassName = "LightshipOcclusionExtension Pass (LegacyRP)";

        /// <summary>
        /// Message logged when the extension tries to set a frame rate that is not supported by the subsystem.
        /// </summary>
        private const string k_TargetFrameRateNotSupportedMessage =
            "TargetFrameRate is not supported on non-Lightship implementations of the XROcclusionSubsystem.";

        /// <summary>
        /// Message logged when the LatestInrinsicsMatrix API is not supported by the subsystem.
        /// </summary>
        private const string k_LatestIntrinsicsMatrixNotSupportedMessage =
            "LatestInrinsicsMatrix is not supported on non-Lightship implementations of the XROcclusionSubsystem.";

        /// <summary>
        /// Message logged when the LatestExtrinsicsMatrix API is not supported by the subsystem.
        /// </summary>
        private const string k_LatestExtrinsicsMatrixNotSupportedMessage =
            "LatestExtrinsicsMatrix is not supported on non-Lightship implementations of the XROcclusionSubsystem.";

        /// <summary>
        /// Message logged when the extension tries to use the cpu image it failed to acquire.
        /// </summary>
        private const string k_MissingCpuImageMessage = "Could not acquire the cpu depth image.";

        /// <summary>
        /// Message logged when there has been an error updating the suppression texture.
        /// </summary>
        private const string k_SuppressionTextureErrorMessage = "Unable to update the depth suppresion texture.";

        /// <summary>
        /// Message logged when the OptimalOcclusionDistanceMode is invalidly set to SpecifiedGameObject.
        /// </summary>
        private const string k_MissingOccludeeMessage =
            "Active OptimalOcclusionDistanceMode is SpecifiedGameObject but the Principal Occludee " +
            "object is null. Falling back to the ClosestOccluder mode.";

        /// <summary>
        /// Message logged when the semantic depth suppression is enabled without the required components in the scene.
        /// </summary>
        private const string k_MissingSemanticManagerMessage =
            "Missing ARSemanticSegmentationManager component reference. " +
            "One in the scene is required to enable semantic depth suppression.";

        /// <summary>
        /// Message logged when the occlusion stabilization feature is enabled without the required components in the scene.
        /// </summary>
        private const string k_MissingMeshManagerMessage =
            "Missing ARMeshManager component reference. " +
            "One in the scene is required to enable occlusion stabilization.";

        /// <summary>
        /// Message logged when an api tries to interact with the fused depth camera without it being initialized.
        /// </summary>
        private const string k_MissingFusedDepthCameraMessage =
            "Fused depth camera is not initialized. Please enable occlusion stabilization first.";

        /// <summary>
        /// Message logged when the occlusion extension shader is missing from the project.
        /// </summary>
        private const string k_MissingShaderResourceMessage =
            "Missing " + DefaultShaderName + " shader resource.";

        /// <summary>
        /// Message logged when the component is set to use a custom material, but the the material resource is null.
        /// </summary>
        private const string k_MissingCustomBackgroundMaterialMessage =
            "Set to use a custom background material without a valid reference.";

        /// <summary>
        /// Shader keyword for occlusion stabilization.
        /// </summary>
        private const string k_OcclusionStabilizationFeature = "FEATURE_STABILIZATION";

        /// <summary>
        /// Shader keyword for semantic suppression.
        /// </summary>
        private const string k_OcclusionSuppressionFeature = "FEATURE_SUPPRESSION";

        /// <summary>
        /// Shader keyword for depth edge smoothing.
        /// </summary>
        private const string k_DepthEdgeSmoothingFeature = "FEATURE_EDGE_SMOOTHING";

        /// <summary>
        /// Shader keyword for debug view.
        /// </summary>
        private const string k_DebugViewFeature = "FEATURE_DEBUG";

        /// <summary>
        /// Minimum possible value for the optimal occlusion depth.
        /// </summary>
        private const float k_MinimumDepthSample = 0.2f;

        /// <summary>
        /// Maximum possible value for the optimal occlusion depth.
        /// </summary>
        private const float k_MaximumDepthSample = 100.0f;

        /// <summary>
        /// Value used to exclude the edges of the image from 'ClosestOccluder' occlusion mode, since objects at
        /// the edges should be ignored for the calculation.
        /// </summary>
        private const float k_FullScreenSampleBorder = 0.2f;

        /// <summary>
        /// Rectangle used to sample the depth buffer for the optimal occlusion depth.
        /// </summary>
        private static readonly Rect s_fullScreenRect = new
        (
            k_FullScreenSampleBorder,
            k_FullScreenSampleBorder,
            1 - k_FullScreenSampleBorder,
            1 - k_FullScreenSampleBorder
        );

        /// <summary>
        /// ARFoundation's renderer gets added to the camera on the first AR frame, so this component needs
        /// to wait this number of AR frames before attaching its command buffers.
        /// </summary>
        private const int k_attachDelay = 2;
    }
}
