// Copyright 2022-2024 Niantic.

using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    public class DevicePlaybackSettingsEditor: IPlaybackSettingsEditor
    {
        private SerializedProperty _deviceUsePlayback;
        private SerializedProperty _deviceDatasetPath;
        private SerializedProperty _deviceRunManually;
        private SerializedProperty _deviceLoopInfinitely;

        public void InitializeSerializedProperties(SerializedObject lightshipSettings)
        {
            _deviceUsePlayback =  lightshipSettings.FindProperty("_devicePlaybackSettings._usePlayback");
            _deviceDatasetPath =  lightshipSettings.FindProperty("_devicePlaybackSettings._playbackDatasetPath");
            _deviceRunManually =  lightshipSettings.FindProperty("_devicePlaybackSettings._runPlaybackManually");
            _deviceLoopInfinitely =  lightshipSettings.FindProperty("_devicePlaybackSettings._loopInfinitely");
        }

        public void DrawGUI()
        {
            EditorGUILayout.PropertyField(_deviceUsePlayback, new GUIContent("Enabled"));

            EditorGUI.BeginDisabledGroup(!_deviceUsePlayback.boolValue);

            DrawDatasetPathGUI();
            EditorGUILayout.PropertyField(_deviceRunManually, new GUIContent("Run Manually"));
            EditorGUILayout.PropertyField(_deviceLoopInfinitely, new GUIContent("Loop Infinitely"));

            EditorGUI.EndDisabledGroup();
        }

        private void DrawDatasetPathGUI()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(_deviceDatasetPath, new GUIContent("Dataset Path"));

            if (GUILayout.Button("Browse", GUILayout.Width(125)))
            {
                var path = EditorUtility.OpenFolderPanel
                (
                    "Select Dataset Directory",
                    _deviceDatasetPath.stringValue,
                    ""
                );

                if (path.Length > 0)
                {
                    _deviceDatasetPath.stringValue = path;
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
