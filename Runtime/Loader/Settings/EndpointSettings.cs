// Copyright 2022-2024 Niantic.

using System.IO;
using Newtonsoft.Json;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.Utilities.UnityAssets;
using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    internal class EndpointSettings
    {
        public string ApiKey { get; set; }
        public string ScanningEndpoint { get; set; }
        public string VpsEndpoint { get; set; }
        public string VpsCoverageEndpoint { get; set; }
        public string SharedArEndpoint { get; set; }
        public string FastDepthSemanticsEndpoint { get; set; }
        public string DefaultDepthSemanticsEndpoint { get; set; }
        public string SmoothDepthSemanticsEndpoint { get; set; }
        public string ScanningSqcEndpoint { get; set; }
        public string ObjectDetectionEndpoint { get; set; }
        public string TelemetryEndpoint { get; set; }
        public string TelemetryApiKey { get; set; }

        public static EndpointSettings GetDefaultEnvironmentConfig()
        {
            EndpointSettings defaultSettings = new EndpointSettings()
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

            return defaultSettings;
        }

        public static bool TryGetConfigurationFromJson(string fileWithPath, out EndpointSettings parsedConfig)
        {
            parsedConfig = null;
            bool hasData = FileUtilities.TryReadAllText(fileWithPath, out string result);

            if (hasData && !string.IsNullOrWhiteSpace(result))
            {
                try
                {
                    parsedConfig = JsonConvert.DeserializeObject<EndpointSettings>(result);

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

        public static EndpointSettings GetFromFileOrDefault()
        {
            const string ConfigFileName = "ardkConfig.json";

            var gotConfigurationFromJson =
                TryGetConfigurationFromJson
                (
                    Path.Combine(Application.streamingAssetsPath, ConfigFileName),
                    out EndpointSettings parsedConfig
                );

            return
                gotConfigurationFromJson ? parsedConfig : GetDefaultEnvironmentConfig();
        }

        public EndpointSettings() { }

        public EndpointSettings(EndpointSettings source)
        {
            CopyFrom(source);
        }

        internal void CopyFrom(EndpointSettings source)
        {
            ApiKey = source.ApiKey;
            ScanningEndpoint = source.ScanningEndpoint;
            VpsEndpoint = source.VpsEndpoint;
            VpsCoverageEndpoint = source.VpsCoverageEndpoint;
            SharedArEndpoint = source.SharedArEndpoint;
            FastDepthSemanticsEndpoint = source.FastDepthSemanticsEndpoint;
            DefaultDepthSemanticsEndpoint = source.DefaultDepthSemanticsEndpoint;
            SmoothDepthSemanticsEndpoint = source.SmoothDepthSemanticsEndpoint;
            ScanningSqcEndpoint = source.ScanningSqcEndpoint;
            ObjectDetectionEndpoint = source.ObjectDetectionEndpoint;
            TelemetryEndpoint = source.TelemetryEndpoint;
            TelemetryApiKey = source.TelemetryApiKey;
        }
    }
}
