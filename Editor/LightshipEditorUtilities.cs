// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Linq;
using Niantic.Lightship.AR.Loader;
using UnityEditor;
using UnityEditor.XR.Management;

namespace Niantic.Lightship.AR.Editor
{
    static class LightshipEditorUtilities
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
            return managerSettings != null && doesLoaderOfTypeExist;
        }
    }
}
