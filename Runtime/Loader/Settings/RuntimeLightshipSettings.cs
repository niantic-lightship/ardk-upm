// Copyright 2022-2025 Niantic.

using System;
using System.IO;
using Niantic.Lightship.AR.Auth;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;
using Niantic.Lightship.AR.Settings;
using Niantic.Lightship.AR.Utilities.Auth;

namespace Niantic.Lightship.AR.Loader
{
    public class RuntimeLightshipSettings : IAuthSettings
    {
        private string _apiKey;
        private AuthEnvironmentType _authEnvironment;
        private string _accessToken;
        private int _accessExpiresAt;
        private string _refreshToken;
        private int _refreshExpiresAt;
        private bool _useLightshipDepth;
        private bool _preferLidarIfAvailable;
        private bool _useLightshipMeshing;
        private bool _useLightshipSemanticSegmentation;
        private bool _useLightshipScanning;
        private bool _useLightshipPersistentAnchor;
        private bool _useLightshipObjectDetection;
        private bool _useLightshipWorldPositioning;
        private LocationDataSource _locationAndCompassDataSource;
        private SpoofLocationInfo _spoofLocationInfo;
        private SpoofCompassInfo _spoofCompassInfo;

        private LogLevel _unityLogLevel;
        private LogLevel _fileLogLevel;
        private LogLevel _stdoutLogLevel;

        private LightshipSimulationParams _lightshipSimulationParams;
        private EndpointSettings _endpointSettings;
        private TestSettings _testSettings;
        private ILightshipPlaybackSettings _playbackSettings;

        /// <summary>
        /// Get the Lightship API key.
        /// </summary>
        public string ApiKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_apiKey))
                {
                    // ensure that the config provider's key is overridden in case the user has provided their own
                    return _apiKey;
                }

                return EndpointSettings.ApiKey ?? string.Empty;
            }

            internal set => _apiKey = value;
        }

        public AuthEnvironmentType AuthEnvironment => _authEnvironment;

        public string AccessToken
        {
            get => _accessToken;
            set => SetAccess(value);
        }

        public int AccessExpiresAt => _accessExpiresAt;

        public string RefreshToken
        {
            get => _refreshToken;
            set => SetRefresh(value);
        }

        public int RefreshExpiresAt => _refreshExpiresAt;

        /// <summary>
        /// When enabled, Lightship's depth and occlusion features can be used via ARFoundation. Additional occlusion
        /// features unique to Lightship can be configured in the LightshipOcclusionExtension component.
        /// </summary>
        public bool UseLightshipDepth
        {
            get => _useLightshipDepth;
            set => _useLightshipDepth = value;
        }

        /// <summary>
        /// When enabled, LiDAR depth will be used instead of Lightship depth on devices where LiDAR is available.
        /// Features unique to the LightshipOcclusionExtension cannot be used.
        /// </summary>
        /// <remarks>
        /// When enabled in experiences with meshing, the XROcclusionSubsystem must also be running in order to
        /// generate meshes.
        /// </remarks>
        public bool PreferLidarIfAvailable
        {
            get => _preferLidarIfAvailable;
            set => _preferLidarIfAvailable = value;
        }

        /// <summary>
        /// When enabled, Lightship's meshing features can be used via ARFoundation. Additional mesh features unique
        /// to Lightship can be configured in the LightshipMeshingExtension component.
        /// </summary>
        public bool UseLightshipMeshing
        {
            get => _useLightshipMeshing;
            set => _useLightshipMeshing = value;
        }

        /// <summary>
        /// When enabled, Lightship's semantic segmentation features can be used.
        /// </summary>
        public bool UseLightshipSemanticSegmentation
        {
            get => _useLightshipSemanticSegmentation;
            set => _useLightshipSemanticSegmentation = value;
        }

        /// <summary>
        /// When enabled, Lightship's scanning features can be used.
        /// </summary>
        public bool UseLightshipScanning
        {
            get => _useLightshipScanning;
            set => _useLightshipScanning = value;
        }

        /// <summary>
        /// When enabled, Lightship VPS can be used.
        /// </summary>
        public bool UseLightshipPersistentAnchor
        {
            get => _useLightshipPersistentAnchor;
            set => _useLightshipPersistentAnchor = value;
        }

        /// <summary>
        /// When true, Lightship's object detection features can be used.
        /// </summary>
        public bool UseLightshipObjectDetection
        {
            get => _useLightshipObjectDetection;
            set => _useLightshipObjectDetection = value;
        }

        /// <summary>
        /// When true, Lightship's World Positioning System (WPS) feature can be used.
        /// </summary>
        public bool UseLightshipWorldPositioning
        {
            get => _useLightshipWorldPositioning;
            set => _useLightshipWorldPositioning = value;
        }

        /// <summary>
        /// Source of location and compass data fetched from the Niantic.Lightship.AR.Input APIs
        /// </summary>
        public LocationDataSource LocationAndCompassDataSource
        {
            get => _locationAndCompassDataSource;
            set { _locationAndCompassDataSource = value; }
        }

        /// <summary>
        /// Values returned by location service when in Spoof mode
        /// </summary>
        public SpoofLocationInfo SpoofLocationInfo
        {
            get => _spoofLocationInfo;
        }

        /// <summary>
        /// Values returned by compass service when in Spoof mode
        /// </summary>
        public SpoofCompassInfo SpoofCompassInfo
        {
            get => _spoofCompassInfo;
        }

        /// <summary>
        /// The highest log level to print for Unity logger
        /// </summary>
        public LogLevel UnityLightshipLogLevel
        {
            get => _unityLogLevel;
            set => _unityLogLevel = value;
        }

        /// <summary>
        /// The highest log level to print for a file logger
        /// </summary>
        public LogLevel FileLightshipLogLevel
        {
            get => _fileLogLevel;
            set => _fileLogLevel = value;
        }

        /// <summary>
        /// The highest log level to print for the stdout logger - typically for internal testing. Keep this off unless
        /// you know what you are looking for
        /// </summary>
        public LogLevel StdOutLightshipLogLevel
        {
            get => _stdoutLogLevel;
            set => _stdoutLogLevel = value;
        }

        public LightshipSimulationParams LightshipSimulationParams
        {
            get
            {
                if (_lightshipSimulationParams == null)
                {
                    _lightshipSimulationParams = new LightshipSimulationParams();
                }

                return _lightshipSimulationParams;
            }
        }

        public bool UsePlayback
        {
            get => _playbackSettings.UsePlayback;
            set => _playbackSettings.UsePlayback = value;
        }

        public string PlaybackDatasetPath
        {
            get => _playbackSettings.PlaybackDatasetPath;
            set => _playbackSettings.PlaybackDatasetPath = value;
        }

        public bool RunPlaybackManually
        {
            get => _playbackSettings.RunManually;
            set => _playbackSettings.RunManually = value;
        }

        public bool LoopPlaybackInfinitely
        {
            get => _playbackSettings.LoopInfinitely;
            set => _playbackSettings.LoopInfinitely = value;
        }

        public int StartFrame
        {
            get => _playbackSettings.StartFrame;
            set => _playbackSettings.StartFrame = value;
        }

        public int EndFrame
        {
            get => _playbackSettings.EndFrame;
            set => _playbackSettings.EndFrame = value;
        }

        internal EndpointSettings EndpointSettings
        {
            get => _endpointSettings;
        }

        internal TestSettings TestSettings
        {
            get => _testSettings;
        }

        internal ILightshipPlaybackSettings PlaybackSettings
        {
            get => _playbackSettings;
        }

        internal RuntimeLightshipSettings()
        {
            _playbackSettings = new OverloadPlaybackSettings();
            _lightshipSimulationParams = new LightshipSimulationParams();
            _testSettings = new TestSettings();
            _endpointSettings = EndpointSettings.GetDefaultEnvironmentConfig();

            _spoofLocationInfo = SpoofLocationInfo.Default;
            _spoofCompassInfo = SpoofCompassInfo.Default;
        }

        internal RuntimeLightshipSettings(LightshipSettings source)
        {
            CopyFrom(source);
        }

        internal void CopyFrom(LightshipSettings source)
        {
            ApiKey = source.ApiKey;
            UseLightshipDepth = source.UseLightshipDepth;
            PreferLidarIfAvailable = source.PreferLidarIfAvailable;
            UseLightshipMeshing = source.UseLightshipMeshing;
            UseLightshipPersistentAnchor = source.UseLightshipPersistentAnchor;
            UseLightshipSemanticSegmentation = source.UseLightshipSemanticSegmentation;
            UseLightshipScanning = source.UseLightshipScanning;
            UseLightshipObjectDetection = source.UseLightshipObjectDetection;
            UseLightshipWorldPositioning = source.UseLightshipWorldPositioning;
            LocationAndCompassDataSource = source.LocationAndCompassDataSource;

            _spoofLocationInfo = new SpoofLocationInfo(source.SpoofLocationInfo);
            _spoofCompassInfo = new SpoofCompassInfo(source.SpoofCompassInfo);

            _authEnvironment = source.AuthEnvironment;

            // Only copy the developer authentication settings if the user has left developer authentication enabled
            // and is not using an API key
            if (source.UseDeveloperAuthentication && string.IsNullOrEmpty(source.ApiKey))
            {
                _accessToken = source.AccessToken;
                _accessExpiresAt = source.AccessExpiresAt;
                _refreshToken = source.RefreshToken;
                _refreshExpiresAt = source.RefreshExpiresAt;
            }

            UnityLightshipLogLevel = source.UnityLightshipLogLevel;
            FileLightshipLogLevel = source.FileLightshipLogLevel;
            StdOutLightshipLogLevel = source.StdOutLightshipLogLevel;

            var activePlaybackSettings =
                Application.isEditor
                    ? source.EditorPlaybackSettings
                    : source.DevicePlaybackSettings;

            _playbackSettings = new OverloadPlaybackSettings(activePlaybackSettings);
            _testSettings = new TestSettings(source.TestSettings);
            _endpointSettings = new EndpointSettings(source.EndpointSettings);

            _lightshipSimulationParams = new LightshipSimulationParams(source.LightshipSimulationParams);
        }

        [Obsolete("Use the parameter-less constructor with object initializers instead")]
        internal static RuntimeLightshipSettings _CreateRuntimeInstance
        (
            bool enableDepth = false,
            bool enableMeshing = false,
            bool enablePersistentAnchors = false,
            bool usePlayback = false,
            string playbackDataset = "",
            bool runPlaybackManually = false,
            bool loopPlaybackInfinitely = false,
            string apiKey = "",
            bool enableSemanticSegmentation = false,
            bool preferLidarIfAvailable = false,
            bool enableScanning = false,
            bool enableObjectDetection = false,
            bool enableWorldPositioning = false,
            LogLevel unityLogLevel = LogLevel.Debug,
            EndpointSettings endpointSettings = null,
            LogLevel stdoutLogLevel = LogLevel.Off,
            LogLevel fileLogLevel = LogLevel.Off,
            bool disableTelemetry = true,
            bool tickPamOnUpdate = true,
            LightshipSimulationParams simulationParams = null,
            int startFrame = 0,
            int endFrame = -1
        )
        {
            var settings =
                new RuntimeLightshipSettings
                {
                    ApiKey = apiKey,
                    UseLightshipDepth = enableDepth,
                    PreferLidarIfAvailable = preferLidarIfAvailable,
                    UseLightshipMeshing = enableMeshing,
                    UseLightshipPersistentAnchor = enablePersistentAnchors,
                    UseLightshipSemanticSegmentation = enableSemanticSegmentation,
                    UseLightshipScanning = enableScanning,
                    UseLightshipObjectDetection = enableObjectDetection,
                    UseLightshipWorldPositioning = enableWorldPositioning,
                    UnityLightshipLogLevel = unityLogLevel,
                    FileLightshipLogLevel = fileLogLevel,
                    StdOutLightshipLogLevel = stdoutLogLevel,
                    UsePlayback = usePlayback,
                    PlaybackDatasetPath = playbackDataset,
                    RunPlaybackManually = runPlaybackManually,
                    LoopPlaybackInfinitely = loopPlaybackInfinitely,
                    _playbackSettings =
                        new OverloadPlaybackSettings
                        {
                            UsePlayback = usePlayback,
                            PlaybackDatasetPath = playbackDataset,
                            RunManually = runPlaybackManually,
                            LoopInfinitely = loopPlaybackInfinitely,
                            StartFrame = startFrame,
                            EndFrame = endFrame
                        },
                    _endpointSettings = endpointSettings ?? EndpointSettings.GetDefaultEnvironmentConfig(),
                    _testSettings =
                        new TestSettings
                        {
                            DisableTelemetry = disableTelemetry,
                            TickPamOnUpdate = tickPamOnUpdate
                        }
                };

            simulationParams ??= new LightshipSimulationParams();
            settings._lightshipSimulationParams = simulationParams;

            return settings;
        }

        public void UpdateAccess(string accessToken, int accessExpiresAt, string refreshToken, int refreshExpiresAt)
        {
            _refreshToken = refreshToken;
            _accessToken = accessToken;
            _accessExpiresAt = accessExpiresAt;
            _refreshExpiresAt = refreshExpiresAt;

            AuthGatewayUtils.Instance.LogSettings(this, "Updated runtime");

            if (LightshipUnityContext.UnityContextHandle != IntPtr.Zero)
            {
                // Pass the access token to native ARDK code.
                // Note: ARDK native can also receive the refresh token, but we don't want to set it here
                // (in Unity, we run the refresh loop in C#).
                Metadata.SetAccessToken(_accessToken);
            }
        }

        public override string ToString()
        {
            return
                $"{GetType()}: \n" +
                "\t ApiKey: " + _apiKey + "\n" +
                "\t UseLightshipDepth: " + UseLightshipDepth + "\n" +
                "\t PreferLidarIfAvailable: " + PreferLidarIfAvailable + "\n" +
                "\t UseLightshipMeshing: " + UseLightshipMeshing + "\n" +
                "\t UseLightshipPersistentAnchor: " + UseLightshipPersistentAnchor + "\n" +
                "\t UseLightshipSemanticSegmentation: " + UseLightshipSemanticSegmentation + "\n" +
                "\t UseLightshipScanning: " + UseLightshipScanning + "\n" +
                "\t UseLightshipObjectDetection: " + UseLightshipObjectDetection + "\n" +
                "\t UseLightshipWorldPositioning: " + UseLightshipWorldPositioning + "\n" +
                "\t UnityLogLevel: " + UnityLightshipLogLevel;
        }

        private void SetAccess(string accessToken)
        {
            if (_accessToken != accessToken)
            {
                _accessToken = accessToken;
                _accessExpiresAt = AuthGatewayUtils.Instance.DecodeJwtTokenBody(accessToken)?.exp ?? 0;
                AuthGatewayUtils.Instance.LogToken("Updated runtime access", _accessToken);

                if (LightshipUnityContext.UnityContextHandle != IntPtr.Zero)
                {
                    // Pass the access token to native ARDK code.
                    // Note: ARDK native can also receive the refresh token, but we don't want to set it here
                    // (in Unity, we run the refresh loop in C#).
                    Metadata.SetAccessToken(_accessToken);
                }
            }
        }

        private void SetRefresh(string refreshToken)
        {
            if (_refreshToken != refreshToken)
            {
                _refreshToken = refreshToken;
                _refreshExpiresAt = AuthGatewayUtils.Instance.DecodeJwtTokenBody(refreshToken)?.exp ?? 0;
                AuthGatewayUtils.Instance.LogToken("Updated runtime refresh", _refreshToken);
                AuthRuntimeRefreshManager.RestartRefreshLoop();
            }
        }
    }
}
