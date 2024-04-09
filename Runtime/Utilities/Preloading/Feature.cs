// Copyright 2022-2024 Niantic.

using System.ComponentModel;

namespace Niantic.Lightship.AR.Utilities.Preloading
{
    /// <summary>
    /// The Lightship depth model to use.
    /// </summary>
    /// <remarks>
    /// This is analogous to Unity's <see cref="UnityEngine.XR.ARSubsystems.EnvironmentDepthMode"/>.
    /// </remarks>
    [PublicAPI]
    public enum DepthMode : byte
    {
        /// <summary>
        /// The default model will be used, if applicable.
        /// </summary>
        [Description("No model specified")]
        Unspecified = 0,

        /// <summary>
        /// Lightship will use the model file provided by the user.
        /// </summary>
        /// <remarks>
        /// The model file path can be provided via the ARDK configuration file or with
        /// <see cref="IModelPreloader.RegisterModel(DepthMode, string)"/>.
        /// </remarks>
        [Description("Custom model")]
        Custom = 1,

        /// <summary>
        /// Depth will be generated at the fastest resolution.
        /// </summary>
        [Description("Fast")]
        Fast = 2,

        /// <summary>
        /// Depth will be generated at a medium resolution.
        /// </summary>
        [Description("Medium")]
        Medium = 3,

        /// <summary>
        /// Depth will be generated at the best resolution.
        /// </summary>
        [Description("Smooth")]
        Smooth = 4,

    }

    /// <summary>
    /// The Lightship semantic segmentation model to use.
    /// </summary>
    [PublicAPI]
    public enum SemanticsMode : byte
    {
        /// <summary>
        /// The default model will be used, if applicable.
        /// </summary>
        [Description("No model specified")]
        Unspecified = 0,

        /// <summary>
        /// Lightship will use the model file provided by the user.
        /// </summary>
        /// <remarks>
        /// The model file path can be provided via the ARDK configuration file or with
        /// <see cref="IModelPreloader.RegisterModel(SemanticsMode, string)"/>.
        /// </remarks>
        [Description("Custom model")]
        Custom = 1,

        /// <summary>
        /// Semantic segmentation will be generated at the fastest resolution.
        /// </summary>
        [Description("Fast")]
        Fast = 2,

        /// <summary>
        /// Semantic segmentation will be generated at a medium resolution.
        /// </summary>
        [Description("Medium")]
        Medium = 3,

        /// <summary>
        /// Semantic segmentation will be generated at the best resolution.
        /// </summary>
        [Description("Smooth")]
        Smooth = 4,
    }

    /// <summary>
    /// The Lightship object detection model to use.
    /// </summary>
    [PublicAPI]
    public enum ObjectDetectionMode : byte
    {
        /// <summary>
        /// The default model will be used.
        /// </summary>
        [Description("Default model")]
        Default = 0,
    }

}
