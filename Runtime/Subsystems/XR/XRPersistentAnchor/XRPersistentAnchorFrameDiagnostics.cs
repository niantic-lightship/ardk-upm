// Copyright 2022-2024 Niantic.

using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Labels that describe possible issues with frame
    /// @note This is an experimental feature. Experimental features should not be used in
    /// production products as they are subject to breaking changes, not officially supported, and
    /// may be deprecated without notice
    /// </summary>
    public enum DiagnosticLabel : UInt32
    {
        Unknown = 0,
        TooDarkProbability, // SQC Prediction
        ObstructedProbability, // SQC Prediction
        BlurryProbability, // SQC Prediction
        TargetNotVisibleProbability, // SQC Prediction
        BadQualityProbability, // SQC Prediction
        GroundOrFeetProbability, // SQC Prediction
        BrightnessMean, // Unnormalized Intensity Average
        BrightnessVariance, // Unnormalized Intensity Variance
        OversaturatedPixelRatio, // Ratio of oversaturated pixels in the frame
        LinearVelocityMetersPerSecond, // Calculated from poses
        AngularVelocityRadiansPerSecond, // Calculated from poses
        InCarProbability // SQC prediction
    }

    /// <summary>
    /// Diagnostic information about frames used for localization
    /// @note This is an experimental feature. Experimental features should not be used in
    /// production products as they are subject to breaking changes, not officially supported, and
    /// may be deprecated without notice
    /// </summary>
    public struct XRPersistentAnchorFrameDiagnostics
    {
        /// <summary>
        /// Frame Id of frame in question
        /// </summary>
        public UInt64 FrameId;

        /// <summary>
        /// Timestamp of frame in question
        /// </summary>
        public UInt64 TimestampMs;

        /// <summary>
        /// Diagnostic Labels and their corresponding scores for the frame in question
        /// </summary>
        public Dictionary<DiagnosticLabel, float> ScoresPerDiagnosticLabel;
    }
}
