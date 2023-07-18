// Copyright 2023 Niantic, Inc. All Rights Reserved.
using System;
using System.Linq;
using Niantic.Lightship.AR.Loader;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.ARKit;

namespace Niantic.Lightship.AR.Editor
{
    static class LightshipSDKProjectValidationRules
    {
        private const string kCategory = "Niantic Lightship SDK";
        private static readonly LightshipSettings s_settings = LightshipSettings.Instance;
        private static readonly Version s_minGradleVersion = new(6,7,1);
        private const string OculusLoader = "Unity.XR.Oculus.OculusLoader";
        private const string MockHmdLoader = "Unity.XR.MockHMD.MockHMDLoader";
        private const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string UnityDownloadsPage = "https://unity.com/download";
        private const string CreateAPIKeyHelpLink = "https://lightship.dev/docs/beta/ardk/install/#adding-your-api-key-to-your-unity-project";
        private const string UpdateGradleVersionHelpLink = "https://lightship.dev/docs/beta/ardk/install/#installing-gradle-for-android";

        [InitializeOnLoadMethod]
        static void AddLightshipSDKValidationRules()
        {
            //iostargetvers, ioslocusasgedesc, androidtargetsdk
            var iOSTargetVersion = OSVersion.Parse(PlayerSettings.iOS.targetOSVersionString);
            var iOSLocUsageDesc = PlayerSettings.iOS.locationUsageDescription;
            var androidTargetVersion = PlayerSettings.Android.targetSdkVersion;

            var globalRules = CreateGlobalRules(s_settings);

            var iOSRules = CreateiOSRules(s_settings, iOSTargetVersion, iOSLocUsageDesc, false);

            var androidRules = CreateAndroidRules(androidTargetVersion, false);

            var standaloneRules = CreateStandaloneRules();
            var globalRulesForStandalone = new BuildValidationRule[] {globalRules[0], globalRules[4]};

            BuildValidator.AddRules(BuildTargetGroup.iOS, iOSRules);
            BuildValidator.AddRules(BuildTargetGroup.iOS, globalRules);

            BuildValidator.AddRules(BuildTargetGroup.Android, androidRules);
            BuildValidator.AddRules(BuildTargetGroup.Android, globalRules);

            BuildValidator.AddRules(BuildTargetGroup.Standalone, globalRulesForStandalone);
            BuildValidator.AddRules(BuildTargetGroup.Standalone, standaloneRules);
        }

        internal static BuildValidationRule[] CreateGlobalRules(LightshipSettings lightshipSettings)
        {
            var globalRules = new[]
            {
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "Lightship SDK is officially supported on Unity version 2021+ LTS",
                    CheckPredicate = () =>
                    {
#if !UNITY_2021_1_OR_NEWER
                        return false;
#endif
                        return true;
                    },
                    FixItMessage = "Please update your Unity Editor version to 2021+ LTS",
                    FixItAutomatic = false,

                    HelpLink = UnityDownloadsPage
                },
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "An API Key is needed to access network features like VPS",
                    CheckPredicate = () => !string.IsNullOrEmpty(lightshipSettings.ApiKey),

                    IsRuleEnabled =
                        () => lightshipSettings.UseLightshipPersistentAnchor,

                    FixIt = () =>
                        SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Niantic Lightship SDK"),

                    FixItMessage = "Please generate an API Key in the Projects tab in your Lightship Account Dashboard and set it in Lightship Settings",
                    HelpLink = CreateAPIKeyHelpLink,
                    FixItAutomatic = false,
                    Error = true
                },
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "Framerate over 20 could negatively affect performance in older devices",
                    CheckPredicate = () => lightshipSettings.LightshipDepthFrameRate <= 20,
                    IsRuleEnabled = () => lightshipSettings.UseLightshipDepth,
                    FixIt = () =>
                        SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Niantic Lightship SDK"),

                    FixItMessage = "Go to Lightship Settings > Depth and lower the framerate for better performance",
                    FixItAutomatic = false,
                    Error = false
                },
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "A dataset path inside the project's Streaming Assets folder must be specified for playback on device.",
                    CheckPredicate = () =>
                    {
                        var datasetPath = lightshipSettings.DevicePlaybackSettings.PlaybackDatasetPath;
                        var hasDatasetPath = !string.IsNullOrEmpty(datasetPath);
                        if (!hasDatasetPath)
                            return false;

                        var isInStreamingAssets =
                            datasetPath.StartsWith(Application.dataPath + "/StreamingAssets");
                        return isInStreamingAssets;
                    },
                    IsRuleEnabled = () => lightshipSettings.DevicePlaybackSettings.UsePlayback,
                    FixIt = () =>
                    {
                        SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Niantic Lightship SDK");
                    },
                    FixItMessage = "Please go to Lightship Settings > Playback > Device and set a dataset path. Make sure for device playback, the file is inside StreamingAssets.",
                    FixItAutomatic = false,
                    Error = true
                },
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "A dataset path must be specified for playback in Editor.",
                    CheckPredicate = () => !string.IsNullOrEmpty(lightshipSettings.EditorPlaybackSettings.PlaybackDatasetPath),
                    IsRuleEnabled = () => lightshipSettings.EditorPlaybackSettings.UsePlayback,
                    FixIt = () =>
                        SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Niantic Lightship SDK"),
                    FixItMessage = "Please go to Lightship Settings > Playback > Editor and set a dataset path.",
                    FixItAutomatic = false,
                    Error = true
                }
            };
            return globalRules;
        }

        internal static BuildValidationRule[] CreateiOSRules(LightshipSettings lightshipSettings, OSVersion iOSTargetVersion, string iOSLocUsageDesc, bool testFlag)
        {
            var iOSRules = new[]
            {
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "Please enable the 'Niantic Lightship SDK' plugin in 'XR Plug-in Management'.",
                    CheckPredicate = IsLightshipPluginEnablediOS,
                    FixItMessage = "Open Project Setting > XR Plug-in Management > iOS tab and enable `Niantic Lightship SDK`.",
                    FixIt = () =>
                    {
                        var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);

                        XRPackageMetadataStore.RemoveLoader(generalSettings.AssignedSettings,
                            typeof(UnityEngine.XR.ARKit.ARKitLoader).FullName, BuildTargetGroup.iOS);

                        XRPackageMetadataStore.AssignLoader(generalSettings.AssignedSettings,
                            typeof(LightshipARKitLoader).FullName, BuildTargetGroup.iOS);
                    },
                    Error = false,
                    FixItAutomatic = true
                },
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "A minimum iOS version of 13.0 is needed when Depth, Meshing, or Semantics are enabled",
                    Error = true,
                    CheckPredicate = () =>
                    {
                        if (testFlag)
                        {
                            return iOSTargetVersion >= new OSVersion(13);
                        }
                        var userSetTargetVersion = OSVersion.Parse(PlayerSettings.iOS.targetOSVersionString);
                        return userSetTargetVersion >= new OSVersion(13);
                    },
                    IsRuleEnabled = () =>
                    {
                        if (lightshipSettings.UseLightshipDepth || lightshipSettings.UseLightshipMeshing
                            || lightshipSettings.UseLightshipSemanticSegmentation)
                            return true;
                        return false;
                    },
                    FixIt = () =>
                    {
                        PlayerSettings.iOS.targetOSVersionString = "13.0";
                    },
                    FixItMessage = "Please open Project Settings > Player > iOS > Other Settings to change target iOS version",
                    FixItAutomatic = true
                },
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "A location usage description must be provided when using location services with VPS",
                    CheckPredicate = () =>
                    {
                        if (testFlag)
                        {
                            return !string.IsNullOrEmpty(iOSLocUsageDesc);
                        }
                        return !string.IsNullOrEmpty(PlayerSettings.iOS.locationUsageDescription);
                    },
                    IsRuleEnabled = () => lightshipSettings.UseLightshipPersistentAnchor,
                    FixIt = () =>
                    {
                        PlayerSettings.iOS.locationUsageDescription = "Lightship VPS needs access to your location.";
                    },
                    FixItMessage = "Please go to Project Settings > Player > iOS > Other Settings and provide a location usage description.",
                    FixItAutomatic = true,
                    Error = true
                }
            };
            return iOSRules;
        }

        internal static BuildValidationRule[] CreateAndroidRules(AndroidSdkVersions androidTargetVersion, bool testFlag)
        {
            var androidRules = new[]
            {
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "Please enable the 'Niantic Lightship SDK' plugin in 'XR Plug-in Management'.",
                    CheckPredicate = IsLightshipPluginEnabledAndroid,
                    FixItMessage = "Open Project Setting > XR Plug-in Management > Android tab and enable `Niantic Lightship SDK`.",
                    FixIt = () =>
                    {
                        var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);

                        XRPackageMetadataStore.RemoveLoader(generalSettings.AssignedSettings,
                            typeof(UnityEngine.XR.ARCore.ARCoreLoader).FullName, BuildTargetGroup.Android);
                        XRPackageMetadataStore.RemoveLoader(generalSettings.AssignedSettings,
                            OculusLoader, BuildTargetGroup.Android);
                        XRPackageMetadataStore.RemoveLoader(generalSettings.AssignedSettings,
                            MockHmdLoader, BuildTargetGroup.Android);

                        XRPackageMetadataStore.AssignLoader(generalSettings.AssignedSettings,
                            typeof(LightshipARCoreLoader).FullName, BuildTargetGroup.Android);
                    },
                    Error = false,
                    FixItAutomatic = true
                },
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "Android target version must be API level 33 or higher to publish app on Google Play",
                    CheckPredicate = () =>
                    {
                        if (testFlag)
                        {
                            return (int)androidTargetVersion >= 33;
                        }
                        return (int)PlayerSettings.Android.targetSdkVersion >= 33;
                    },
                    FixItMessage = "Please go to Project Settings > Player > Android > Other Settings and change target API level to 33",
                    FixIt = () =>
                    {
#if UNITY_2023_1_OR_NEWER
                        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
#else
                        SettingsService.OpenProjectSettings("Project/Player");
#endif
                    },
#if UNITY_2023_1_OR_NEWER
                    FixItAutomatic = true,
#else
                    FixItAutomatic = false,
#endif
                    Error = false
                },
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = $"Lightship SDK requires at least Gradle version {s_minGradleVersion}. Make sure to set correct gradle path in Preferences > External Tools",
                    CheckPredicate = () => GetGradleVersion() >= s_minGradleVersion,
                    FixIt = () => { SettingsService.OpenUserPreferences("Preferences/External Tools");},
                    IsRuleEnabled = () => EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
                    HelpText = "Follow the instructions in the Lightship SDK docs for building for Android apps with OpenCL",
                    HelpLink = UpdateGradleVersionHelpLink,
                    FixItMessage = "Please go to Preferences > External Tools and manually update your gradle version to 6.7.1 or greater.",
                    FixItAutomatic = false,
                    Error = true
                }
            };
            return androidRules;
        }

        internal static BuildValidationRule[] CreateStandaloneRules()
        {
            var standaloneRules = new[]
            {
                new BuildValidationRule
                {
                    Category = kCategory,
                    Message = "Please enable the 'Niantic Lightship SDK' plugin in 'XR Plug-in Management'.",
                    CheckPredicate = IsLightshipPluginEnabledStandalone,
                    FixItMessage =
                        "Open Project Setting > XR Plug-in Management > Standalone tab and enable `Niantic Lightship SDK`.",
                    FixIt = () =>
                    {
                        var generalSettings =
                            XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);

                        XRPackageMetadataStore.RemoveLoader(generalSettings.AssignedSettings,
                            typeof(UnityEngine.XR.Simulation.SimulationLoader).FullName, BuildTargetGroup.Standalone);
                        XRPackageMetadataStore.RemoveLoader(generalSettings.AssignedSettings,
                            OculusLoader, BuildTargetGroup.Standalone);
                        XRPackageMetadataStore.RemoveLoader(generalSettings.AssignedSettings,
                            OpenXRLoader, BuildTargetGroup.Standalone);
                        XRPackageMetadataStore.RemoveLoader(generalSettings.AssignedSettings,
                            MockHmdLoader, BuildTargetGroup.Standalone);

                        XRPackageMetadataStore.AssignLoader(generalSettings.AssignedSettings,
                            typeof(LightshipStandaloneLoader).FullName, BuildTargetGroup.Standalone);
                    },
                    Error = false,
                    FixItAutomatic = true
                }
            };
            return standaloneRules;
        }

        static bool IsLightshipPluginEnablediOS()
        {
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
            if (generalSettings == null)
                return false;

            var managerSettings = generalSettings.AssignedSettings;

            return managerSettings != null && managerSettings.activeLoaders.Any(loader => loader is LightshipARKitLoader);
        }

        static bool IsLightshipPluginEnabledAndroid()
        {
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings == null)
                return false;

            var managerSettings = generalSettings.AssignedSettings;

            return managerSettings != null && managerSettings.activeLoaders.Any(loader => loader is LightshipARCoreLoader);
        }

        static bool IsLightshipPluginEnabledStandalone()
        {
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (generalSettings == null)
                return false;

            var managerSettings = generalSettings.AssignedSettings;

            return managerSettings != null && managerSettings.activeLoaders.Any(loader => loader is LightshipStandaloneLoader);
        }

        static Version GetGradleVersion()
        {
            return Gradle.TryGetVersion(out var gradleVersion, out var _) ? gradleVersion : new Version(0, 0);
        }
    }
}
