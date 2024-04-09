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
    /// Build time settings for Lightship AR. These are serialized and available at runtime.
    /// </summary>
    /// <note>
    /// This object is specifically for build time settings, i.e. settings that cannot change at runtime.
    /// Values can only be set at construction time, or, for the asset instance, through the Inspector
    /// while in EditMode.
    /// </note>
    [Serializable]
    [XRConfigurationData("Niantic Lightship SDK", SettingsKey)]
    public partial class LightshipSettings : ScriptableObject
    {
        private const string AssetsPath = "Assets";
        private const string AssetsRelativeSettingsPath = "XR/Settings";
        private const string ConfigFileName = "ardkConfig.json";

        public const string SettingsKey = "Niantic.Lightship.AR.LightshipSettings";

        [SerializeField, Tooltip("This should match an API Key found in your Niantic Lightship developer account")]
        private string _apiKey = string.Empty;

        private ArdkConfiguration _ardkConfiguration;

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

                return _ardkConfiguration.ApiKey;
            }
        }

        [SerializeField]
        [Tooltip
            (
                "When enabled, Lightship's depth and occlusion features can be used via ARFoundation. " +
                "Additional occlusion features unique to Lightship can be configured in the " +
                "LightshipOcclusionExtension component."
            )
        ]
        private bool _useLightshipDepth = true;

        /// <summary>
        /// When enabled, Lightship's depth and occlusion features can be used via ARFoundation. Additional occlusion
        /// features unique to Lightship can be configured in the LightshipOcclusionExtension component.
        /// </summary>
        public bool UseLightshipDepth => _useLightshipDepth;

        [SerializeField]
        [Tooltip
            (
                "When enabled, Lightship's meshing features can be used via ARFoundation. Additional mesh features " +
                "unique to Lightship can be configured in the LightshipMeshingExtension component."
            )
        ]
        private bool _useLightshipMeshing = true;

        /// <summary>
        /// When enabled, Lightship's meshing features can be used via ARFoundation. Additional mesh features unique
        /// to Lightship can be configured in the LightshipMeshingExtension component.
        /// </summary>
        public bool UseLightshipMeshing => _useLightshipMeshing;

        [SerializeField]
        [Tooltip
            (
                "When enabled, LiDAR depth will be used instead of Lightship depth on devices where LiDAR is " +
                "available. Features unique to the LightshipOcclusionExtension cannot be used."
            )
        ]
        private bool _preferLidarIfAvailable = true;

        /// <summary>
        /// When enabled, LiDAR depth will be used instead of Lightship depth on devices where LiDAR is available.
        /// Features unique to the LightshipOcclusionExtension cannot be used.
        /// </summary>
        /// <remarks>
        /// When enabled in experiences with meshing, the XROcclusionSubsystem must also be running in order to
        /// generate meshes.
        /// </remarks>
        public bool PreferLidarIfAvailable => _preferLidarIfAvailable;

        [SerializeField, Tooltip("When enabled, Lightship VPS can be used.")]
        private bool _useLightshipPersistentAnchor = true;

        /// <summary>
        /// When enabled, Lightship VPS can be used.
        /// </summary>
        public bool UseLightshipPersistentAnchor => _useLightshipPersistentAnchor;

        [SerializeField,
         Tooltip("When enabled, Lightship's semantic segmentation features can be used.")]
        private bool _useLightshipSemanticSegmentation = true;

        /// <summary>
        /// When enabled, Lightship's semantic segmentation features can be used.
        /// </summary>
        public bool UseLightshipSemanticSegmentation => _useLightshipSemanticSegmentation;

        [SerializeField, Tooltip("When enabled, Lightship's scanning features can be used.")]
        private bool _useLightshipScanning = true;

        /// <summary>
        /// When enabled, Lightship's scanning features can be used.
        /// </summary>
        public bool UseLightshipScanning => _useLightshipScanning;

        [SerializeField, Tooltip("When true, Lightship's object detection features can be used.")]
        private bool _useLightshipObjectDetection = true;

        /// <summary>
        /// When true, Lightship's object detection features can be used.
        /// </summary>
        public bool UseLightshipObjectDetection => _useLightshipObjectDetection;

        [SerializeField, Tooltip("The lowest log level to print")]
        private LogLevel _unityLogLevel = LogLevel.Warn;

        [SerializeField,
         Tooltip("The lowest log level for file logger")]
        private LogLevel _fileLogLevel = LogLevel.Off;

        [SerializeField,
         Tooltip("The lowest log level to print")]
        private LogLevel _stdoutLogLevel = LogLevel.Off;

        /// <summary>
        /// The highest log level to print for Unity logger
        /// </summary>
        internal LogLevel UnityLightshipLogLevel => _unityLogLevel;

        /// <summary>
        /// The highest log level to print for a file logger
        /// </summary>
        internal LogLevel FileLightshipLogLevel => _fileLogLevel;

        /// <summary>
        /// The highest log level to print for the stdout logger - typically for internal testing. Keep this off unless
        /// you know what you are looking for
        /// </summary>
        internal LogLevel StdOutLightshipLogLevel => _stdoutLogLevel;

        [SerializeField]
        private LightshipSimulationParams _lightshipSimulationParams;

        public LightshipSimulationParams LightshipSimulationParams => _lightshipSimulationParams;

        /// <summary>
        /// All Settings for Playback in the Unity Editor
        /// </summary>
        private ILightshipPlaybackSettings PlaybackSettings
        {
            get
            {
                if (_overloadPlaybackSettings != null)
                {
                    return _overloadPlaybackSettings;
                }

                return Application.isEditor ? EditorPlaybackSettings : DevicePlaybackSettings;
            }
        }

        [SerializeField]
        private DevicePlaybackSettings _devicePlaybackSettings = new();

        private EditorPlaybackSettings _editorPlaybackSettings;
        private OverloadPlaybackSettings _overloadPlaybackSettings;

        public bool UsePlayback => PlaybackSettings.UsePlayback;

        public string PlaybackDatasetPath => PlaybackSettings.PlaybackDatasetPath;

        public bool RunManually => PlaybackSettings.RunManually;

        public bool LoopInfinitely => PlaybackSettings.LoopInfinitely;

        public ILightshipPlaybackSettings DevicePlaybackSettings
        {
            get
            {
                if (_overloadPlaybackSettings != null)
                {
                    return _overloadPlaybackSettings;
                }

                return _devicePlaybackSettings;
            }
        }

        public ILightshipPlaybackSettings EditorPlaybackSettings
        {
            get
            {
                if (_overloadPlaybackSettings != null)
                {
                    return _overloadPlaybackSettings;
                }

                if (_editorPlaybackSettings == null)
                {
                    _editorPlaybackSettings = new EditorPlaybackSettings();
                }

                return _editorPlaybackSettings;
            }
        }

        private LightshipTestSettings _testSettings;

        internal LightshipTestSettings TestSettings
        {
            get
            {
                if (_testSettings == null)
                {
                    _testSettings = new LightshipTestSettings(false, true);
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

        private void Awake()
        {
#if !UNITY_EDITOR
            s_RuntimeInstance = this;
#endif
        }

        /// <summary>
        /// Accessor to Lightship settings.
        /// </summary>
        public static LightshipSettings Instance => GetOrCreateInstance();

        private static LightshipSettings GetOrCreateInstance()
        {
            LightshipSettings settings = null;

#if UNITY_EDITOR
            if (!EditorBuildSettings.TryGetConfigObject(SettingsKey, out settings))
            {
                Log.Info("No LightshipSettings.Instance found, creating new one.");
                settings = CreateInstanceAsset();
            }
#else
            settings = s_RuntimeInstance;
            if (settings == null)
                settings = CreateInstance<LightshipSettings>();
#endif
            if (settings._ardkConfiguration == null)
            {
                var gotConfigurationFromJson =
                    ArdkConfiguration.TryGetConfigurationFromJson
                    (
                        Path.Combine(Application.streamingAssetsPath, ConfigFileName),
                        out ArdkConfiguration parsedConfig
                    );

                settings._ardkConfiguration =
                    gotConfigurationFromJson ? parsedConfig : ArdkConfiguration.GetDefaultEnvironmentConfig();
            }

            ValidateApiKey(settings.ApiKey);

            return settings;
        }


        private static void ValidateApiKey(string apiKey)
        {

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Log.Error("Please provide a Lightship API key that has been created for your account for a project at https://lightship.dev/account/projects");
            }

            if (apiKey is { Length: > 512 })
            {
                Log.Error("Provided Lightship API key is too long");
            }
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

            EditorBuildSettings.AddConfigObject(SettingsKey, settings, true);

            return settings;
        }
#endif

        /// <summary>
        /// FOR TESTING PURPOSES ONLY. DO NOT TRY TO USE THIS NORMALLY. YOU WILL BREAK THE GENERAL SETUP.
        /// It does not use the configuration provider.
        /// </summary>
        internal static LightshipSettings _CreateRuntimeInstance
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
            ArdkConfiguration ardkConfiguration = null,
            LogLevel stdoutLogLevel = LogLevel.Off,
            LogLevel fileLogLevel = LogLevel.Off,
            bool disableTelemetry = true,
            bool tickPamOnUpdate = true
        )
        {
            var settings = CreateInstance<LightshipSettings>();

            settings._apiKey = apiKey;
            settings._useLightshipDepth = enableDepth;
            settings._useLightshipMeshing = enableMeshing;
            settings._useLightshipPersistentAnchor = enablePersistentAnchors;
            settings._useLightshipSemanticSegmentation = enableSemanticSegmentation;
            settings._useLightshipScanning = enableScanning;
            settings._useLightshipObjectDetection = enableObjectDetection;
            settings._unityLogLevel = unityLogLevel;
            settings._fileLogLevel = fileLogLevel;
            settings._stdoutLogLevel = stdoutLogLevel;

            settings._overloadPlaybackSettings =
                new OverloadPlaybackSettings
                {
                    UsePlayback = usePlayback,
                    PlaybackDatasetPath = playbackDataset,
                    RunManually = runPlaybackManually,
                    LoopInfinitely = loopPlaybackInfinitely,
                    NumberOfIterations = numberOfPlaybackLoops
                };

            settings._preferLidarIfAvailable = preferLidarIfAvailable;

            if (ardkConfiguration == null)
            {
                settings._ardkConfiguration = ArdkConfiguration.TryGetConfigurationFromJson(
                    Path.Combine(Application.streamingAssetsPath, ConfigFileName),
                    out ArdkConfiguration parsedConfig)
                    ? parsedConfig
                    : ArdkConfiguration.GetDefaultEnvironmentConfig();
            }
            else
            {
                settings._ardkConfiguration = ardkConfiguration;
            }

            settings._testSettings = new LightshipTestSettings(disableTelemetry, tickPamOnUpdate);

            return settings;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _lightshipSimulationParams.OnValidate();
        }
#endif
    }
}
