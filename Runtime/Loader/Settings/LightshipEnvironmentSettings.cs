// Copyright 2022-2024 Niantic.

using Newtonsoft.Json;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.Utilities.UnityAssets;
using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    public partial class LightshipSettings
    {
        internal string ScanningEndpoint
        {
            get => _ardkConfiguration.ScanningEndpoint;
        }

        internal string ScanningSqcEndpoint
        {
            get => _ardkConfiguration.ScanningSqcEndpoint;
        }

        internal string VpsEndpoint
        {
            get => _ardkConfiguration.VpsEndpoint;
        }

        internal string VpsCoverageEndpoint
        {
            get => _ardkConfiguration.VpsCoverageEndpoint;
        }

        internal string SharedArEndpoint
        {
            get => _ardkConfiguration.SharedArEndpoint;
        }

        internal string FastDepthSemanticsEndpoint
        {
            get => _ardkConfiguration.FastDepthSemanticsEndpoint;
        }

        internal string DefaultDepthSemanticsEndpoint
        {
            get => _ardkConfiguration.DefaultDepthSemanticsEndpoint;
        }

        internal string SmoothDepthSemanticsEndpoint
        {
            get => _ardkConfiguration.SmoothDepthSemanticsEndpoint;
        }

        internal string ObjectDetectionEndpoint
        {
            get => _ardkConfiguration.ObjectDetectionEndpoint;
        }

        internal string TelemetryApiKey
        {
            get => _ardkConfiguration.TelemetryApiKey;
        }

        internal string TelemetryEndpoint
        {
            get => _ardkConfiguration.TelemetryEndpoint;
        }

        /// <summary>
        /// Internal for testing purposes only.
        /// </summary>
        internal class ArdkConfiguration
        {
            public string ApiKey;
            public string ScanningEndpoint;
            public string VpsEndpoint;
            public string VpsCoverageEndpoint;
            public string SharedArEndpoint;
            public string FastDepthSemanticsEndpoint;
            public string DefaultDepthSemanticsEndpoint;
            public string SmoothDepthSemanticsEndpoint;
            public string ScanningSqcEndpoint;
            public string ObjectDetectionEndpoint;
            public string TelemetryEndpoint;
            public string TelemetryApiKey;

            public static ArdkConfiguration GetDefaultEnvironmentConfig()
            {
                ArdkConfiguration defaultArdkConfig = new ArdkConfiguration()
                {
                    // Do NOT add api key for default values. But leave it as string.Empty. Not null. Else Unity will crash
                    ApiKey = string.Empty,

                    ScanningEndpoint = "https://wayfarer-ugc-api.nianticlabs.com/api/proto/v1/",
                    ScanningSqcEndpoint = "https://armodels.eng.nianticlabs.com/sqc/sqc3_enc.tar.gz",

                    SharedArEndpoint = "marsh-prod.nianticlabs.com",

                    VpsEndpoint = "https://vps-frontend.nianticlabs.com/web",
                    VpsCoverageEndpoint = "https://vps-coverage-api.nianticlabs.com/",

                    DefaultDepthSemanticsEndpoint = "https://armodels.eng.nianticlabs.com/niantic_ca_v1.2.bin",
                    FastDepthSemanticsEndpoint = "https://armodels.eng.nianticlabs.com/niantic_ca_v1.2_fast.bin",
                    SmoothDepthSemanticsEndpoint = "https://armodels.eng.nianticlabs.com/niantic_ca_v1.2_antiflicker.bin",

                    ObjectDetectionEndpoint = "https://armodels.eng.nianticlabs.com/niantic_ob_v0.4_full.bin",

                    TelemetryEndpoint = "https://analytics.nianticlabs.com",
                    TelemetryApiKey = "b7d03117-f80f-4039-8488-3466633f8639",
                };

                return defaultArdkConfig;
            }

            public static bool TryGetConfigurationFromJson(string fileWithPath, out ArdkConfiguration parsedConfig)
            {
                parsedConfig = null;
                bool hasData = FileUtilities.TryReadAllText(fileWithPath, out string result);

                if(hasData && !string.IsNullOrWhiteSpace(result))
                {
                    try
                    {
                        parsedConfig = JsonConvert.DeserializeObject<ArdkConfiguration>(result);

                        Log.Warning($"Targeting the following endpoints: {result}");
                        return true;
                    }
                    catch
                    {
                        Log.Warning("Parsing the config failed. Defaulting to the prod config.");
                    }
                }

                return false;
            }
        }
    }
}
