// Copyright 2023 Niantic, Inc. All Rights Reserved.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Niantic.Lightship.AR.Utilities.Log;
using Niantic.Lightship.AR.Loader;
using UnityEngine;
using Niantic.Lightship.AR.PAM;
using Niantic.Lightship.AR.Utilities.Profiling;
using Niantic.Lightship.AR.Settings;
using Niantic.Lightship.AR.Telemetry;


namespace Niantic.Lightship.AR.Core
{
    internal class LightshipUnityContext
    {
        // Pointer to the unity context
        internal static IntPtr UnityContextHandle { get; private set; } = IntPtr.Zero;

        internal static PlatformAdapterManager PlatformAdapterManager { get; private set; }
        internal static LightshipSettings ActiveSettings { get; private set; }
        private static IntPtr s_propertyBagHandle = IntPtr.Zero;
        private static EnvironmentConfig s_environmentConfig;
        private static UserConfig s_userConfig;
        private static TelemetryService s_telemetryService;
        internal static bool s_isDeviceLidarSupported = false;

        // Event triggered right before the context is destroyed. Used by internal code its lifecycle is not managed
        // by native UnityContext
        internal static event Action OnDeinitialized;
        internal static event Action OnUnityContextHandleInitialized;

        internal static void Initialize(LightshipSettings settings, bool isDeviceLidarSupported, bool isTest = false)
        {
            ActiveSettings = settings;
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            s_isDeviceLidarSupported = isDeviceLidarSupported;

            if (UnityContextHandle != IntPtr.Zero)
            {
                Log.Warning($"Cannot initialize {nameof(LightshipUnityContext)} as it is already initialized");
                return;
            }

            Log.Info($"Initializing {nameof(LightshipUnityContext)}");
            s_environmentConfig = new EnvironmentConfig
            {
                ScanningEndpoint = settings.ScanningEndpoint,
                ScanningSqcEndpoint = settings.ScanningSqcEndpoint,
                SharedArEndpoint = settings.SharedArEndpoint,
                VpsEndpoint = settings.VpsEndpoint,
                VpsCoverageEndpoint = settings.VpsCoverageEndpoint,
                FastDepthEndpoint = settings.FastDepthSemanticsEndpoint,
                MediumDepthEndpoint = settings.DefaultDepthSemanticsEndpoint,
                SmoothDepthEndpoint = settings.SmoothDepthSemanticsEndpoint,
                FastSemanticsEndpoint = settings.FastDepthSemanticsEndpoint,
                MediumSemanticsEndpoint = settings.DefaultDepthSemanticsEndpoint,
                SmoothSemanticsEndpoint = settings.SmoothDepthSemanticsEndpoint,
                TelemetryEndpoint = "",
                TelemetryKey = "",
            };

            s_userConfig = new UserConfig
            {
                ApiKey = settings.ApiKey,
                ScanningCustomEndpoint = "",
                CustomDepthEndpoint = "",
                CustomSemanticsEndpoint = "",
            };

            DeviceInfo deviceInfo = new DeviceInfo
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

            if (!isTest)
            {
                // Cannot use Application.persistentDataPath in testing
                try
                {
                    AnalyticsTelemetryPublisher telemetryPublisher = new AnalyticsTelemetryPublisher(
                        endpoint: settings.TelemetryEndpoint,
                        directoryPath: Path.Combine(Application.persistentDataPath, "telemetry"),
                        key: settings.TelemetryApiKey,
                        registerLogger: false);

                    s_telemetryService = new TelemetryService(UnityContextHandle, telemetryPublisher, settings.ApiKey);
                }
                catch (Exception)
                {
                    // Failed to initialize telemetry service. This is expected from Unity's Auto XR Initialization when starting a test
                }
            }
            OnUnityContextHandleInitialized?.Invoke();

            var modelPath = Path.Combine(Application.streamingAssetsPath, "full_model.bin");
            Log.Info("Model path: " + modelPath);

            s_propertyBagHandle = NativeApi.Lightship_ARDK_Unity_Property_Bag_Create(UnityContextHandle);
            NativeApi.Lightship_ARDK_Unity_Property_Bag_Put
            (
                s_propertyBagHandle,
                "depth_semantics_model_path",
                modelPath
            );

            ProfilerUtility.RegisterProfiler(new UnityProfiler());
            ProfilerUtility.RegisterProfiler(new CTraceProfiler());

            CreatePam(settings);
#endif
        }

        private static void CreatePam(LightshipSettings settings)
        {
            if (PlatformAdapterManager != null)
            {
                Log.Warning("Cannot create PAM as it is already created");
                return;
            }

            // Create the PAM, which will create the SAH
            Log.Info("Creating PAM");

            // Playback
            if (Application.isEditor || settings.UsePlayback)
            {
                PlatformAdapterManager =
                    AR.PAM.PlatformAdapterManager.Create<PAM.NativeApi, StandaloneSubsystemsDataAcquirer>
                    (
                        UnityContextHandle,
                        AR.PAM.PlatformAdapterManager.ImageProcessingMode.GPU
                    );
            }
            // Native
            else
            {
                PlatformAdapterManager =
                    AR.PAM.PlatformAdapterManager.Create<PAM.NativeApi, SubsystemsDataAcquirer>
                    (
                        UnityContextHandle,
                        AR.PAM.PlatformAdapterManager.ImageProcessingMode.CPU
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
            ActiveSettings = null;
            OnDeinitialized?.Invoke();
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            if (UnityContextHandle != IntPtr.Zero)
            {
                Log.Info($"Shutting down {nameof(LightshipUnityContext)}");

                DisposePam();

                if (s_propertyBagHandle != IntPtr.Zero)
                {
                    NativeApi.Lightship_ARDK_Unity_Property_Bag_Release(s_propertyBagHandle);
                    s_propertyBagHandle = IntPtr.Zero;
                }

                s_telemetryService?.Dispose();
                s_telemetryService = null;

                ProfilerUtility.ShutdownAll();

                NativeApi.Lightship_ARDK_Unity_Context_Shutdown(UnityContextHandle);
                UnityContextHandle = IntPtr.Zero;
            }
#endif
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
            public static extern IntPtr Lightship_ARDK_Unity_Property_Bag_Create(IntPtr unityContext);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Property_Bag_Release(IntPtr bagHandle);

            [DllImport(LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_Property_Bag_Put(IntPtr bagHandle, string key, string value);
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
            public string TelemetryEndpoint;
            public string TelemetryKey;
        }

        // PLEASE NOTE: Do NOT add feature flags in this struct.
        [StructLayout(LayoutKind.Sequential)]
        private struct UserConfig
        {
            public string ApiKey;
            public string CustomDepthEndpoint;
            public string CustomSemanticsEndpoint;
            public string ScanningCustomEndpoint;
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
