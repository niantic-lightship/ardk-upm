// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Loader;

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// Factory to create CoverageClient instances.
    public static class CoverageClientFactory
    {
        public static CoverageClient Create()
        {
            // TODO [AR-16577]: Using the asset instance will prevent this class from being easily testable
            return new CoverageClient(LightshipSettings.Instance);
        }
    }
}
