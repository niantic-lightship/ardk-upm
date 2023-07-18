// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System.IO;
using Newtonsoft.Json;
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

        internal string TelemetryApiKey
        {
            get => _ardkConfiguration.TelemetryApiKey;
        }

        internal string TelemetryEndpoint
        {
            get => _ardkConfiguration.TelemetryEndpoint;
        }

        private class ArdkConfiguration
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
            public string TelemetryEndpoint;
            public string TelemetryApiKey;
            
            public static ArdkConfiguration GetDefaultEnvironmentConfig()
            {
                Debug.Log("Configuring system to target Prod");
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

                    TelemetryEndpoint = "https://analytics.nianticlabs.com",
                    TelemetryApiKey = "b7d03117-f80f-4039-8488-3466633f8639",
                };

                return defaultArdkConfig;
            }

            public static bool TryGetConfigurationFromJson(string fileWithPath, out ArdkConfiguration parsedConfig)
            {
                // Android's apk is a jar file and hence has this wonky behaviour.
                // please do NOT use #if UNITY_ANDROID.
                // having all the code enabled at all times makes it easier to test and
                // for identifying issues.
                parsedConfig = null;
                string result = string.Empty;
                if (fileWithPath.Contains("://") || fileWithPath.Contains(":///"))
                {
                    // the constructor is async in Unity
                    WWW www = new WWW(fileWithPath);
                    // without this loop, we call things without them being ready.
                    // it takes about 00:00:00.0003 to get the www object ready
                    while (!www.isDone){}

                    result = www.text;
                }
                else
                {
                    if (File.Exists(fileWithPath))
                    {
                        result = File.ReadAllText(fileWithPath);
                    }
                }
                
                if(!string.IsNullOrWhiteSpace(result))
                {
                    try
                    {
                        parsedConfig = JsonConvert.DeserializeObject<ArdkConfiguration>(result);
                        
                        Debug.LogWarning($"Targeting the following endpoints: {result}");
                        return true;
                    }
                    catch 
                    { 
                        Debug.LogWarning("Parsing the config failed. Defaulting to the prod config.");
                    }
                }

                return false;
            }
        }
    }
}
