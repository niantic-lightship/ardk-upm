// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// Factory to create CoverageClient instances.
    public static class CoverageClientFactory
    {
        [Obsolete("Create a CoverageClient instance using the default constructor instead.")]
        public static CoverageClient Create()
        {
            return new CoverageClient();
        }
    }
}
