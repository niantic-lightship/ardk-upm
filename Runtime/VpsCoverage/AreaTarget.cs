// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// <summary>
    /// The AreaTarget struct is a wrapper struct that contains correlated VPS coverage area
    /// and localization target information.
    /// </summary>
    [PublicAPI]
    public struct AreaTarget
    {
        public CoverageArea Area { get; }
        public LocalizationTarget Target { get; }

        public AreaTarget(CoverageArea area, LocalizationTarget target)
        {
            Area = area;
            Target = target;
        }
    }
}
