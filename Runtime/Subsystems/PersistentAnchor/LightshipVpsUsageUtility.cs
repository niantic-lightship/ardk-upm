// Copyright 2022-2024 Niantic.

using System;

using Niantic.Lightship.AR.XRSubsystems;

namespace Niantic.Lightship.AR.Subsystems.PersistentAnchor
{
    public static class LightshipVpsUsageUtility
    {
        /// <summary>
        /// Usage modes for Lightship VPS.
        /// These modes are intended to provide a simple way to configure Lightship VPS for different
        ///     use cases. The exact configuration for each mode may change over time as better
        ///     parameters are discovered.
        /// </summary>
        public enum LightshipVpsUsageMode
        {
            // Currently defaulting to SingleLocalization
            Default = 0,
            // Sends localization requests until the first successful response is received.
            SingleLocalization = 1,
            // Sends localization requests continuously to maintain a high accuracy localization.
            HighAccuracyContinuousLocalization,
            // Sends localization requests continuously with lower frequencey and higher compression.
            // Maintains a high accuracy localization with lower bandwidth.
            LowBandwidthContinuousLocalization,
            // Sends rapid localization requests for debugging purposes.
            // Smoothing is disabled so the content is likely to jump slightly with each localization.
            // This is helpful for debugging, but not recommended for production applications.
            HighFrequencyDebug,
            Custom = Int32.MaxValue
        }

        public static XRPersistentAnchorConfiguration CreateConfiguration(LightshipVpsUsageMode usageMode)
        {
            var config = new XRPersistentAnchorConfiguration();
            config.CloudLocalizationEnabled = true;
            config.DeviceMappingLocalizationEnabled = false;

            switch (usageMode)
            {
                // Default to SingleLocalization for now
                case LightshipVpsUsageMode.Default:
                case LightshipVpsUsageMode.SingleLocalization:
                    config.ContinuousLocalizationEnabled = false;
                    config.CloudLocalizerInitialRequestsPerSecond = 1.0f;
                    break;
                case LightshipVpsUsageMode.HighAccuracyContinuousLocalization:
                    config.ContinuousLocalizationEnabled = true;
                    config.TemporalFusionEnabled = true;
                    config.TransformUpdateSmoothingEnabled = true;
                    config.CloudLocalizerInitialRequestsPerSecond = 1.0f;
                    config.CloudLocalizerContinuousRequestsPerSecond = 1.0f;
                    break;
                case LightshipVpsUsageMode.LowBandwidthContinuousLocalization:
                    config.ContinuousLocalizationEnabled = true;
                    config.TemporalFusionEnabled = true;
                    config.TransformUpdateSmoothingEnabled = true;
                    config.CloudLocalizerInitialRequestsPerSecond = 1.5f;
                    config.CloudLocalizerContinuousRequestsPerSecond = 0.2f;
                    config.JpegCompressionQuality = 30;
                    break;
                case LightshipVpsUsageMode.HighFrequencyDebug:
                    config.ContinuousLocalizationEnabled = true;
                    config.TemporalFusionEnabled = false;
                    config.TransformUpdateSmoothingEnabled = false;
                    config.CloudLocalizerInitialRequestsPerSecond = 0f;
                    config.CloudLocalizerContinuousRequestsPerSecond = 0f;
                    config.JpegCompressionQuality = 30;
                    break;
                case LightshipVpsUsageMode.Custom:
                    break;
            }

            return config;
        }
    }
}
