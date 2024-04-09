// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;

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

            bool anyRuleFailed = false;
            bool errorRuleFailed = false;
            List<string> warnings = new List<string>();
            foreach (var rule in LightshipSDKProjectValidationRules.PlatformRules[platformGroup])
            {
                if (rule.IsRuleEnabled.Invoke() && !rule.CheckPredicate.Invoke())
                {
                    if (rule.Error)
                    {
                        Log.Error
                        (
                            $"Lightship SDK Project Validation failed: '{rule.Message}'. See Project Validation window for more details."
                        );
                        errorRuleFailed = true;
                    }
                    else
                    {
                        warnings.Add($"Lightship SDK Project Validation warning: '{rule.Message}'. See Project Validation window for more details.");
                    }

                    anyRuleFailed = true;
                }
            }

            if (anyRuleFailed)
            {
                EditorApplication.delayCall += () =>
                {
                    GoToProjectValidation();
                    foreach (string warning in warnings)
                    {
                        Log.Warning(warning);
                    }
                };
            }
            if (errorRuleFailed)
            {
                throw new BuildFailedException
                (
                    "Lightship SDK build failed. All errors in Project Validation must be fixed."
                );
            }
        }

        [OnOpenAsset(0)]
        private static bool OnDoubleClicked(int instanceId, int line)
        {
            // If the user double clicks on an error from this script, open the Project Validation window
            if (EditorUtility.InstanceIDToObject(instanceId).name != "LightshipSDKProjectValidationBuildPreprocess" ||
                line == -1)
            {
                return false;
            }

            GoToProjectValidation();
            return true;
        }

        private static void GoToProjectValidation()
        {
            EditorApplication.delayCall += () =>
            {
                SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Project Validation");
            };
        }
    }
}
