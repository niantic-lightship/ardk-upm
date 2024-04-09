// Copyright 2022-2024 Niantic.

using System;
using System.Linq;
using Niantic.Lightship.AR.Loader;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine.XR.Simulation;

namespace Niantic.Lightship.AR.Editor
{
    internal static class LightshipEditorUtilities
    {
        internal static bool GetIosIsLightshipPluginEnabled()
        {
            return GetIsLightshipPluginEnabledForPlatform(BuildTargetGroup.iOS, typeof(LightshipARKitLoader));
        }

        internal static bool GetAndroidIsLightshipPluginEnabled()
        {
            return GetIsLightshipPluginEnabledForPlatform(BuildTargetGroup.Android, typeof(LightshipARCoreLoader));
        }

        internal static bool GetStandaloneIsLightshipPluginEnabled()
        {
            return GetIsLightshipPluginEnabledForPlatform(BuildTargetGroup.Standalone, typeof(LightshipStandaloneLoader));
        }

        internal static bool GetSimulationIsLightshipPluginEnabled()
        {
            return GetIsLightshipPluginEnabledForPlatform(BuildTargetGroup.Standalone, typeof(LightshipSimulationLoader));
        }

        private static bool GetIsLightshipPluginEnabledForPlatform(BuildTargetGroup buildTargetGroup, Type lightshipLoaderType)
        {
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(buildTargetGroup);
            if (generalSettings == null)
            {
                return false;
            }
            var managerSettings = generalSettings.AssignedSettings;
            var doesLoaderOfTypeExist = false;
            if (lightshipLoaderType == typeof(LightshipARKitLoader))
            {
                doesLoaderOfTypeExist = managerSettings.activeLoaders.Any(loader => loader is LightshipARKitLoader);
            }
            else if (lightshipLoaderType == typeof(LightshipARCoreLoader))
            {
                doesLoaderOfTypeExist = managerSettings.activeLoaders.Any(loader => loader is LightshipARCoreLoader);
            }
            else if (lightshipLoaderType == typeof(LightshipStandaloneLoader))
            {
                doesLoaderOfTypeExist = managerSettings.activeLoaders.Any(loader => loader is LightshipStandaloneLoader);
            }
            else if (lightshipLoaderType == typeof(LightshipSimulationLoader))
            {
                doesLoaderOfTypeExist = managerSettings.activeLoaders.Any(loader => loader is LightshipSimulationLoader);
            }
            return managerSettings != null && doesLoaderOfTypeExist;
        }

        internal static bool IsUnitySimulationPluginEnabled()
        {
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (null == generalSettings)
                return false;

            var managerSettings = generalSettings.AssignedSettings;
            if (null == managerSettings)
                return false;

            var simulationLoaderIsActive = managerSettings.activeLoaders.Any(loader => loader is SimulationLoader);
            return simulationLoaderIsActive;
        }
    }
}
