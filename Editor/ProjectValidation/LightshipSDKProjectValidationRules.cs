// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.IO;
using Niantic.Lightship.AR.Loader;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARKit;
using JetBrains.Annotations;
using System.Text.RegularExpressions;
using UnityEditor.XR.Management.Metadata;

namespace Niantic.Lightship.AR.Editor
{
    internal static class LightshipSDKProjectValidationRules
    {
        private static readonly LightshipSettings s_settings = LightshipSettings.Instance;
        private static readonly Version s_minGradleVersion = new(6, 7, 1);
        private static readonly Version s_minUnityVersion = new(2021, 1);

        private const string XRPlugInManagementPath = "Project/XR Plug-in Management";
        private const string NianticLightshipSDKPath = XRPlugInManagementPath + "/Niantic Lightship SDK";
        private const string PreferencesExternalToolsPath = "Preferences/External Tools";
        private const string Category = "Niantic Lightship SDK";
        private const string PlaybackDatasetMetaFilename = "capture.json";
        private const string UnityDownloadsPage = "https://unity.com/download";
        private const string CreateAPIKeyHelpLink = "https://lightship.dev/docs/beta/ardk/install/#adding-your-api-key-to-your-unity-project";
        private const string UpdateGradleVersionHelpLink = "https://lightship.dev/docs/beta/ardk/install/#installing-gradle-for-android";

        private static Dictionary<BuildTargetGroup, List<BuildValidationRule>> s_platformRules;

        internal static Dictionary<BuildTargetGroup, List<BuildValidationRule>> PlatformRules
        {
            get
            {
                if (s_platformRules == null || s_platformRules.Count == 0)
                {
                    s_platformRules = CreateLightshipSDKValidationRules();
                }

                return s_platformRules;
            }
        }

        [InitializeOnLoadMethod]
        private static void AddLightshipSDKValidationRules()
        {
            BuildValidator.AddRules(BuildTargetGroup.Standalone, PlatformRules[BuildTargetGroup.Standalone]);
            BuildValidator.AddRules(BuildTargetGroup.Android, PlatformRules[BuildTargetGroup.Android]);
            BuildValidator.AddRules(BuildTargetGroup.iOS, PlatformRules[BuildTargetGroup.iOS]);
        }

        private static Dictionary<BuildTargetGroup, List<BuildValidationRule>> CreateLightshipSDKValidationRules()
        {
            var platformRules = new Dictionary<BuildTargetGroup, List<BuildValidationRule>>
            {
                { BuildTargetGroup.Standalone, new List<BuildValidationRule>() },
                { BuildTargetGroup.Android, new List<BuildValidationRule>() },
                { BuildTargetGroup.iOS, new List<BuildValidationRule>() }
            };

            var standaloneGlobalRules = CreateGlobalRules(
                s_settings,
                "StreamingAssets",
                GetStandaloneIsLightshipPluginEnabled,
                GetUnityVersion);
            var standaloneRules = CreateStandaloneRules(
                GetStandaloneIsLightshipPluginEnabled,
                GetSimulationPluginsStatusMatches);

            var androidGlobalRules = CreateGlobalRules(
                s_settings,
                "StreamingAssets",
                GetAndroidIsLightshipPluginEnabled,
                GetUnityVersion);
            var androidRules = CreateAndroidRules(
                GetAndroidIsLightshipPluginEnabled,
                GetAndroidTargetSdkVersion,
                GetAndroidGradleVersion,
                GetActiveBuildTarget);

            var iosGlobalRules = CreateGlobalRules(
                s_settings,
                "StreamingAssets",
                GetIosIsLightshipPluginEnabled,
                GetUnityVersion);
            var iosRules = CreateIOSRules(
                s_settings,
                GetIosIsLightshipPluginEnabled,
                GetIosTargetOsVersionString,
                GetIosLocationUsageDescription);

            platformRules[BuildTargetGroup.Standalone].AddRange(standaloneRules);
            platformRules[BuildTargetGroup.Standalone].Add(standaloneGlobalRules[0]);
            platformRules[BuildTargetGroup.Standalone].Add(standaloneGlobalRules[1]);
            platformRules[BuildTargetGroup.Standalone].Add(standaloneGlobalRules[3]);

            platformRules[BuildTargetGroup.Android].AddRange(androidRules);
            platformRules[BuildTargetGroup.Android].Add(androidGlobalRules[0]);
            platformRules[BuildTargetGroup.Android].Add(androidGlobalRules[1]);
            platformRules[BuildTargetGroup.Android].Add(androidGlobalRules[2]);

            platformRules[BuildTargetGroup.iOS].AddRange(iosRules);
            platformRules[BuildTargetGroup.iOS].Add(iosGlobalRules[0]);
            platformRules[BuildTargetGroup.iOS].Add(iosGlobalRules[1]);
            platformRules[BuildTargetGroup.iOS].Add(iosGlobalRules[2]);

            return platformRules;
        }

        internal static BuildValidationRule[] CreateGlobalRules
        (
            LightshipSettings lightshipSettings,
            string datasetContainingDirectory,
            [NotNull] Func<bool> getIsLightshipPluginEnabled,
            [NotNull] Func<string> getUnityVersion
        )
        {
            var globalRules = new[]
            {
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK, it is recommended to use Unity version 2021.1 LTS or higher.",
                    CheckPredicate = () => new Version(getUnityVersion.Invoke()) >= s_minUnityVersion,
                    IsRuleEnabled = getIsLightshipPluginEnabled.Invoke,
                    FixItMessage = "Open the Unity project in Unity version 2021.1 LTS or higher.",
                    FixItAutomatic = false,
                    HelpLink = UnityDownloadsPage
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK, please set the Lightship API Key provided in your project at https://lightship.dev/account/projects",
                    CheckPredicate = () => !string.IsNullOrWhiteSpace(lightshipSettings.ApiKey),
                    IsRuleEnabled = getIsLightshipPluginEnabled.Invoke,
                    FixIt = () => SettingsService.OpenProjectSettings(NianticLightshipSDKPath),
                    FixItMessage = "Open `Project Settings` > `XR Plug-in Management` > `Niantic Lightship SDK` and set the Lightship API Key provided by the Lightship Portal.",
                    HelpText = "For further assistance, follow the instructions in the Lightship SDK docs.",
                    HelpLink = CreateAPIKeyHelpLink,
                    FixItAutomatic = false,
                    Error = true
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK Device Playback feature, set the device playback dataset path to a valid dataset directory path in the StreamingAssets directory.",
                    CheckPredicate = () =>
                    {
                        var datasetPath = lightshipSettings.DevicePlaybackSettings.PlaybackDatasetPath;
                        if (string.IsNullOrEmpty(datasetPath) || string.IsNullOrEmpty(datasetContainingDirectory))
                        {
                            return false;
                        }
                        // normalize paths using GetFullPath() so that string comparison works on osx or windows
                        var fullPathDatasetContainingDir = Path.GetFullPath(datasetContainingDirectory, Application.dataPath);
                        var fullPathDatasetPath = Path.GetFullPath(datasetPath);

                        var isInStreamingAssets = fullPathDatasetPath.StartsWith(fullPathDatasetContainingDir);
                        var doesFolderExist = Directory.Exists(datasetPath);
                        var doesMetafileExist = File.Exists(Path.Combine(datasetPath, PlaybackDatasetMetaFilename));
                        return isInStreamingAssets && doesFolderExist && doesMetafileExist;
                    },
                    IsRuleEnabled = () =>
                        getIsLightshipPluginEnabled.Invoke() &&
                        lightshipSettings.DevicePlaybackSettings.UsePlayback,
                    FixIt = () =>
                    {
                        SettingsService.OpenProjectSettings(NianticLightshipSDKPath);
                    },
                    FixItMessage = "Open `Project Settings` > `XR Plug-in Management` > `Niantic Lightship SDK` > `Device` and set the device playback dataset path to a valid dataset directory path in the StreamingAssets directory.",
                    FixItAutomatic = false,
                    Error = true
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK Editor Playback feature, set the editor playback dataset path to a valid dataset directory path.",
                    CheckPredicate = () =>
                    {
                        var datasetPath = lightshipSettings.EditorPlaybackSettings.PlaybackDatasetPath;
                        if (string.IsNullOrEmpty(datasetPath))
                        {
                            return false;
                        }
                        var doesFolderExist = Directory.Exists(datasetPath);
                        var doesMetafileExist = File.Exists(Path.Combine(datasetPath, PlaybackDatasetMetaFilename));
                        return doesFolderExist && doesMetafileExist;
                    },
                    IsRuleEnabled = () =>
                        getIsLightshipPluginEnabled.Invoke() &&
                        lightshipSettings.EditorPlaybackSettings.UsePlayback,
                    FixIt = () =>
                    {
                        SettingsService.OpenProjectSettings(NianticLightshipSDKPath);
                    },
                    FixItMessage = "Open `Project Settings` > `XR Plug-in Management` > `Niantic Lightship SDK` > `Editor` and set the editor playback dataset path to a valid dataset directory path.",
                    FixItAutomatic = false,
                    Error = true
                }
            };
            return globalRules;
        }

        internal static BuildValidationRule[] CreateIOSRules(
            LightshipSettings lightshipSettings,
            [NotNull] Func<bool> getIosIsLightshipPluginEnabled,
            [NotNull] Func<string> getIosTargetOsVersionString,
            [NotNull] Func<string> getIosLocationUsageDescription)
        {
            var iOSRules = new[]
            {
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK for iOS, enable the 'Niantic Lightship SDK' plug-in.",
                    CheckPredicate = getIosIsLightshipPluginEnabled.Invoke,
                    FixItMessage = "Open `Project Settings` > `XR Plug-in Management` > `iOS settings` and enable the 'Niantic Lightship SDK' plug-in.",
                    FixIt = () =>
                    {
                        var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
                        if (null == generalSettings)
                            return;

                        var managerSettings = generalSettings.AssignedSettings;
                        if (null == managerSettings)
                            return;

                        XRPackageMetadataStore.AssignLoader(managerSettings, typeof(LightshipARKitLoader).FullName, BuildTargetGroup.iOS);
                    },
                    FixItAutomatic = true,
                    Error = false
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = $"If using Lightship ARDK Depth, Meshing, or Semantics features for iOS, set the target iOS version to 13.0 or higher (currently {getIosTargetOsVersionString.Invoke()}).",
                    CheckPredicate = () => OSVersion.Parse(getIosTargetOsVersionString.Invoke()) >= new OSVersion(13),
                    IsRuleEnabled = () =>
                    {
                        var isFeatureEnabled =
                            lightshipSettings.UseLightshipDepth ||
                            lightshipSettings.UseLightshipMeshing ||
                            lightshipSettings.UseLightshipSemanticSegmentation;
                        return
                            getIosIsLightshipPluginEnabled.Invoke() &&
                            isFeatureEnabled;
                    },
                    FixIt = () =>
                    {
                        PlayerSettings.iOS.targetOSVersionString = "13.0";
                    },
                    FixItMessage = "Open `Project Settings` > `Player` > `iOS settings` > `Other Settings` and set the target iOS version to 13.0 or higher.",
                    FixItAutomatic = true,
                    Error = true
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = $"If using Lightship ARDK Scanning feature for iOS, set the target iOS version to 14.0 or higher (currently {getIosTargetOsVersionString.Invoke()}).",
                    CheckPredicate = () => OSVersion.Parse(getIosTargetOsVersionString.Invoke()) >= new OSVersion(14),
                    IsRuleEnabled = () =>
                    {
                        var isFeatureEnabled = lightshipSettings.UseLightshipScanning;
                        return
                            getIosIsLightshipPluginEnabled.Invoke() &&
                            isFeatureEnabled;
                    },
                    FixIt = () =>
                    {
                        PlayerSettings.iOS.targetOSVersionString = "14.0";
                    },
                    FixItMessage = "Open `Project Settings` > `Player` > `iOS settings` > `Other Settings` and set the target iOS version to 14.0 or higher.",
                    FixItAutomatic = true,
                    Error = true
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK VPS or Scanning features for iOS, set the location usage description.",
                    CheckPredicate = () => !string.IsNullOrEmpty(getIosLocationUsageDescription.Invoke()),
                    IsRuleEnabled = () =>
                    {
                        var isFeatureEnabled =
                            lightshipSettings.UseLightshipScanning ||
                            lightshipSettings.UseLightshipPersistentAnchor;
                        return
                            getIosIsLightshipPluginEnabled.Invoke() &&
                            isFeatureEnabled;
                    },
                    FixIt = () =>
                    {
                        PlayerSettings.iOS.locationUsageDescription = "Lightship VPS needs access to your location.";
                    },
                    FixItMessage = "Open 'Project Settings' > 'Player' > 'iOS Settings' > `Other Settings` and set the location usage description.",
                    FixItAutomatic = true,
                    Error = true
                }
            };
            return iOSRules;
        }

        internal static BuildValidationRule[] CreateAndroidRules(
            [NotNull] Func<bool> getAndroidIsLightshipPluginEnabled,
            [NotNull] Func<int> getAndroidTargetSdkVersion,
            [NotNull] Func<string> getAndroidGradleVersion,
            [NotNull] Func<BuildTarget> getActiveBuildTarget)
        {
            var androidRules = new[]
            {
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK for Android, enable the 'Niantic Lightship SDK' plug-in.",
                    CheckPredicate = getAndroidIsLightshipPluginEnabled.Invoke,
                    FixIt = () =>
                    {
                        EditorApplication.ExecuteMenuItem("Lightship/XR Plug-in Management");
                    },
                    FixItMessage = "Open `Project Settings` > `XR Plug-in Management` > `Android settings` and enable the 'Niantic Lightship SDK' plug-in.",
                    FixItAutomatic = false,
                    Error = false,
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = $"If using Lightship ARDK for Android, set the Android Gradle path to that of a Gradle of version {s_minGradleVersion} or higher.",
                    CheckPredicate = () => new Version(getAndroidGradleVersion.Invoke()) >= s_minGradleVersion,
                    IsRuleEnabled = () =>
                        getAndroidIsLightshipPluginEnabled.Invoke() &&
                        getActiveBuildTarget.Invoke() == BuildTarget.Android,
                    FixIt = () =>
                    {
                        SettingsService.OpenUserPreferences(PreferencesExternalToolsPath);
                    },
                    FixItMessage = $"Open `Preferences` > `External Tools` and set the Android Gradle path to that of a Gradle of version {s_minGradleVersion} or higher.",
                    FixItAutomatic = false,
                    HelpText = "For further assistance, follow the instructions in the Lightship SDK docs.",
                    HelpLink = UpdateGradleVersionHelpLink,
                    Error = true
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK for Android, set the graphics API to OpenGLES3.",
                    IsRuleEnabled = getAndroidIsLightshipPluginEnabled.Invoke,
                    CheckPredicate = () =>
                    {
                        var graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
                        return graphicsApis.Length > 0 && graphicsApis[0] == GraphicsDeviceType.OpenGLES3;
                    },
                    FixItMessage = "Open `Project Settings` > `Player` > `Android setting` and disable 'Auto Graphics API' then set the graphics API to OpenGLES3",
                    FixIt = () =>
                    {
                        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                        var currentGraphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
                        GraphicsDeviceType[] correctGraphicsApis;
                        if (currentGraphicsApis.Length == 0)
                        {
                            correctGraphicsApis = new[]
                            {
                                GraphicsDeviceType.OpenGLES3
                            };
                        }
                        else
                        {
                            var graphicApis = new List<GraphicsDeviceType>(currentGraphicsApis.Length);
                            graphicApis.Add(GraphicsDeviceType.OpenGLES3);
                            foreach (var graphicsApi in currentGraphicsApis)
                            {
                                if (graphicsApi != GraphicsDeviceType.OpenGLES3)
                                {
                                    graphicApis.Add(graphicsApi);
                                }
                            }
                            correctGraphicsApis = graphicApis.ToArray();
                        }
                        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, correctGraphicsApis);
                    },
                    Error = true
                },
#if !UNITY_2022_1_OR_NEWER
                // This rule is only enabled on Unity versions 2022 or lower since Unity 2022 no longer has
                // a means to programatically set the android sdk version to 33 as it expects the
                // "highest installed version" option to be used
                new BuildValidationRule
                {
                    Category = Category,
                    Message = $"If using Lightship ARDK for Android, set the target Android SDK version to 33 or higher" +
                        $" (currently {(getAndroidTargetSdkVersion.Invoke() == 0 ? "automatic" : getAndroidTargetSdkVersion.Invoke())}).",
                    CheckPredicate = () => getAndroidTargetSdkVersion.Invoke() >= 33 || getAndroidTargetSdkVersion.Invoke() == 0,
                    IsRuleEnabled = getAndroidIsLightshipPluginEnabled.Invoke,
                    FixIt = () => PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)33,
                    FixItMessage = $"Open `Project Settings` > `Player` > 'Android settings' > `Other Settings` and set the target Android SDK version to 33 or higher.",
                    FixItAutomatic = true,
                    Error = true
                }
#endif
            };
            return androidRules;
        }

        internal static BuildValidationRule[] CreateStandaloneRules(
            [NotNull] Func<bool> getStandaloneIsLightshipPluginEnabled,
            [NotNull] Func<bool> getSimulationPluginsStatusMatches)
        {
            var standaloneRules = new[]
            {
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK for Standalone with a playback dataset, enable the 'Niantic Lightship SDK' plug-in.",
                    CheckPredicate = getStandaloneIsLightshipPluginEnabled.Invoke,
                    FixIt = () =>
                    {
                        var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
                        if (null == generalSettings)
                            return;

                        var managerSettings = generalSettings.AssignedSettings;
                        if (null == managerSettings)
                            return;

                        XRPackageMetadataStore.AssignLoader(managerSettings, typeof(LightshipStandaloneLoader).FullName, BuildTargetGroup.Standalone);
                    },
                    FixItMessage = "Open `Project Settings` > `XR Plug-in Management` > `Standalone settings` and enable the 'Niantic Lightship SDK' plug-in.",
                    FixItAutomatic = true,
                    Error = false,
                },
                new BuildValidationRule
                {
                    Category = Category,
                    Message = "If using Lightship ARDK for Simulation, enable both the 'Niantic Lightship Simulation' and 'XR Simulation' plug-ins.",
                    CheckPredicate = getSimulationPluginsStatusMatches.Invoke,
                    FixIt = () =>
                    {
                        var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
                        if (null == generalSettings)
                            return;

                        var managerSettings = generalSettings.AssignedSettings;
                        if (null == managerSettings)
                            return;

                        if (LightshipEditorUtilities.IsUnitySimulationPluginEnabled())
                        {
                            // Lightship Simulation Loader needs to be enabled and must take precedence
                            XRPackageMetadataStore.AssignLoader(managerSettings,
                                "Niantic.Lightship.AR.Loader.LightshipSimulationLoader", BuildTargetGroup.Standalone);
                        }
                        else
                        {
                            // Unity's XR Simulation Loader needs to be added to the list but will not be used.
                            // This is to unlock the XR Simulation UI menus.
                            XRPackageMetadataStore.AssignLoader(managerSettings,
                                "UnityEngine.XR.Simulation.SimulationLoader", BuildTargetGroup.Standalone);
                        }
                    },
                    FixItMessage = "Open `Project Settings` > `XR Plug-in Management` > `Standalone settings` and enable " +
                        "both the 'Niantic Lightship Simulation' and 'XR Simulation' plug-ins.",
                    FixItAutomatic = true,
                    Error = false,
                },
            };
            return standaloneRules;
        }

        private static bool GetIosIsLightshipPluginEnabled()
        {
            return LightshipEditorUtilities.GetIosIsLightshipPluginEnabled();
        }

        private static bool GetAndroidIsLightshipPluginEnabled()
        {
            return LightshipEditorUtilities.GetAndroidIsLightshipPluginEnabled();
        }

        private static bool GetStandaloneIsLightshipPluginEnabled()
        {
            // Standalone mode is used for both playback and simulation.
            // If the Lightship Simulation plugin is enabled, don't display a warning about the standalone plugin.
            return LightshipEditorUtilities.GetStandaloneIsLightshipPluginEnabled()
                || LightshipEditorUtilities.GetSimulationIsLightshipPluginEnabled();
        }

        // When using simulation, both the Lightship and Unity simulation plugins must be enabled to unlock the XR Environment UI menus.
        private static bool GetSimulationPluginsStatusMatches()
        {
            return !(LightshipEditorUtilities.GetSimulationIsLightshipPluginEnabled() ^ LightshipEditorUtilities.IsUnitySimulationPluginEnabled());
        }

        private static string GetAndroidGradleVersion()
        {
            // Note: This Gradle API call only works if the target platform is set to Android before making this call
            return Gradle.TryGetVersion(out var gradleVersion, out var message) ? gradleVersion.ToString() : new Version(0, 0).ToString();
        }

        private static string GetUnityVersion()
        {
            return Regex.Replace(Application.unityVersion, "[A-Za-z ]", "");
        }

        private static BuildTarget GetActiveBuildTarget()
        {
            return EditorUserBuildSettings.activeBuildTarget;
        }

        private static string GetIosTargetOsVersionString()
        {
            return PlayerSettings.iOS.targetOSVersionString;
        }

        private static string GetIosLocationUsageDescription()
        {
            return PlayerSettings.iOS.locationUsageDescription;
        }

        private static int GetAndroidTargetSdkVersion()
        {
            return (int)PlayerSettings.Android.targetSdkVersion;
        }
    }
}
