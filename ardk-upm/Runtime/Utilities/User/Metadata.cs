// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf;
using Niantic.ARDK.AR.Protobuf;
using Niantic.Lightship.AR.Utilities.Device;
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities.User
{
    internal static class Metadata
    {
        private const string ArdkVersion = "3.0.0-232302";

        private const string UserIdHeaderKey = "x-ardk-userid";
        private const string ClientIdHeaderKey = "x-ardk-clientid";
        private const string ArClientEnvelopeHeaderKey = "x-ardk-clientenvelope";

        private const string ClientIdFileName = "g453uih2w348";

        static Metadata()
        {
            ClientId = GetOrGenerateClientId();

            ApplicationId = Application.identifier;
            Platform = GetPlatform();
            Manufacturer = GetManufacturer();
            DeviceModel = SystemInfo.deviceModel;
            Version = ArdkVersion;
            AppInstanceId = Guid.NewGuid().ToString();
        }
        
        public static string ApplicationId { get; }
        public static string Platform { get; }
        public static string Manufacturer { get; }
        public static string ClientId { get; }
        public static string DeviceModel { get; }
        public static string Version { get; }
        public static string AppInstanceId { get; }
        public static string UserId { get; private set; }
        public static ARClientEnvelope.Types.AgeLevel AgeLevel { get; private set; }

        public static Dictionary<string, string> GetApiGatewayHeaders(string requestId = null, string userId = null)
        {
            // TODO(AR-17030) copy these from native side.
            requestId ??= string.Empty;
            var gatewayHeaders = new Dictionary<string, string>();
            gatewayHeaders.Add(ArClientEnvelopeHeaderKey, ConvertToBase64(GetArClientEnvelopeAsJson(requestId)));
            gatewayHeaders.Add(ClientIdHeaderKey, ClientId);
            if(!string.IsNullOrWhiteSpace(userId))
            {
                gatewayHeaders.Add(UserIdHeaderKey, userId);
            }

            return gatewayHeaders;
        }

        private static string ConvertToBase64(string stringToConvert)
        {
            return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(stringToConvert));
        }

        public static string GetArClientEnvelopeAsJson(string requestId)
        {
            requestId ??= string.Empty;

            ARClientEnvelope clientEnvelope = new ARClientEnvelope()
            {
                AgeLevel = ARClientEnvelope.Types.AgeLevel.Unknown,
                ArCommonMetadata = GetArCommonMetadata(requestId),
            };

            return JsonFormatter.Default.Format(clientEnvelope);
        }

        public static void SetUserId(string userId)
        {
            if (userId == null)
            {
                throw new ArgumentNullException(nameof(userId));
            }

            // We do allow for string.Empty to be set as the userId
            userId = userId.Trim();
            UserId = userId;
        }

        public static void ClearUserId()
        {
            SetUserId(string.Empty);
        }

        public static void SetUserInfo(string userId, ARClientEnvelope.Types.AgeLevel ageLevel)
        {
            SetUserId(userId);
            AgeLevel = ageLevel;
        }

        public static void ClearUserInfo()
        {
            SetUserInfo(string.Empty, ARClientEnvelope.Types.AgeLevel.Unknown);
        }

        private static void SetAgeLevel(ARClientEnvelope.Types.AgeLevel ageLevel)
        {
            AgeLevel = ageLevel;
        }

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
                Debug.Log($"Retrieved ClientId = {clientId}");
                return clientId;
            }

            clientId = Guid.NewGuid().ToString();
            Debug.Log($"Creating new clientId: {clientId}");
            clientIdFileTracker.WriteData(clientId);
            return clientId;
        }



        private static string GetPlatform()
        {
            string operatingSystemWithApi = SystemInfo.operatingSystem;

            // if editor, return Unity version
            // example: UnityEditor-2021.3.17f1
            if (IsEditor())
            {
                return $"UnityEditor-{Application.unityVersion}";
            }

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

        /// <summary>
        /// INTERNAL FOR TESTING ONLY. DO NOT USE DIRECTLY.
        /// </summary>
        /// <param name="operatingSystemWithApi"></param>
        /// <returns></returns>
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
        /// <returns></returns>
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

        private static bool IsEditor()
        {
            return Application.platform == RuntimePlatform.LinuxEditor
                || Application.platform == RuntimePlatform.OSXEditor
                || Application.platform == RuntimePlatform.WindowsEditor;
        }

        private static bool IsIphone()
        {
            // SystemInfo.operatingSystemFamily = Other for iphones.
            return Application.platform == RuntimePlatform.IPhonePlayer;
        }

        private static bool IsAndroid()
        {
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

        internal static ARCommonMetadata GetArCommonMetadata(string requestId)
        {
            ARCommonMetadata commonMetadata = new ARCommonMetadata()
            {
                ApplicationId = ApplicationId,
                Manufacturer = Manufacturer,
                Platform = Platform,
                ClientId = ClientId,
                ArdkVersion = Version,
                ArdkAppInstanceId = AppInstanceId,
                RequestId = requestId,
                DeviceModel = DeviceModel,
            };

            return commonMetadata;
        }
    }
}
