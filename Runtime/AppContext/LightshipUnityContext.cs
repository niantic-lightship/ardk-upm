using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Niantic.Lightship.AR.Loader;
using UnityEngine;
using Niantic.Lightship.AR.PlatformAdapterManager;
using Niantic.Lightship.AR.Utilities.CTrace;
using Niantic.Lightship.AR.Settings.User;
using Telemetry;

namespace Niantic.Lightship.AR
{
    internal class LightshipUnityContext
    {
        // Pointer to the unity context
        internal static IntPtr UnityContextHandle { get; private set; } = IntPtr.Zero;

        // Temporarily exposing this so loaders can inject the PlaybackDatasetReader into the PAM
        // To remove once all subsystems are implemented via playback.
        internal static _PlatformAdapterManager PlatformAdapterManager { get; private set; }

        private static IntPtr s_propertyBagHandle = IntPtr.Zero;
        private static _ICTrace s_ctrace;
        private static _EnvironmentConfig s_environmentConfig;
        private static TelemetryService s_telemetryService;

        // Event triggered right before the context is destroyed. Used by internal code its lifecycle is not managed
        // by native UnityContext
        internal static event Action OnDeinitialized;
        internal static event Action OnUnityContextHandleInitialized;

        internal static void Initialize(LightshipSettings settings, bool isDeviceLidarSupported, bool isTest = false)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED

            if (!isTest)
            {
                // Cannot use Application.persistentDataPath in testing
                AnalyticsTelemetryPublisher telemetryPublisher = new AnalyticsTelemetryPublisher(
                    endpoint: settings.TelemetryEndpoint,
                    directoryPath: Path.Combine(Application.persistentDataPath, "telemetry"),
                    key: settings.TelemetryApiKey,
                    registerLogger: false);

                s_telemetryService = new TelemetryService(telemetryPublisher, settings.ApiKey);
            }

            if (UnityContextHandle != IntPtr.Zero)
            {
                Debug.LogWarning($"Cannot initialize {nameof(LightshipUnityContext)} as it is already initialized");
                return;
            }

            Debug.Log($"Initializing {nameof(LightshipUnityContext)}");
            s_environmentConfig = new _EnvironmentConfig
            {
                ApiKey = settings.ApiKey,
                ScanningEndpoint = settings.ScanningEndpoint,
                ScanningSqcEndpoint = settings.ScanningSqcEndpoint,
                SharedArEndpoint = settings.SharedArEndpoint,
                VpsEndpoint = settings.VpsEndpoint,
                VpsCoverageEndpoint = settings.VpsCoverageEndpoint,
                DefaultDepthSemanticsEndpoint = settings.DefaultDepthSemanticsEndpoint,
                FastDepthSemanticsEndpoint = settings.FastDepthSemanticsEndpoint,
                SmoothDepthSemanticsEndpoint = settings.SmoothDepthSemanticsEndpoint,
            };

            _DeviceInfo deviceInfo = new _DeviceInfo
            {
                AppId = Metadata.ApplicationId,
                Platform = Metadata.Platform,
                Manufacturer = Metadata.Manufacturer,
                ClientId = Metadata.ClientId,
                DeviceModel = Metadata.DeviceModel,
                Version = Metadata.Version,
                AppInstanceId = Metadata.AppInstanceId,
                DeviceLidarSupported = isDeviceLidarSupported,
            };
            UnityContextHandle = NativeApi.Lightship_ARDK_Unity_Context_Create(false, ref deviceInfo, ref s_environmentConfig);
            OnUnityContextHandleInitialized?.Invoke();

            var modelPath = Path.Combine(Application.streamingAssetsPath, "full_model.bin");
            Debug.Log("Model path: " + modelPath);

            s_propertyBagHandle = NativeApi.Lightship_ARDK_Unity_Property_Bag_Create(UnityContextHandle);
            NativeApi.Lightship_ARDK_Unity_Property_Bag_Put
            (
                s_propertyBagHandle,
                "depth_semantics_model_path",
                modelPath
            );

            s_ctrace = new _NativeCTrace();
            s_ctrace.InitializeCtrace();

            CreatePam(settings);
#endif
        }

        private static void CreatePam(LightshipSettings settings)
        {
            if (PlatformAdapterManager != null)
            {
                Debug.LogWarning("Cannot create PAM as it is already created");
                return;
            }

            // Create the PAM, which will create the SAH
            Debug.Log("Creating PAM");
            if (settings.EditorPlaybackEnabled || settings.DevicePlaybackEnabled)
            {
                PlatformAdapterManager =
                    _PlatformAdapterManager.Create<_NativeApi, _PlaybackSubsystemsDataAcquirer>(UnityContextHandle,
                        _PlatformAdapterManager.ImageProcessingMode.GPU, s_ctrace);
            }
            else
            {
                PlatformAdapterManager =
                    _PlatformAdapterManager.Create<_NativeApi, _SubsystemsDataAcquirer>(UnityContextHandle,
                        _PlatformAdapterManager.ImageProcessingMode.CPU, s_ctrace);
            }
        }

        private static void DisposePam()
        {
            Debug.Log("Disposing PAM");

            PlatformAdapterManager?.Dispose();
            PlatformAdapterManager = null;
        }

        internal static void Deinitialize()
        {
            OnDeinitialized?.Invoke();
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            if (UnityContextHandle != IntPtr.Zero)
            {
                Debug.Log($"Shutting down {nameof(LightshipUnityContext)}");

                DisposePam();

                if (s_propertyBagHandle != IntPtr.Zero)
                {
                    NativeApi.Lightship_ARDK_Unity_Property_Bag_Release(s_propertyBagHandle);
                    s_propertyBagHandle = IntPtr.Zero;
                }

                s_telemetryService?.Dispose();
                s_telemetryService = null;

                s_ctrace?.ShutdownCtrace();

                NativeApi.Lightship_ARDK_Unity_Context_Shutdown(UnityContextHandle);
                UnityContextHandle = IntPtr.Zero;
            }
#endif
        }

        /// Temporarily host this function here until we have a ApiGatewayAccess class implementation
        /// ONLY FOR TESTS
        internal static string FetchApiKeyFromNative()
        {
            if (UnityContextHandle == IntPtr.Zero)
            {
                Debug.LogWarning($"Cannot fetch api key from native as {nameof(LightshipUnityContext)} is not initialized");
                return string.Empty;
            }

            // this function is rather obsolete as we already have the api key at this point on the c# side and is
            // more useful for tests scripts until we implement username/password auth
            var sb = new StringBuilder(s_environmentConfig.ApiKey.Length);
            NativeApi.Lightship_ARDK_Unity_ApiGatewayAccess_GetApiKey(UnityContextHandle, sb, (ulong)sb.Capacity);
            return sb.ToString();
        }

        /// <summary>
        /// Container to wrap the native Lightship C APIs
        /// </summary>
        private static class NativeApi
        {
            [DllImport(_LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_Context_Create(
                bool disableCtrace, ref _DeviceInfo deviceInfo, ref _EnvironmentConfig environmentConfig);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Context_Shutdown(IntPtr unityContext);

            [DllImport(_LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_Property_Bag_Create(IntPtr unityContext);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Property_Bag_Release(IntPtr bagHandle);

            [DllImport(_LightshipPlugin.Name)]
            public static extern bool
                Lightship_ARDK_Unity_Property_Bag_Put(IntPtr bagHandle, string key, string value);

            /// Temporarily host this function here until we have a ApiGatewayAccess class implementation
            [DllImport(_LightshipPlugin.Name)]
            public static extern ulong Lightship_ARDK_Unity_ApiGatewayAccess_GetApiKey(IntPtr unityContext,
                StringBuilder outKey, ulong outKeyBufferLength);
        }

        // PLEASE NOTE: Do NOT add feature flags in this struct.
        [StructLayout(LayoutKind.Sequential)]
        private struct _EnvironmentConfig
        {
            public string ApiKey;
            public string VpsEndpoint;
            public string VpsCoverageEndpoint;
            public string SharedArEndpoint;
            public string FastDepthSemanticsEndpoint;
            public string DefaultDepthSemanticsEndpoint;
            public string SmoothDepthSemanticsEndpoint;
            public string ScanningEndpoint;
            public string ScanningSqcEndpoint;
            public string TelemetryEndpoint;
            public string TelemetryKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct _DeviceInfo
        {
            public string AppId;
            public string Platform;
            public string Manufacturer;
            public string DeviceModel;
            public string ClientId;
            public string Version;
            public string AppInstanceId;
            public bool DeviceLidarSupported;
        }
    }
}
