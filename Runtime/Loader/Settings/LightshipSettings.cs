// Copyright 2022-2024 Niantic.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Loader
{
    /// <summary>
    /// Build time settings for Lightship AR. These are serialized to an asset file and can only be altered
    /// via the Unity Inspector window.
    /// </summary>
    [Serializable]
    [XRConfigurationData("Niantic Lightship SDK", SettingsKey)]
    public partial class LightshipSettings : ScriptableObject
    {
        private const string AssetsPath = "Assets";
        private const string AssetsRelativeSettingsPath = "XR/Settings";

        public const string SettingsKey = "Niantic.Lightship.AR.LightshipSettings";

        [SerializeField, Tooltip("This should match an API Key found in your Niantic Lightship developer account")]
        private string _apiKey = string.Empty;

        [SerializeField]
        [Tooltip
            (
                "When enabled, Lightship's depth and occlusion features can be used via ARFoundation. " +
                "Additional occlusion features unique to Lightship can be configured in the " +
                "LightshipOcclusionExtension component."
            )
        ]
        private bool _useLightshipDepth = true;

        [SerializeField]
        [Tooltip
            (
                "When enabled, LiDAR depth will be used instead of Lightship depth on devices where LiDAR is " +
                "available. Features unique to the LightshipOcclusionExtension cannot be used."
            )
        ]
        private bool _preferLidarIfAvailable = true;

        [SerializeField]
        [Tooltip
            (
                "When enabled, Lightship's meshing features can be used via ARFoundation. Additional mesh features " +
                "unique to Lightship can be configured in the LightshipMeshingExtension component."
            )
        ]
        private bool _useLightshipMeshing = true;

        [SerializeField, Tooltip("When enabled, Lightship's semantic segmentation features can be used.")]
        private bool _useLightshipSemanticSegmentation = true;

        [SerializeField, Tooltip("When enabled, Lightship's scanning features can be used.")]
        private bool _useLightshipScanning = true;

        [SerializeField, Tooltip("When enabled, Lightship VPS can be used.")]
        private bool _useLightshipPersistentAnchor = true;

        [SerializeField, Tooltip("When enabled, Lightship's object detection features can be used.")]
        private bool _useLightshipObjectDetection = true;

        [SerializeField, Tooltip("When enabled, Lightship's World Positioning System (WPS) feature can be used")]
        private bool _useLightshipWorldPositioning = true;

        [SerializeField, Tooltip("Source of location and compass data")]
        private LocationDataSource _locationAndCompassDataSource = LocationDataSource.Sensors;

        // Default to the Ferry Building location, so that non-zero location info is
        // surfaced even if the dev has not specified spoof location values.
        [SerializeField, Tooltip("Values returned by location service when in Spoof mode")]
        private SpoofLocationInfo _spoofLocationInfo = SpoofLocationInfo.Default;

        [SerializeField, Tooltip("Values returned by compass service when in Spoof mode")]
        private SpoofCompassInfo _spoofCompassInfo = SpoofCompassInfo.Default;

        [SerializeField, Tooltip("The lowest log level to print")]
        private LogLevel _unityLogLevel = LogLevel.Warn;

        [SerializeField, Tooltip("The lowest log level for file logger")]
        private LogLevel _fileLogLevel = LogLevel.Off;

        [SerializeField, Tooltip("The lowest log level to print")]
        private LogLevel _stdoutLogLevel = LogLevel.Off;

        [SerializeField]
        private LightshipSimulationParams _lightshipSimulationParams;

        [SerializeField]
        private DevicePlaybackSettings _devicePlaybackSettings;

        private EditorPlaybackSettings _editorPlaybackSettings;
        private EndpointSettings _endpointSettings;
        private TestSettings _testSettings;

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

                return EndpointSettings.ApiKey;
            }
        }

        /// <summary>
        /// When enabled, Lightship's depth and occlusion features can be used via ARFoundation. Additional occlusion
        /// features unique to Lightship can be configured in the LightshipOcclusionExtension component.
        /// </summary>
        public bool UseLightshipDepth => _useLightshipDepth;

        /// <summary>
        /// When enabled, Lightship's meshing features can be used via ARFoundation. Additional mesh features unique
        /// to Lightship can be configured in the LightshipMeshingExtension component.
        /// </summary>
        public bool UseLightshipMeshing => _useLightshipMeshing;

        /// <summary>
        /// When enabled, LiDAR depth will be used instead of Lightship depth on devices where LiDAR is available.
        /// Features unique to the LightshipOcclusionExtension cannot be used.
        /// </summary>
        /// <remarks>
        /// When enabled in experiences with meshing, the XROcclusionSubsystem must also be running in order to
        /// generate meshes.
        /// </remarks>
        public bool PreferLidarIfAvailable => _preferLidarIfAvailable;

        /// <summary>
        /// When enabled, Lightship VPS can be used.
        /// </summary>
        public bool UseLightshipPersistentAnchor => _useLightshipPersistentAnchor;

        /// <summary>
        /// When enabled, Lightship's semantic segmentation features can be used.
        /// </summary>
        public bool UseLightshipSemanticSegmentation => _useLightshipSemanticSegmentation;

        /// <summary>
        /// When enabled, Lightship's scanning features can be used.
        /// </summary>
        public bool UseLightshipScanning => _useLightshipScanning;

        /// <summary>
        /// When true, Lightship's object detection features can be used.
        /// </summary>
        public bool UseLightshipObjectDetection => _useLightshipObjectDetection;

        /// <summary>
        /// Source of location and compass data fetched from the Niantic.Lightship.AR.Input APIs
        /// </summary>
        public LocationDataSource LocationAndCompassDataSource
        {
            get => _locationAndCompassDataSource;
            set => _locationAndCompassDataSource = value;
        }

        /// <summary>
        /// Values returned by location service when in Spoof mode
        /// </summary>
        public SpoofLocationInfo SpoofLocationInfo
        {
            get
            {
                if (_spoofLocationInfo == null)
                {
                    // Default to the Ferry Building location, so that non-zero location info is
                    // surfaced even if the dev has not specified spoof location values.
                    _spoofLocationInfo =
                        new SpoofLocationInfo
                        {
                            Latitude = 37.795322f,
                            Longitude = -122.39243f,
                            Timestamp = 123456,
                            Altitude = 16,
                            HorizontalAccuracy = 10,
                            VerticalAccuracy = 10
                        };
                }

                return _spoofLocationInfo;
            }

            set => _spoofLocationInfo = value;
        }

        /// <summary>
        /// Values returned by compass service when in Spoof mode
        /// </summary>
        public SpoofCompassInfo SpoofCompassInfo
        {
            get
            {
                if (_spoofCompassInfo == null)
                {
                    _spoofCompassInfo = new SpoofCompassInfo();
                }

                return _spoofCompassInfo;
            }
            set => _spoofCompassInfo = value;
        }

        public bool UseLightshipWorldPositioning => _useLightshipWorldPositioning;

        /// <summary>
        /// The highest log level to print for Unity logger
        /// </summary>
        public LogLevel UnityLightshipLogLevel => _unityLogLevel;

        /// <summary>
        /// The highest log level to print for a file logger
        /// </summary>
        public LogLevel FileLightshipLogLevel => _fileLogLevel;

        /// <summary>
        /// The highest log level to print for the stdout logger - typically for internal testing. Keep this off unless
        /// you know what you are looking for
        /// </summary>
        public LogLevel StdOutLightshipLogLevel => _stdoutLogLevel;

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

        /// <summary>
        /// All Settings for Playback on the active platform
        /// </summary>
        private ILightshipPlaybackSettings PlaybackSettings
        {
            get => Application.isEditor ? EditorPlaybackSettings : DevicePlaybackSettings;
        }

        public bool UsePlayback => PlaybackSettings.UsePlayback;
        public string PlaybackDatasetPath => PlaybackSettings.PlaybackDatasetPath;
        public bool RunManually => PlaybackSettings.RunManually;
        public bool LoopInfinitely => PlaybackSettings.LoopInfinitely;

        public ILightshipPlaybackSettings DevicePlaybackSettings
        {
            get
            {
                return _devicePlaybackSettings;
            }
        }

        public ILightshipPlaybackSettings EditorPlaybackSettings
        {
            get
            {
                if (_editorPlaybackSettings == null)
                {
                    _editorPlaybackSettings = new EditorPlaybackSettings();
                }

                return _editorPlaybackSettings;

            }
        }

        internal EndpointSettings EndpointSettings
        {
            get
            {
                if (_endpointSettings == null)
                {
                    _endpointSettings = EndpointSettings.GetFromFileOrDefault();
                }

                return _endpointSettings;
            }
        }

        internal TestSettings TestSettings
        {
            get
            {
                if (_testSettings == null)
                {
                    _testSettings = new TestSettings { DisableTelemetry = false, TickPamOnUpdate = true };
                }

                return _testSettings;
            }
        }

#if !UNITY_EDITOR
        /// <summary>
        /// Static instance that will hold the runtime asset instance we created in our build process.
        /// </summary>
        private static LightshipSettings s_RuntimeInstance;
#endif

        /// <summary>
        /// On devices, this will be called when the application is loaded.
        /// </summary>
        private void Awake()
        {
#if !UNITY_EDITOR
            s_RuntimeInstance = this;
#endif
        }

        /// <summary>
        /// Accessor to Lightship settings asset instance.
        /// THIS SHOULD ONLY BE USED IN SPECIFIC SITUATIONS:
        ///     1) Editor classes that need to read/write values to the asset
        ///     2) By LightshipLoaderHelper to initialize the runtime settings on application load
        ///
        /// All other code should use LightshipLoaderHelper.ActiveSettings to get the settings,
        /// otherwise it will get the asset instance's values instead of the runtime instance's, which
        /// may be different in tests or if the dev has modified settings at runtime.
        /// </summary>
        public static LightshipSettings Instance => GetOrCreateAssetInstance();

        private static LightshipSettings GetOrCreateAssetInstance()
        {
            LightshipSettings settings = null;

#if UNITY_EDITOR
            if (!EditorBuildSettings.TryGetConfigObject(SettingsKey, out settings))
            {
                settings = CreateInstanceAsset();
            }
#else
            settings = s_RuntimeInstance;
            if (settings == null)
            {
                settings = CreateInstance<LightshipSettings>();
            }
#endif

            return settings;
        }

#if UNITY_EDITOR
        [MenuItem("Lightship/Settings", false, 1)]
        private static void FocusOnAsset()
        {
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Niantic Lightship SDK");
        }

        [MenuItem("Lightship/XR Plug-in Management", false, 0)]
        private static void OpenXRPluginManagement()
        {
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
        }

        [MenuItem("Lightship/Project Validation", false, 2)]
        private static void OpenProjectValidation()
        {
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Project Validation");
        }

        private static LightshipSettings CreateInstanceAsset()
        {
            // ensure all parent directories of settings asset exists
            var settingsPath = Path.Combine(AssetsPath, AssetsRelativeSettingsPath, "LightshipSettings.asset");
            var pathSplits = settingsPath.Split("/");
            var runningPath = pathSplits[0];
            for (var i = 1; i < pathSplits.Length - 1; i++)
            {
                var pathSplit = pathSplits[i];
                var nextPath = Path.Combine(runningPath, pathSplit);
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(runningPath, pathSplit);
                }

                runningPath = nextPath;
            }

            // create settings asset at specified path
            var settings = CreateInstance<LightshipSettings>();
            AssetDatabase.CreateAsset(settings, settingsPath);

            settings._endpointSettings = EndpointSettings.GetFromFileOrDefault();

            EditorBuildSettings.AddConfigObject(SettingsKey, settings, true);

            return settings;
        }
#endif

        internal static LightshipSettings CreateTestOnlyInstance
        (
            bool enableDepth = false,
            bool enableMeshing = false,
            bool enablePersistentAnchors = false,
            bool usePlayback = false,
            string playbackDataset = "",
            bool runPlaybackManually = false,
            bool loopPlaybackInfinitely = false,
            uint numberOfPlaybackLoops = 1,
            string apiKey = "",
            bool enableSemanticSegmentation = false,
            bool preferLidarIfAvailable = false,
            bool enableScanning = false,
            bool enableObjectDetection = false,
            LogLevel unityLogLevel = LogLevel.Debug,
            EndpointSettings endpointSettings = null,
            LogLevel stdoutLogLevel = LogLevel.Off,
            LogLevel fileLogLevel = LogLevel.Off,
            bool disableTelemetry = true,
            bool tickPamOnUpdate = true,
            LightshipSimulationParams simulationParams = null,
            LocationDataSource locationAndCompassDataSource = LocationDataSource.Sensors
        )
        {
            var settings = CreateInstance<LightshipSettings>();

            settings._apiKey = apiKey;
            settings._useLightshipDepth = enableDepth;
            settings._preferLidarIfAvailable = preferLidarIfAvailable;
            settings._useLightshipMeshing = enableMeshing;
            settings._useLightshipPersistentAnchor = enablePersistentAnchors;
            settings._useLightshipSemanticSegmentation = enableSemanticSegmentation;
            settings._useLightshipScanning = enableScanning;
            settings._useLightshipObjectDetection = enableObjectDetection;
            settings._locationAndCompassDataSource = locationAndCompassDataSource;
            settings._unityLogLevel = unityLogLevel;
            settings._fileLogLevel = fileLogLevel;
            settings._stdoutLogLevel = stdoutLogLevel;

            settings._devicePlaybackSettings =
                new DevicePlaybackSettings()
                {
                    UsePlayback = usePlayback,
                    PlaybackDatasetPath = playbackDataset,
                    RunManually = runPlaybackManually,
                    LoopInfinitely = loopPlaybackInfinitely,
                    NumberOfIterations = numberOfPlaybackLoops
                };

            if (endpointSettings == null)
            {
                settings._endpointSettings = EndpointSettings.GetDefaultEnvironmentConfig();
            }
            else
            {
                settings._endpointSettings = endpointSettings;
            }

            settings._testSettings =
                new TestSettings { DisableTelemetry = disableTelemetry, TickPamOnUpdate = tickPamOnUpdate };

            simulationParams ??= new LightshipSimulationParams();
            settings._lightshipSimulationParams = simulationParams;

            return settings;
        }

    }
}
