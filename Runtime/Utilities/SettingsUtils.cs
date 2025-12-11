using System.IO;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities
{
    /// <summary>
    /// Utility class for creating and retrieving settings assets.
    /// </summary>
    internal static class SettingsUtils
    {
#if UNITY_EDITOR
        private const string AssetsPath = "Assets";
        private const string AssetsRelativeSettingsPath = "XR/Settings";

        private static T CreateInstanceAsset<T>(string name) where T : ScriptableObject
        {
            var settings = ScriptableObject.CreateInstance<T>();
            // ensure all parent directories of settings asset exists
            var settingsPath = Path.Combine(AssetsPath, AssetsRelativeSettingsPath, $"{name}.asset");
            var pathSplits = settingsPath.Split("/");
            var runningPath = pathSplits[0];
            for (var i = 1; i < pathSplits.Length - 1; i++)
            {
                var pathSplit = pathSplits[i];
                var nextPath = Path.Combine(runningPath, pathSplit);
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(runningPath, pathSplit);
                }

                runningPath = nextPath;
            }

            // create settings asset at specified path
            AssetDatabase.CreateAsset(settings, settingsPath);
            return settings;
        }

        /// <summary>
        /// Create or retrieve a settings asset of the specified type.
        /// If it doesn't exist, the asset will be created in the Assets/XR/Settings folder.
        /// </summary>
        /// <param name="settingsKey">The unique key for the asset in EditorBuildSettings</param>
        /// <param name="name">the filename of the asset (excluding .asset)</param>
        /// <typeparam name="T">The type of the asset</typeparam>
        /// <returns>current instance of the asset</returns>
        public static T GetOrCreateSettingsAsset<T>(string settingsKey, string name) where T : ScriptableObject
        {
            if (!EditorBuildSettings.TryGetConfigObject(settingsKey, out T settings))
            {
                settings = CreateInstanceAsset<T>(name);
                EditorBuildSettings.AddConfigObject(settingsKey, settings, true);
            }

            return settings;
        }
#endif

        /// <summary>
        /// Save the given Unity object immediately, along with any changes.
        /// </summary>
        /// <param name="obj">the object</param>
        public static void SaveImmediatelyInEditor(Object obj)
        {
#if UNITY_EDITOR
            if (obj == null)
            {
                return;
            }
            EditorUtility.SetDirty(obj);
            AssetDatabase.SaveAssetIfDirty(obj);
#endif
        }
    }
}
