using System;
using System.IO;
using Newtonsoft.Json;
using Niantic.Lightship.AR.Editor;
using Niantic.Lightship.AR.Settings.User;
using UnityEditor;
using UnityEngine;
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
        /// Api to get the lightship ApiKey.
        /// </summary>
        public string ApiKey
        {
            get {
                if (!string.IsNullOrWhiteSpace(_apiKey))
                {
                    // ensure that the config provider's key is overridden in case the user has provided their own
                    return _apiKey;
                }

                return _ardkConfiguration.ApiKey;
            }
        }

        [SerializeField, Tooltip("When enabled, use Niantic's depth provider instead of the native platform's")]
        private bool _useLightshipDepth = true;

        /// <summary>
        /// Layer used for the depth
        /// </summary>
        public bool UseLightshipDepth => _useLightshipDepth;

        [SerializeField, Tooltip("When enabled, use Niantic's meshing provider instead of the native platform's")]
        private bool _useLightshipMeshing = true;

        /// <summary>
        /// Layer used for the meshing
        /// </summary>
        public bool UseLightshipMeshing => _useLightshipMeshing;

        [SerializeField, Tooltip("When enabled, prioritize using AR Foundation LiDAR depth " +
             "if LiDAR is available on device")]
        private bool _preferLidarIfAvailable = true;

        /// <summary>
        /// Layer used for the depth
        /// </summary>
        public bool PreferLidarIfAvailable => _preferLidarIfAvailable;

        [SerializeField, Tooltip("Frame rate at which to run depth inference")]
        private uint _lightshipDepthFrameRate = 20;

        /// <summary>
        /// Desired frame rate of the depth inference model
        /// </summary>
        public uint LightshipDepthFrameRate => _lightshipDepthFrameRate;

        [SerializeField,
         Tooltip("When enabled, use Niantic's persistent anchor provider instead of the native platform's")]
        private bool _useLightshipPersistentAnchor = true;

        /// <summary>
        /// Layer used for the persistent anchor
        /// </summary>
        public bool UseLightshipPersistentAnchor => _useLightshipPersistentAnchor;

        [SerializeField,
         Tooltip("When enabled, use Niantic's semantic segmentation subsystem provider")]
        private bool _useLightshipSemanticSegmentation = true;

        /// <summary>
        /// Use Lightship provider for semantic segmentation
        /// </summary>
        public bool UseLightshipSemanticSegmentation => _useLightshipSemanticSegmentation;

        [SerializeField, Tooltip("Frame rate at which to run semantic segmentation")]
        private uint _LightshipSemanticSegmentationFrameRate = 10;

        /// <summary>
        /// Desired frame rate of the semantic segmentation model
        /// </summary>
        public uint LightshipSemanticSegmentationFrameRate => _LightshipSemanticSegmentationFrameRate;

        [SerializeField]
        private LightshipPlaybackSettings _editorPlaybackSettings = new();

        /// <summary>
        /// All Settings for Playback in the Unity Editor
        /// </summary>
        public LightshipPlaybackSettings EditorPlaybackSettings => _editorPlaybackSettings;

        /// <summary>
        /// Use Playback in Playmode in the Unity Editor
        /// </summary>
        public bool EditorPlaybackEnabled => _editorPlaybackSettings.UsePlayback && Application.isEditor;

        [SerializeField]
        private LightshipPlaybackSettings _devicePlaybackSettings = new();

        /// <summary>
        /// All Settings for Playback on devices, meaning iOS and Android
        /// </summary>
        public LightshipPlaybackSettings DevicePlaybackSettings => _devicePlaybackSettings;

        /// <summary>
        /// Use Playback running the app on device
        /// </summary>
        public bool DevicePlaybackEnabled => _devicePlaybackSettings.UsePlayback && !Application.isEditor;

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
                Debug.Log("No LightshipSettings.Instance found, creating new one.");
                settings = CreateInstanceAsset();
            }
#else
            settings = s_RuntimeInstance;
            if (settings == null)
                settings = CreateInstance<LightshipSettings>();
#endif
            if (settings._ardkConfiguration == null)
            {
                settings._ardkConfiguration = ArdkConfiguration.TryGetConfigurationFromJson(
                    Path.Combine(Application.streamingAssetsPath, ConfigFileName),
                    out ArdkConfiguration parsedConfig) ?
                        parsedConfig :
                        ArdkConfiguration.GetDefaultEnvironmentConfig();
            }

            ValidateApiKey(settings.ApiKey);

            return settings;
        }

        private static void ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogWarning("Please provide an ApiKey that has been created for your account for a project at https://lightship.dev/account/projects/");
            }

            if (apiKey != null && apiKey.Length > 512)
            {
                throw new InvalidOperationException("Api Key Length is too large");
            }
        }

#if UNITY_EDITOR
        [MenuItem("Lightship/Settings", false, 1)]
        private static void FocusOnAsset()
        {
            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<LightshipSettings>(AssetDatabase.GetAssetPath(Instance));
        }

        [MenuItem("Lightship/Project Validation", false, 1)]
        private static void OpenProjectValidation()
        {
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Project Validation");
        }

        private static LightshipSettings CreateInstanceAsset()
        {
            // ensure all parent directories of settings asset exists
            var settingsPath = Path.Combine(AssetsPath, AssetsRelativeSettingsPath, "Lightship Settings.asset");
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
            uint depthFrameRate = 20,
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
            uint semanticSegmentationFrameRate = 10,
            bool enableScanning = false)
        {
            var settings = CreateInstance<LightshipSettings>();

            settings._apiKey = apiKey;
            settings._useLightshipDepth = enableDepth;
            settings._lightshipDepthFrameRate = depthFrameRate;
            settings._useLightshipMeshing = enableMeshing;
            settings._useLightshipPersistentAnchor = enablePersistentAnchors;
            settings._useLightshipSemanticSegmentation = enableSemanticSegmentation;
            settings._LightshipSemanticSegmentationFrameRate = semanticSegmentationFrameRate;

            settings._editorPlaybackSettings.UsePlayback = usePlayback;
            settings._editorPlaybackSettings.PlaybackDatasetPath = playbackDataset;
            settings._editorPlaybackSettings.RunManually = runPlaybackManually;
            settings._editorPlaybackSettings.LoopInfinitely = loopPlaybackInfinitely;
            settings._editorPlaybackSettings.NumberOfIterations = numberOfPlaybackLoops;

            settings._devicePlaybackSettings.UsePlayback = usePlayback;
            settings._devicePlaybackSettings.PlaybackDatasetPath = playbackDataset;
            settings._devicePlaybackSettings.RunManually = runPlaybackManually;
            settings._devicePlaybackSettings.LoopInfinitely = loopPlaybackInfinitely;
            settings._devicePlaybackSettings.NumberOfIterations = numberOfPlaybackLoops;

            settings._preferLidarIfAvailable = preferLidarIfAvailable;

            settings._ardkConfiguration = ArdkConfiguration.TryGetConfigurationFromJson(
                Path.Combine(Application.streamingAssetsPath, ConfigFileName),
                out ArdkConfiguration parsedConfig) ?
                    parsedConfig :
                    ArdkConfiguration.GetDefaultEnvironmentConfig();

            return settings;
        }

        internal ARCommonMetadataStruct GetCommonDataEnvelopeWithRequestIdAsStruct(string requestId)
        {
            var metadata = new ARCommonMetadataStruct
            (
                Metadata.ApplicationId,
                Metadata.Platform,
                Metadata.Manufacturer,
                Metadata.DeviceModel,
                Metadata.UserId,
                Metadata.ClientId,
                Metadata.Version,
                Metadata.AppInstanceId,
                requestId
            );
            return metadata;
        }
    }

    [Serializable]
    internal struct ARCommonMetadataStruct
    {
        // DO NOT RENAME THESE VARIABLES EVEN WITH THE NEW STYLE
        public string application_id;
        public string platform;
        public string manufacturer;
        public string device_model;
        public string user_id;
        public string client_id;
        public string ardk_version;
        public string ardk_app_instance_id;
        public string request_id;

        public ARCommonMetadataStruct
        (
            string applicationID,
            string platform,
            string manufacturer,
            string deviceModel,
            string userID,
            string clientID,
            string ardkVersion,
            string ardkAppInstanceID,
            string requestID
        )
        {
            application_id = applicationID;
            this.platform = platform;
            this.manufacturer = manufacturer;
            device_model = deviceModel;
            user_id = userID;
            client_id = clientID;
            ardk_version = ardkVersion;
            ardk_app_instance_id = ardkAppInstanceID;
            request_id = requestID;
        }
    }

}
