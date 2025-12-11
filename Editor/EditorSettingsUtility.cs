using System;
using System.IO;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    /// <summary>
    /// Interface to singleton representing editor settings paths
    /// </summary>
    internal interface IEditorSettingsUtility
    {
        /// <summary>
        /// Location for editor settings.
        /// On MacOs, this is an .ardkSettings folder under the user path.
        /// On Windows, the is an ardkSettings folder under C:\ProgramData
        /// </summary>
        string EditorSettingsPath { get; }

        /// <summary>
        /// Path settings specific to the current project.
        /// This is a hashed directory under the editor settings path.
        /// </summary>
        string ProjectSettingsPath { get; }
    }

    internal class EditorSettingsUtility : IEditorSettingsUtility
    {
        // For MacOS utilise first character period convention to aid in hiding:
        private const string SettingsFolderMacOs = ".ardkSettings";
        private const string SettingsFolderWindows = "ardkSettings";

        /// <summary>
        /// Private constructor to enforce singleton
        /// </summary>
        private EditorSettingsUtility()
        {
        }

        public static readonly IEditorSettingsUtility Instance = new EditorSettingsUtility();

        public string EditorSettingsPath {
            get {
                // Use different root folders per OS for best compatibility
                if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    // Use the user's home directory on Mac
                    // Path: /Users/YourUsername
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), SettingsFolderMacOs);
                }

                // Use the common data path for Windows
                // Path: C:\ProgramData
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), SettingsFolderWindows);
            }
        }

        public string ProjectSettingsPath
        {
            get
            {
                // Generate a unique ID for the project location that we can use as a directory name.
                // Note: this will fail if the project is moved, but is otherwise the most reliable method for
                // creating a unique ID for a project. Other options with Unity:
                // 1. Utilise the Library folder ProjectGuid (fails if the Library is ever rebuilt)
                // 2. Application.identifier (only works if set for the project)
                // 3. Application.cloudProjectId (only works if cloud services setup)
                Hash128 pathHash = Hash128.Compute(Application.dataPath);
                return Path.Combine(EditorSettingsPath, pathHash.ToString());
            }
        }
    }
}
