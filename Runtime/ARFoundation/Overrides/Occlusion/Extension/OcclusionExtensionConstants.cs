// Copyright 2022-2025 Niantic.
using UnityEngine;

namespace Niantic.Lightship.AR.Occlusion
{
    public partial class LightshipOcclusionExtension
    {
        /// <summary>
        /// Message logged when the extension tries to use the cpu image it failed to acquire.
        /// </summary>
        private const string k_MissingCpuImageMessage = "Could not acquire the cpu depth image.";

        /// <summary>
        /// Message logged when the OptimalOcclusionDistanceMode is invalidly set to SpecifiedGameObject.
        /// </summary>
        private const string k_MissingOccludeeMessage =
            "Active OptimalOcclusionDistanceMode is SpecifiedGameObject but the Principal Occludee " +
            "object is null. Falling back to the ClosestOccluder mode.";

        /// <summary>
        /// Message logged when an the semantics subsystem is attempted to be used without a semantics manager.
        /// </summary>
        private const string k_MissingSemanticSegmentationManagerMessage =
            "Could not find an active ARSemanticSegmentationManager. " +
            "Please ensure that an active ARSemanticSegmentationManager is present in the scene.";

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
    }
}
