// Copyright 2022-2024 Niantic.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Occlusion;
using Niantic.Lightship.AR.Subsystems.Playback;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

using UnityEditor.Rendering;
using UnityEditor.XR.ARSubsystems;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;

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
                foreach (string shaderName in LightshipPlaybackCameraSubsystem.BackgroundShaderNames)
                {
                    BuildHelper.RemoveShaderFromProject(shaderName);
                }

                if (report.summary.platform == BuildTarget.iOS)
                {
                    PostProcessIosBuild(report.summary.outputPath);
                }
            }

            private static void PostProcessIosUnityAppControllerFix(string buildPath)
            {
                Log.Info
                (
                    "Lightship has identified this version of Unity as having a bug that affects the " +
                    "initialization of custom-defined integrated subsystems, and has applied a fix for it."
                );

                // Apply patch for Unity timing bug:
                //  Initial: https://support.unity.com/hc/en-us/requests/1630624
                //  Reopened: https://support.unity.com/hc/en-us/requests/1745237
                // We're going to do a simple replace for any UnityAppController.mm which includes the buggy line.
                // Affects release versions 2021.3.24-29, 2022.2.13-21,3.0-6, 2023.1.0-7
                const string buggyLine = "[self startUnity: application];";
                const string fixedLine = "[self performSelector: @selector(startUnity:) withObject: application afterDelay: 0];";
                string appControllerPath = buildPath + "/Classes/UnityAppController.mm";
                var appControllerLines = File.ReadAllLines(appControllerPath);
                int index = Array.FindIndex(appControllerLines, line => line.Contains(buggyLine));
                if (index >= 0)
                {
                    appControllerLines[index] = appControllerLines[index].Replace(buggyLine, fixedLine);
                    File.WriteAllLines(appControllerPath, appControllerLines);
                }
            }

            [DllImport("libc")] //https://man7.org/linux/man-pages/man3/realpath.3.html
            private static extern IntPtr realpath([In, MarshalAs(UnmanagedType.LPUTF8Str)] string path, [Out] byte[] buf);
            private static void ReplaceFrameworksSymlinks(string directoryPath)
            {
                foreach (string filePath in Directory.GetFiles(directoryPath)) {
                    if (IsSymlink(new FileInfo(filePath))) {
                        string targetPath = SymlinkRealPath(filePath);
                        if (!string.IsNullOrEmpty(targetPath)) {
                            File.Delete(filePath);
                            File.Copy(targetPath, filePath);
                        }
                    }
                }

                foreach (string subdirectoryPath in Directory.GetDirectories(directoryPath))
                    ReplaceFrameworksSymlinks(subdirectoryPath);
            }

            private static bool IsSymlink(FileInfo pathInfo) => pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);

            private static string SymlinkRealPath(string path)
            {
                var buffer = new byte[4096];
                var result = realpath(path, buffer);
                if (result == IntPtr.Zero) throw new Exception($"Failed to call realpath for {path}");
                return Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0));
            }

            private static void PostProcessIosBuild(string buildPath)
            {
#if UNITY_IOS
                Log.Info($"Running {nameof(PostProcessIosBuild)}");

                string projectPath = PBXProject.GetPBXProjectPath(buildPath);
                ReplaceFrameworksSymlinks(Path.Combine(buildPath, "Frameworks"));
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

#if (FIX_INTEGRATED_SUBSYSTEM_2021 && UNITY_2021_3_OR_NEWER && !UNITY_2022) || (FIX_INTEGRATED_SUBSYSTEM_2022 && UNITY_2022)
                PostProcessIosUnityAppControllerFix(buildPath);
#endif
#endif // UNITY_IOS
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
                foreach (string backgroundShaderName in LightshipPlaybackCameraSubsystem.BackgroundShaderNames)
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
                foreach (string backgroundShaderName in LightshipPlaybackCameraSubsystem.BackgroundShaderNames)
                {
                    BuildHelper.AddBackgroundShaderToProject(backgroundShaderName);
                }

                BuildHelper.AddBackgroundShaderToProject(LightshipOcclusionExtension.DefaultShaderName);
                BuildHelper.AddBackgroundShaderToProject(LightshipFusedDepthCamera.DefaultShaderName);

                // TODO: Things that ARKit and ARCore BuildProcessor implementations do
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
                bool loaderIsOpenXR = false;
                foreach (var loader in generalSettings.Manager.activeLoaders)
                {
                    loaderIsOpenXR = loader is OpenXRLoader;   //TODO: make this actually check for spaces
                    if (loader is ILightshipInternalLoaderSupport || loaderIsOpenXR)
                    {
                        LightshipBuildProcessor.loaderEnabled = true;
                        break;
                    }
                }

                if (LightshipBuildProcessor.loaderEnabled && !previousLoaderEnabled)
                {
                    if (loaderIsOpenXR)
                    {
                        // TODO(ARDK-3724): Add speicific define for OpenXR
                        // AddDefineSymbols.Add("NIANTIC_LIGHTSHIP_SPACES_ENABLED");
                    }

                    AddDefineSymbols.Add("NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED");
                    AddDefineSymbols.Add("NIANTIC_ARDK_USE_FAST_LIGHTWEIGHT_PAM");
                }
                else if (!LightshipBuildProcessor.loaderEnabled && previousLoaderEnabled)
                {
                    AddDefineSymbols.Remove("NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED");
                    AddDefineSymbols.Remove("NIANTIC_ARDK_USE_FAST_LIGHTWEIGHT_PAM");
                    AddDefineSymbols.Remove("NIANTIC_LIGHTSHIP_SPACES_ENABLED");
                }
            }
        }
    }
}
