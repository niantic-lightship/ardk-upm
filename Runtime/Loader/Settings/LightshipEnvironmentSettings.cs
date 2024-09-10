// Copyright 2022-2024 Niantic.

namespace Niantic.Lightship.AR.Loader
{
    public partial class LightshipSettings
    {
        internal string ScanningEndpoint
        {
            get => EndpointSettings.ScanningEndpoint;
        }

        internal string ScanningSqcEndpoint
        {
            get => EndpointSettings.ScanningSqcEndpoint;
        }

        internal string VpsEndpoint
        {
            get => EndpointSettings.VpsEndpoint;
        }

        internal string VpsCoverageEndpoint
        {
            get => EndpointSettings.VpsCoverageEndpoint;
        }

        internal string SharedArEndpoint
        {
            get => EndpointSettings.SharedArEndpoint;
        }

        internal string FastDepthSemanticsEndpoint
        {
            get => EndpointSettings.FastDepthSemanticsEndpoint;
        }

        internal string DefaultDepthSemanticsEndpoint
        {
            get => EndpointSettings.DefaultDepthSemanticsEndpoint;
        }

        internal string SmoothDepthSemanticsEndpoint
        {
            get => EndpointSettings.SmoothDepthSemanticsEndpoint;
        }

        internal string ObjectDetectionEndpoint
        {
            get => EndpointSettings.ObjectDetectionEndpoint;
        }

        internal string TelemetryApiKey
        {
            get => EndpointSettings.TelemetryApiKey;
        }

        internal string TelemetryEndpoint
        {
            get => EndpointSettings.TelemetryEndpoint;
        }
    }
}
