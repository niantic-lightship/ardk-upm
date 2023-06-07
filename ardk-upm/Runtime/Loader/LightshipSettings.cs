using System;
using System.IO;
using System.Collections.Generic;
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
    [XRConfigurationData("Lightship SDK", SettingsKey)]
    public partial class LightshipSettings : ScriptableObject
    {
        private const string AssetsPath = "Assets";
        private const string AssetsRelativeSettingsPath = "XR/Settings/Lightship Settings.asset";
        private const string AuthorizationHeaderKey = "Authorization";
        private const string ClientIdHeaderKey = "x-ardk-clientid";
        private const string UserIdHeaderKey = "x-ardk-userid";
        private const string ApplicationIdKey = "x-ardk-application-id";

        public const string SettingsKey = "Niantic.Lightship.AR.LightshipSettings";

        private string _userId;
        private string _clientId;
        private string _ardkAppInstanceId;
        private string _applicationId;
        private string _ardkVersion;
        private string _manufacturer;
        private string _deviceModel;
        private string _platform;

        [SerializeField, Tooltip("This should match an API Key found in your Niantic Lightship developer account")]
        private string _apiKey = string.Empty;

        /// <summary>
        /// Api to get the lightship ApiKey.
        /// </summary>
        public string ApiKey => _apiKey;

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


        [SerializeField,
         Tooltip("When enabled, use Niantic's scanning subsystem provider")]
        private bool _useLightshipScanning = true;

        /// <summary>
        /// Use Lightship provider for scanning
        /// </summary>
        public bool UseLightshipScanning => _useLightshipScanning;

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
            // Currently some headers are set to empty strings until native
            // logic implements fetching these native values
            _userId = Guid.NewGuid().ToString();
            _clientId = string.Empty;
            _ardkAppInstanceId = string.Empty;
            _applicationId = Application.identifier;
            _ardkVersion = string.Empty;
            _manufacturer = string.Empty;
            _deviceModel = SystemInfo.operatingSystem;
            _platform = string.Empty;
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
                settings = CreateInstanceAsset();
            }
#else
            settings = s_RuntimeInstance;
            if (settings == null)
                settings = CreateInstance<LightshipSettings>();
#endif

            return settings;
        }

#if UNITY_EDITOR
        [MenuItem("Lightship/Settings", false, 1)]
        private static void FocusOnAsset()
        {
            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<LightshipSettings>(AssetDatabase.GetAssetPath(Instance));
        }

        private static LightshipSettings CreateInstanceAsset()
        {
            // ensure all parent directories of settings asset exists
            var settingsPath = Path.Combine(AssetsPath, AssetsRelativeSettingsPath);
            var pathSplits = settingsPath.Split("/");
            var runningPath = pathSplits[0];
            for (var i = 1; i < pathSplits.Length - 1; i++)
            {
                var pathSplit = pathSplits[i];
                var nextPath = Path.Combine(runningPath, pathSplit);
                if (!AssetDatabase.IsValidFolder(nextPath)) AssetDatabase.CreateFolder(runningPath, pathSplit);
                runningPath = nextPath;
            }

            // create settings asset at specified path
            var settings = CreateInstance<LightshipSettings>();
            AssetDatabase.CreateAsset(settings, settingsPath);
            EditorBuildSettings.AddConfigObject(SettingsKey, settings, true);
            return settings;
        }
#endif

        internal static LightshipSettings _CreateRuntimeInstance
        (
            bool enableDepth = false,
            uint depthFrameRate = 20,
            bool enableMeshing = false,
            bool enablePersistentAnchors = false,
            bool usePlaybackOnEditor = false,
            bool usePlaybackOnDevice = false,
            string playbackDataset = "",
            bool runPlaybackManually = false,
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
            settings._useLightshipScanning = enableScanning;
            settings._usePlaybackOnEditor = usePlaybackOnEditor;
            settings._usePlaybackOnDevice = usePlaybackOnDevice;
            settings._playbackDatasetPathEditor = playbackDataset;
            settings._playbackDatasetPathDevice = playbackDataset;
            settings._runPlaybackManuallyEditor = runPlaybackManually;
            settings._runPlaybackManuallyDevice = runPlaybackManually;
            settings._preferLidarIfAvailable = preferLidarIfAvailable;
            return settings;
        }

        internal ARCommonMetadataStruct GetCommonDataEnvelopeWithRequestIdAsStruct()
        {
            var metadata = new ARCommonMetadataStruct
            (
                _applicationId,
                _platform,
                _manufacturer,
                _deviceModel,
                _userId,
                _clientId,
                _ardkVersion,
                _ardkAppInstanceId,
                $"{Guid.NewGuid().ToString("N").ToLower()}"
            );
            return metadata;
        }

        internal Dictionary<string, string> GetApiGatewayHeaders()
        {
            var header = new Dictionary<string, string>
            {
                { AuthorizationHeaderKey, _apiKey },
                { ClientIdHeaderKey, _clientId },
                { UserIdHeaderKey, _userId },
                { ApplicationIdKey, _applicationId }
            };
            return header;
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
