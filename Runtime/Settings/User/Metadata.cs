// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Niantic.Protobuf;
using JetBrains.Annotations;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.ARDK.AR.Protobuf;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities.Device;
using UnityEngine;

namespace Niantic.Lightship.AR.Settings
{
    /// <summary>
    /// Contains metadata and properties regarding the current instance of Ardk
    /// </summary>
    [PublicAPI]
    public static class Metadata
    {
        private const string ArdkVersion = "3.10.0-2411021056";

        private const string AuthorizationHeaderKey = "Authorization";
        private const string ApplicationIdHeaderKey = "x-ardk-application-id";
        private const string UserIdHeaderKey = "x-ardk-userid";
        private const string ClientIdHeaderKey = "x-ardk-clientid";
        private const string ArClientEnvelopeHeaderKey = "x-ardk-clientenvelope";

        private const string ClientIdFileName = "g453uih2w348";

        private static bool s_isUnityContextInitialized;
        static Metadata()
        {
            ClientId = GetOrGenerateClientId();

            ApplicationId = Application.identifier;
            Platform = GetPlatform();
            Manufacturer = GetManufacturer();
            DeviceModel = SystemInfo.deviceModel;
            Version = ArdkVersion;
            AppInstanceId = Guid.NewGuid().ToString();
            AgeLevel = ARClientEnvelope.Types.AgeLevel.Unknown;


            s_isUnityContextInitialized = false;
            LightshipUnityContext.OnUnityContextHandleInitialized += () =>
            {
                s_isUnityContextInitialized = true;
            };
        }

        /// <summary>
        /// Returns the ardk version that you are using
        /// </summary>
        [PublicAPI]
        public static string Version { get; }

        internal static string ApplicationId { get; }
        internal static string Platform { get; }
        internal static string Manufacturer { get; }
        internal static string ClientId { get; }
        internal static string DeviceModel { get; }
        internal static string AppInstanceId { get; }
        internal static string UserId { get; private set; }
        internal static ARClientEnvelope.Types.AgeLevel AgeLevel { get; private set; }

        internal static Dictionary<string, string> GetApiGatewayHeaders(string requestId = null)
        {
            requestId ??= string.Empty;
            var gatewayHeaders = new Dictionary<string, string>();
            gatewayHeaders.Add(ArClientEnvelopeHeaderKey, ConvertToBase64(GetArClientEnvelopeAsJson(requestId)));
            gatewayHeaders.Add(ClientIdHeaderKey, ClientId);
            gatewayHeaders.Add(ApplicationIdHeaderKey, ApplicationId);
            gatewayHeaders.Add(AuthorizationHeaderKey, LightshipSettingsHelper.ActiveSettings.ApiKey);

            if (!string.IsNullOrWhiteSpace(UserId))
            {
                gatewayHeaders.Add(UserIdHeaderKey, UserId);
            }

            return gatewayHeaders;
        }

        internal static ARCommonMetadata GetArCommonMetadata(string requestId)
        {
            ARCommonMetadata commonMetadata = new ARCommonMetadata()
            {
                ApplicationId = ApplicationId??"",
                Manufacturer = Manufacturer??"",
                Platform = Platform??"",
                ClientId = ClientId??"",
                ArdkVersion = Version??"",
                ArdkAppInstanceId = AppInstanceId??"",
                RequestId = requestId??"",
                DeviceModel = DeviceModel??"",
                UserId = UserId??"",
            };

            return commonMetadata;
        }

        internal static string GetArClientEnvelopeAsJson(string requestId)
        {
            requestId ??= string.Empty;

            ARClientEnvelope clientEnvelope = new ARClientEnvelope()
            {
                AgeLevel = AgeLevel,
                ArCommonMetadata = GetArCommonMetadata(requestId),
            };

            return JsonFormatter.Default.Format(clientEnvelope);
        }

        internal static void SetUserId(string userId)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            RunUnityContextCheck();

            userId = GetSanitizedUserId(userId);
            Lightship_ARDK_Unity_CoreContext_SetUserId(LightshipUnityContext.UnityContextHandle, userId);

            UserId = userId;
#endif
        }

        internal static void ClearUserId()
        {
            SetUserId(string.Empty);
        }


        /// <summary>
        /// For desktops, returns SystemInfo.deviceUniqueIdentifier since it will not change for the device.
        /// for ios/android, it checks if there is a cached guid. If not, it will generate a new one and cache the new value.
        /// </summary>
        /// <returns>the id of the device.</returns>
        private static string GetOrGenerateClientId()
        {
            // As of 22 May, 2023, for desktop unity systems, SystemInfo.deviceUniqueIdentifier returns the device Id.
            if (IsEditor())
            {
                return SystemInfo.deviceUniqueIdentifier;
            }

            // ios returns a different clientId for every new app install on running SystemInfo.deviceUniqueIdentifier;
            // android does what editors do. But we need to have consistent behaviour for mobile OSes for data science.
            var clientIdFileTracker = new FileTracker(Application.persistentDataPath, ClientIdFileName);

            var clientId = clientIdFileTracker.ReadData();
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                Log.Info($"Retrieved ClientId = {clientId}");
                return clientId;
            }

            clientId = Guid.NewGuid().ToString();
            Log.Info($"Creating new clientId: {clientId}");
            clientIdFileTracker.WriteData(clientId);
            return clientId;
        }

        /// <summary>
        /// Gets the platform of the system
        /// Unity: UnityEditor-2021.3.17f1
        /// Android: Android OS 13
        /// IPhone: iOS 16.4.1
        /// </summary>
        /// <returns>the platform for the developing/mobile OS run</returns>
        private static string GetPlatform()
        {
            // if editor, return Unity version
            // example: UnityEditor-2021.3.17f1
            if (IsEditor())
            {
                return $"UnityEditor-{Application.unityVersion}";
            }

            string operatingSystemWithApi = SystemInfo.operatingSystem;

            // else return ios os version/android os version.
            if (IsIphone())
            {
                return operatingSystemWithApi;
            }

            if (IsAndroid())
            {
                return GetAndroidOS(operatingSystemWithApi);
            }

            // Other
            return Application.platform.ToString();
        }

        private static string ConvertToBase64(string stringToConvert)
        {
            return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(stringToConvert));
        }

        /// <summary>
        /// INTERNAL FOR TESTING ONLY. DO NOT USE DIRECTLY.
        /// </summary>
        /// <param name="operatingSystemWithApi">format: "Android OS 13 / API-33 (TQ2A.230505.002/9891397)"</param>
        /// <returns>Android OS 13</returns>
        internal static string GetAndroidOS(string androidOperatingSystemWithApi)
        {
            // sample: Android OS 13 / API-33 (TQ2A.230505.002/9891397)
            string operatingSystemWithApi = androidOperatingSystemWithApi.Trim();
            int slashLocation = operatingSystemWithApi.IndexOf('/', StringComparison.Ordinal);
            var androidOSOnly = string.Empty;
            if (slashLocation > 0)
            {
                androidOSOnly = operatingSystemWithApi.Substring(0, slashLocation).Trim();
            }

            if (string.IsNullOrWhiteSpace(androidOSOnly))
            {
                return operatingSystemWithApi;
            }

            return androidOSOnly;
        }

        private static string GetManufacturer()
        {
            if ( IsIphone() || IsMac())
            {
                return "Apple";
            }

            if (IsAndroid())
            {
                return GetAndroidManufacturer(SystemInfo.deviceModel);
            }

            if (IsWindows())
            {
                return "Microsoft";
            }

            if (IsLinux())
            {
                return "Linux";
            }

            return "Other";
        }

        /// <summary>
        /// INTERNAL FOR TESTING ONLY. DO NOT USE DIRECTLY.
        /// </summary>
        /// <param name="androidDeviceModel">format: "GOOGLE Pixel 5"</param>
        /// <returns>GOOGLE</returns>
        internal static string GetAndroidManufacturer(string androidDeviceModel)
        {
            string deviceModel = androidDeviceModel.Trim();

            string androidManufacturer = string.Empty;
            int spaceLocation = deviceModel.IndexOf(' ', StringComparison.Ordinal);
            if (spaceLocation > 0)
            {
                androidManufacturer = deviceModel.Substring(0, spaceLocation).Trim();
            }

            if (string.IsNullOrWhiteSpace(androidManufacturer))
            {
                return androidDeviceModel;
            }
            return androidManufacturer;
        }

        internal static bool IsEditor()
        {
            return Application.platform == RuntimePlatform.LinuxEditor
                || Application.platform == RuntimePlatform.OSXEditor
                || Application.platform == RuntimePlatform.WindowsEditor;
        }

        internal static bool IsIphone()
        {
            // SystemInfo.operatingSystemFamily = Other for iphones.
            return Application.platform == RuntimePlatform.IPhonePlayer;
        }

        internal static bool IsAndroid()
        {
            // SystemInfo.operatingSystemFamily = Other for android.
            return Application.platform == RuntimePlatform.Android;
        }

        private static bool IsMac()
        {
            return SystemInfo.operatingSystemFamily == OperatingSystemFamily.MacOSX;
        }

        private static bool IsWindows()
        {
            return SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows;
        }

        private static bool IsLinux()
        {
            return SystemInfo.operatingSystemFamily == OperatingSystemFamily.Linux;
        }

        private static string GetSanitizedUserId(string userId)
        {
            if (userId == null)
            {
                throw new ArgumentNullException(nameof(userId));
            }

            // We allow for string.Empty to be set as the userId
            userId = userId.Trim();
            return userId;
        }

        private static void RunUnityContextCheck()
        {
            if (!s_isUnityContextInitialized)
            {
                throw new InvalidOperationException("Please call this API later in the app life cycle after the XRLoaders have been loaded.");
            }
        }

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_CoreContext_SetUserId(IntPtr unityContext, string userId);
    }
}
