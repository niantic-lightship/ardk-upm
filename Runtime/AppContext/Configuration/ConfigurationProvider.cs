// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Niantic.Lightship.AR.Configuration
{
    /// <summary>
    /// Parses the endpoints and configurations provided in the file as per the <see cref="_ardkConfiguration"/>> class
    /// </summary>
    internal class ConfigurationProvider
    {
        private _ArdkConfiguration _ardkConfiguration;

        /// <summary>
        /// Provides the config based on the file location if it has a valid config.
        /// Please store the config file in the streaming assets directory
        /// </summary>
        /// <param name="fileWithPath">full path of the file</param>
        public ConfigurationProvider(string fileWithPath)
        {
            bool parsingSuccessful = false;

            string json = ReadFile(fileWithPath);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    _ardkConfiguration = JsonConvert.DeserializeObject<_ArdkConfiguration>(json);
                    parsingSuccessful = true;
                    Debug.LogWarning($"Using ArdkConfig: {JsonConvert.SerializeObject(_ardkConfiguration)}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Parsing the config was not successful. Exception: {e}");
                }
            }

            if(!parsingSuccessful)
            {
                _ardkConfiguration = GetDefaultEnvironmentConfig();
            }
        }

        public string ApiKey
        {
            get => _ardkConfiguration.ApiKey;
        }

        public string ScanningEndpoint
        {
            get => _ardkConfiguration.ScanningEndpoint;
        }

        public string ScanningSqcEndpoint
        {
            get => _ardkConfiguration.ScanningSqcEndpoint;
        }

        public string VpsEndpoint
        {
            get => _ardkConfiguration.VpsEndpoint;
        }

        public string VpsCoverageEndpoint
        {
            get => _ardkConfiguration.VpsCoverageEndpoint;
        }

        public string SharedArEndpoint
        {
            get => _ardkConfiguration.SharedArEndpoint;
        }

        public string FastDepthSemanticsEndpoint
        {
            get => _ardkConfiguration.FastDepthSemanticsEndpoint;
        }

        public string DefaultDepthSemanticsEndpoint
        {
            get => _ardkConfiguration.DefaultDepthSemanticsEndpoint;
        }

        public string SmoothDepthSemanticsEndpoint
        {
            get => _ardkConfiguration.SmoothDepthSemanticsEndpoint;
        }

        public string TelemetryApiKey
        {
            get => _ardkConfiguration.TelemetryApiKey;
        }

        public string TelemetryEndpoint
        {
            get => _ardkConfiguration.TelemetryEndpoint;
        }

        private string ReadFile(string filePath)
        {
            // Android's apk is a jar file and hence has this wonky behaviour.
            // please do NOT use #if UNITY_ANDROID.
            // having all the code enabled at all times makes it easier to test and
            // for identifying issues.
            string result = string.Empty;
            if (filePath.Contains("://") || filePath.Contains(":///"))
            {
                // the constructor is async in Unity
                WWW www = new WWW(filePath);
                // without this loop, we call things without them being ready.
                // it takes about 00:00:00.0003 to get the www object ready
                while (!www.isDone){}

                result = www.text;
            }
            else
            {
                if (File.Exists(filePath))
                {
                    result = File.ReadAllText(filePath);
                }
            }

            return result;
        }

        private _ArdkConfiguration GetDefaultEnvironmentConfig()
        {
            Debug.Log("Configuring system to target Prod");
            _ArdkConfiguration defaultArdkConfig = new _ArdkConfiguration()
            {
                // Do NOT add api key for default values
                ScanningEndpoint = "https://wayfarer-ugc-api.nianticlabs.com/api/proto/v1/",
                ScanningSqcEndpoint = "https://armodels.eng.nianticlabs.com/sqc/sqc3_enc.tar.gz",

                SharedArEndpoint = "marsh-prod.nianticlabs.com",

                VpsEndpoint = "https://vps-frontend.nianticlabs.com/web",
                VpsCoverageEndpoint = "https://vps-coverage-api.nianticlabs.com/",

                DefaultDepthSemanticsEndpoint = "https://armodels.eng.nianticlabs.com/niantic_ca_v1.2.bin",
                FastDepthSemanticsEndpoint = "https://armodels.eng.nianticlabs.com/niantic_ca_v1.2_fast.bin",
                SmoothDepthSemanticsEndpoint = "https://armodels.eng.nianticlabs.com/niantic_ca_v1.2_antiflicker.bin",

                TelemetryEndpoint = "https://analytics.nianticlabs.com",
                TelemetryApiKey = "b7d03117-f80f-4039-8488-3466633f8639", // EXT-REPLACE
            };

            return defaultArdkConfig;
        }

        private class _ArdkConfiguration
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
        }
    }
}
