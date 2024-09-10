// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities.Logging;

namespace Niantic.Lightship.AR.Mapping
{
    internal static class DeviceMapFeatureFlag
    {
        private const string SlickMappingFeatureFlagName = "SlickMapping";

        internal static bool IsFeatureEnabled()
        {
            if (!LightshipUnityContext.FeatureEnabled(SlickMappingFeatureFlagName))
            {
                Log.Warning($"{SlickMappingFeatureFlagName} is disabled. Enable in the feature flag file");
                return false;
            }

            return true;
        }

    }
}
