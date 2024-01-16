// Copyright 2022 - 2023 Niantic.

using Niantic.Lightship.AR.Utilities.Log;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    // TODO(ARDK-2283) - Remove this class once Project Validation is fixed and we know what version of ARF to upgrade to
    // Support ticket link: https://support.unity.com/hc/en-us/requests/1729605
    internal class LightshipSDKProjectValidationBuildPreprocess : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTargetGroup platformGroup = report.summary.platformGroup;

            bool lightshipPluginEnabled = platformGroup switch
            {
                BuildTargetGroup.Standalone => LightshipEditorUtilities.GetStandaloneIsLightshipPluginEnabled(),
                BuildTargetGroup.iOS => LightshipEditorUtilities.GetIosIsLightshipPluginEnabled(),
                BuildTargetGroup.Android => LightshipEditorUtilities.GetAndroidIsLightshipPluginEnabled(),
                _ => false
            };

            if (!lightshipPluginEnabled)
            {
                return;
            }

            bool ruleFailed = false;
            foreach (var rule in LightshipSDKProjectValidationRules.PlatformRules[platformGroup])
            {
                if (rule.IsRuleEnabled.Invoke() && rule.Error && !rule.CheckPredicate.Invoke())
                {
                    Log.Error($"Lightship SDK Project Validation failed: '{rule.Message}'. See Project Validation window for more details.");
                    ruleFailed = true;
                }
            }

            if (ruleFailed)
            {
                throw new BuildFailedException("Lightship SDK build failed. All errors in Project Validation must be fixed.");
            }
        }

        [OnOpenAsset(0)]
        static bool GoToProjectValidation(int instanceId, int line)
        {
            // If the user double clicks on an error from this script, open the Project Validation window
            if (EditorUtility.InstanceIDToObject(instanceId).name != "LightshipSDKProjectValidationBuildPreprocess" ||
                line == -1)
            {
                return false;
            }

            EditorApplication.delayCall += () =>
            {
                SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Project Validation");
            };
            return true;
        }
    }
}
