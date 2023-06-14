using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Playback;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

using UnityEditor.Rendering;
using UnityEditor.XR.ARSubsystems;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;

namespace Niantic.Lightship.AR.Editor
{
    internal class LightshipBuildProcessor : XRBuildHelper<LightshipSettings>
    {
        public static bool loaderEnabled;
        public override string BuildSettingsKey => LightshipSettings.SettingsKey;

        private class PostProcessor : IPostprocessBuildWithReport
        {
            // Needs to be > 0 to make sure we remove the shader since the
            // Input System overwrites the preloaded assets array
            public int callbackOrder => 1;

            public void OnPostprocessBuild(BuildReport report)
            {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                PostprocessBuild(report);
#endif
            }

            private void PostprocessBuild(BuildReport report)
            {
                foreach (string shaderName in LightshipPlaybackCameraSubsystem.backgroundShaderNames)
                {
                    BuildHelper.RemoveShaderFromProject(shaderName);
                }

                if (report.summary.platform == BuildTarget.iOS)
                {
                    PostProcessIosBuild(report.summary.outputPath);
                }
            }

            private static void PostProcessIosBuild(string buildPath)
            {
#if UNITY_IOS
                Debug.Log($"Running {nameof(PostProcessIosBuild)}");

                string projectPath = PBXProject.GetPBXProjectPath(buildPath);
                var project = new PBXProject();
                project.ReadFromFile(projectPath);

                // Set xcode project target settings
                string mainTarget = project.GetUnityMainTargetGuid();
                string unityFrameworkTarget = project.GetUnityFrameworkTargetGuid();
                string unityTestFrameworkTarget = project.TargetGuidByName(PBXProject.GetUnityTestTargetName());
                var xcodeProjectTargets = new[] { mainTarget, unityFrameworkTarget, unityTestFrameworkTarget };
                foreach (string xcodeProjectTarget in xcodeProjectTargets)
                {
                    // Disable bitcode
                    project.SetBuildProperty(xcodeProjectTarget, "ENABLE_BITCODE", "NO");
                }

                project.WriteToFile(projectPath);
#endif
            }
        }

        private class Preprocessor : IPreprocessBuildWithReport, IPreprocessShaders
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                PreprocessBuild(report);
#endif
            }

            public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
            {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                ProcessShader(shader, snippet, data);
#endif
            }

            private void ProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
            {
                // Remove shader variants for the camera background shader that will fail compilation because of package dependencies.
                foreach (string backgroundShaderName in LightshipPlaybackCameraSubsystem.backgroundShaderNames)
                {
                    if (backgroundShaderName.Equals(shader.name))
                    {
                        foreach (string backgroundShaderKeywordToNotCompile in LightshipPlaybackCameraSubsystem
                                     .backgroundShaderKeywordsToNotCompile)
                        {
                            var shaderKeywordToNotCompile =
                                new ShaderKeyword(shader, backgroundShaderKeywordToNotCompile);

                            for (int i = data.Count - 1; i >= 0; --i)
                            {
                                if (data[i].shaderKeywordSet.IsEnabled(shaderKeywordToNotCompile))
                                {
                                    data.RemoveAt(i);
                                }
                            }
                        }
                    }
                }
            }

            private void PreprocessBuild(BuildReport report)
            {
                foreach (string backgroundShaderName in LightshipPlaybackCameraSubsystem.backgroundShaderNames)
                {
                    BuildHelper.AddBackgroundShaderToProject(backgroundShaderName);
                }

                // TODO: Things that ARKit and ARCore BuildProcessor implementations doe
                // - Check camera usage description
                // - Ensure minimum build targets
                // - handle ARKit/ARCore required flags
                // - etc.
            }
        }
    }

    internal static class AddDefineSymbols
    {
        public static void Add(string define)
        {
            var buildTarget =
                NamedBuildTarget.FromBuildTargetGroup(
                    BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            string definesString = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            var allDefines = new HashSet<string>(definesString.Split(';'));

            if (allDefines.Contains(define))
            {
                return;
            }

            allDefines.Add(define);
            PlayerSettings.SetScriptingDefineSymbols(
                buildTarget,
                string.Join(";", allDefines));
        }

        public static void Remove(string define)
        {
            var buildTarget =
                NamedBuildTarget.FromBuildTargetGroup(
                    BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            string definesString = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            var allDefines = new HashSet<string>(definesString.Split(';'));
            allDefines.Remove(define);
            PlayerSettings.SetScriptingDefineSymbols(
                buildTarget,
                string.Join(";", allDefines));
        }
    }

    [InitializeOnLoad]
    internal class LoaderEnabledCheck
    {
        static LoaderEnabledCheck()
        {
            LightshipBuildProcessor.loaderEnabled = false;

            UpdateLightshipDefines();
            EditorCoroutineUtility.StartCoroutineOwnerless(UpdateLightshipDefinesCoroutine());
        }

        private static IEnumerator UpdateLightshipDefinesCoroutine()
        {
            var waitObj = new EditorWaitForSeconds(.25f);

            while (true)
            {
                UpdateLightshipDefines();
                yield return waitObj;
            }
        }

        private static void UpdateLightshipDefines()
        {
            bool previousLoaderEnabled = LightshipBuildProcessor.loaderEnabled;

            var generalSettings =
                XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(
                    BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            if (generalSettings != null)
            {
                LightshipBuildProcessor.loaderEnabled = false;
                foreach (var loader in generalSettings.Manager.activeLoaders)
                {
                    if (loader is LightshipStandaloneLoader || loader is LightshipARCoreLoader ||
                        loader is LightshipARKitLoader)
                    {
                        LightshipBuildProcessor.loaderEnabled = true;
                        break;
                    }
                }

                if (LightshipBuildProcessor.loaderEnabled && !previousLoaderEnabled)
                {
                    AddDefineSymbols.Add("NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED");
                }
                else if (!LightshipBuildProcessor.loaderEnabled && previousLoaderEnabled)
                {
                    AddDefineSymbols.Remove("NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED");
                }
            }
        }
    }
}
