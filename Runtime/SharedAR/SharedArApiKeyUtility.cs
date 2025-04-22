// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities.Logging;

namespace Niantic.Lightship.SharedAR.Settings
{
    internal static class SharedArApiKeyUtility
    {
        // Check if an API key is set in Lightship Settings
        // For now, just log an error, but this can be used to gate apis in the future.
        internal static void CheckApiKey()
        {
            var noValueApiKey = LightshipSettingsHelper.ActiveSettings == null ||
                string.IsNullOrEmpty(LightshipSettingsHelper.ActiveSettings.ApiKey);

            if (noValueApiKey)
            {
                Log.Error("API Key is not set. Please set API Key in Lightship Settings.");
            }
        }

    }
}
