// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System.ComponentModel;

namespace Niantic.Lightship.AR.Utilities.Preloading
{
    public enum DepthMode : byte
    {
        [Description("No model specified")]
        Unspecified = 0,
        [Description("Custom model")]
        Custom = 1,
        [Description("Fast")]
        Fast = 2,
        [Description("Medium")]
        Medium = 3,
        [Description("Smooth")]
        Smooth = 4,
    }

    public enum SemanticsMode : byte
    {
        [Description("No model specified")]
        Unspecified = 0,
        [Description("Custom model")]
        Custom = 1,
        [Description("Fast")]
        Fast = 2,
        [Description("Medium")]
        Medium = 3,
        [Description("Smooth")]
        Smooth = 4,
    }
}
