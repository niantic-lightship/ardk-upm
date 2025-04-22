// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.Mapping
{
    [Experimental]
    public enum DeviceMappingType
    {
        None = 0,
        Orb = 1,

        // @note
        // Learned features require loading and running a model to extract features.
        // This makes learned features slower to load and run than Orb features. It is
        // recommended to use Orb features for performance and future support
        GpuLearnedFeatures = 2,

        // @note
        // Learned features require loading and running a model to extract features.
        // This makes learned features slower to load and run than Orb features. It is
        // recommended to use Orb features for performance and future support
        CpuLearnedFeatures = 3
    }
}
