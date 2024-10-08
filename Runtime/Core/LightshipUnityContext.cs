// Copyright 2022-2024 Niantic.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Loader;
using UnityEngine;
using Niantic.Lightship.AR.PAM;
using Niantic.Lightship.AR.Utilities.Profiling;
using Niantic.Lightship.AR.Settings;
using Niantic.Lightship.AR.Telemetry;
using Niantic.Lightship.Utilities.UnityAssets;

namespace Niantic.Lightship.AR.Core
{
    /// <summary>
    /// [Experimental] <c>LightshipUnityContext</c> contains Lightship system components which are required by multiple modules.  This class should only be accessed by lightship packages
    ///
    /// This Interface is experimental so may change or be removed from future versions without warning.
    /// </summary>
    public class LightshipUnityContext
    {
        /// <summary>
        /// <c>UnityContextHandle</c> holds a pointer to the native Lightship Unity context.  This is intended to be used only by Lightship packages.
        /// </summary>
        public static IntPtr UnityContextHandle { get; private set; } = IntPtr.Zero;

        internal static PlatformAdapterManager PlatformAdapterManager { get; private set; }
        private static EnvironmentConfig s_environmentConfig;
        private static UserConfig s_userConfig;
        private static TelemetryService s_telemetryService;
        internal static bool s_isDeviceLidarSupported = false;

        // Event triggered right before the context is destroyed. Used by internal code its lifecycle is not managed
        // by native UnityContext
        internal static event Action OnDeinitialized;
        internal static event Action OnUnityContextHandleInitialized;

        // Function that an external plugin can use to register its own PlatformDataAcquirer with PAM
        internal static Func<IntPtr, bool, bool, PlatformAdapterManager> CreatePamWithPlugin;

        internal static void Initialize(bool isDeviceLidarSupported, bool disableTelemetry = false)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            s_isDeviceLidarSupported = isDeviceLidarSupported;

            if (UnityContextHandle != IntPtr.Zero)
            {
                Log.Warning($"Cannot initialize {nameof(LightshipUnityContext)} as it is already initialized");
                return;
            }

            var settings = LightshipSettingsHelper.ActiveSettings;

            Log.Info($"Initializing {nameof(LightshipUnityContext)}");
            s_environmentConfig = new EnvironmentConfig
            {
                ScanningEndpoint = settings.EndpointSettings.ScanningEndpoint,
                ScanningSqcEndpoint = settings.EndpointSettings.ScanningSqcEndpoint,
                SharedArEndpoint = settings.EndpointSettings.SharedArEndpoint,
                VpsEndpoint = settings.EndpointSettings.VpsEndpoint,
                VpsCoverageEndpoint = settings.EndpointSettings.VpsCoverageEndpoint,
                FastDepthEndpoint = settings.EndpointSettings.FastDepthSemanticsEndpoint,
                MediumDepthEndpoint = settings.EndpointSettings.DefaultDepthSemanticsEndpoint,
                SmoothDepthEndpoint = settings.EndpointSettings.SmoothDepthSemanticsEndpoint,
                FastSemanticsEndpoint = settings.EndpointSettings.FastDepthSemanticsEndpoint,
                MediumSemanticsEndpoint = settings.EndpointSettings.DefaultDepthSemanticsEndpoint,
                SmoothSemanticsEndpoint = settings.EndpointSettings.SmoothDepthSemanticsEndpoint,
                ObjectDetectionEndpoint = settings.EndpointSettings.ObjectDetectionEndpoint,
                TelemetryEndpoint = "",
                TelemetryKey = "",
            };

            s_userConfig = new UserConfig
            {
                ApiKey = settings.ApiKey,
                FeatureFlagFilePath = GetFeatureFlagPath()
            };

            var deviceInfo = new DeviceInfo
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

            UnityContextHandle = NativeApi.Lightship_ARDK_Unity_Context_Create(false, ref deviceInfo, ref s_environmentConfig, ref s_userConfig);

            Log.ConfigureLogger
            (
                UnityContextHandle,
                settings.UnityLightshipLogLevel,
                settings.FileLightshipLogLevel,
                settings.StdOutLightshipLogLevel
            );

            if (!disableTelemetry)
            {
                // Cannot use Application.persistentDataPath in testing
                try
                {
                    AnalyticsTelemetryPublisher telemetryPublisher =
                        new AnalyticsTelemetryPublisher
                        (
                            endpoint: settings.EndpointSettings.TelemetryEndpoint,
                            directoryPath: Path.Combine(Application.persistentDataPath, "telemetry"),
                            key: settings.EndpointSettings.TelemetryApiKey,
                            registerLogger: false
                        );

                    s_telemetryService = new TelemetryService(UnityContextHandle, telemetryPublisher, settings.ApiKey);
                }
                catch (Exception e)
                {
                    Log.Debug($"Failed to initialize telemetry service with exception {e}");
                }
            }
            else
            {
                Log.Debug("Detected a test run. Keeping telemetry disabled.");
            }
            OnUnityContextHandleInitialized?.Invoke();

            ProfilerUtility.RegisterProfiler(new UnityProfiler());
            ProfilerUtility.RegisterProfiler(new CTraceProfiler());

            CreatePam(settings);
#endif
        }

        private static void CreatePam(RuntimeLightshipSettings settings)
        {
            if (PlatformAdapterManager != null)
            {
                Log.Warning("Cannot create PAM as it is already created");
                return;
            }

            var isLidarEnabled = settings.PreferLidarIfAvailable && s_isDeviceLidarSupported;
            Log.Info($"Creating PAM (lidar enabled: {isLidarEnabled})");

            // Check if another Lightship plugin has registered with its own PlatformDataAcquirer.
            // Except if we're using playback, in which case we always use the SubsystemsDataAcquirer to read the dataset.
            if (null != CreatePamWithPlugin && !settings.UsePlayback)
            {
                PlatformAdapterManager =
                    CreatePamWithPlugin
                    (
                        UnityContextHandle,
                        isLidarEnabled,
                        settings.TestSettings.TickPamOnUpdate
                    );
            }
            else
            {
                PlatformAdapterManager =
                    PlatformAdapterManager.Create<PAM.NativeApi, SubsystemsDataAcquirer>
                    (
                        UnityContextHandle,
                        isLidarEnabled,
                        trySendOnUpdate: settings.TestSettings.TickPamOnUpdate
                    );
            }
        }

        private static void DisposePam()
        {
            Log.Info("Disposing PAM");

            PlatformAdapterManager?.Dispose();
            PlatformAdapterManager = null;
        }

        internal static void Deinitialize()
        {
            OnDeinitialized?.Invoke();
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            if (UnityContextHandle != IntPtr.Zero)
            {
                Log.Info($"Shutting down {nameof(LightshipUnityContext)}");

                DisposePam();

                s_telemetryService?.Dispose();
                s_telemetryService = null;

                NativeApi.Lightship_ARDK_Unity_Context_Shutdown(UnityContextHandle);
                UnityContextHandle = IntPtr.Zero;

                ProfilerUtility.ShutdownAll();
            }
#endif
        }

        internal static bool FeatureEnabled(string featureName)
        {
            if (!UnityContextHandle.IsValidHandle())
            {
                return false;
            }

            return NativeApi.Lightship_ARDK_Unity_Context_FeatureEnabled(UnityContextHandle, featureName);
        }

        private static string GetFeatureFlagPath()
        {
            const string featureFlagFileName = "featureFlag.json";
            var pathInPersistentData = Path.Combine(Application.persistentDataPath, featureFlagFileName);
            var pathInStreamingAsset = Path.Combine(Application.streamingAssetsPath, featureFlagFileName);
            var pathInTempCache = Path.Combine(Application.temporaryCachePath, featureFlagFileName);

            // Use if file exists in the persistent data path
            if (File.Exists(pathInPersistentData))
            {
                return pathInPersistentData;
            }

            // Use if file exists in the streaming asset path
            if (pathInStreamingAsset.Contains("://"))
            {
                // the file path is file URL e.g. on Android. copy to temp and use it
                bool fileRead = FileUtilities.TryReadAllText(pathInStreamingAsset, out var jsonString);
                if (fileRead)
                {
                    File.WriteAllText(pathInTempCache, jsonString);
                    return pathInTempCache;
                }
            }
            else
            {
                if (File.Exists(pathInStreamingAsset))
                {
                    return pathInStreamingAsset;
                }
            }

            // Write default setting to temp and use it
            const string defaultFeatureFlagSetting = @"{
                }";
            File.WriteAllText(pathInTempCache, defaultFeatureFlagSetting);
            return pathInTempCache;
        }

        public static IntPtr GetCoreContext(IntPtr unityContext)
        {
            return NativeApi.Lightship_ARDK_Unity_Context_GetCoreContext(unityContext);
        }

        public static IntPtr GetCommonContext(IntPtr unityContext)
        {
            return NativeApi.Lightship_ARDK_Unity_Context_GetCommonContext(unityContext);
        }

        /// <summary>
        /// Container to wrap the native Lightship C APIs
        /// </summary>
        private static class NativeApi
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_Context_Create(
                bool disableCtrace, ref DeviceInfo deviceInfo, ref EnvironmentConfig environmentConfig, ref UserConfig userConfig);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Context_Shutdown(IntPtr unityContext);

            [DllImport(LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_Context_FeatureEnabled(IntPtr unityContext, string featureName);

            [DllImport(LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_Context_GetCoreContext(IntPtr unityContext);

            [DllImport(LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_Context_GetCommonContext(IntPtr unityContext);
        }


        // PLEASE NOTE: Do NOT add feature flags in this struct.
        [StructLayout(LayoutKind.Sequential)]
        private struct EnvironmentConfig
        {
            public string VpsEndpoint;
            public string VpsCoverageEndpoint;
            public string SharedArEndpoint;
            public string FastDepthEndpoint;
            public string MediumDepthEndpoint;
            public string SmoothDepthEndpoint;
            public string FastSemanticsEndpoint;
            public string MediumSemanticsEndpoint;
            public string SmoothSemanticsEndpoint;
            public string ScanningEndpoint;
            public string ScanningSqcEndpoint;
            public string ObjectDetectionEndpoint;
            public string TelemetryEndpoint;
            public string TelemetryKey;
        }

        // PLEASE NOTE: Do NOT add feature flags in this struct.
        [StructLayout(LayoutKind.Sequential)]
        private struct UserConfig
        {
            public string ApiKey;
            public string FeatureFlagFilePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceInfo
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
