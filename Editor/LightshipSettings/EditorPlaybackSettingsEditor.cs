// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Loader;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    public class EditorPlaybackSettingsEditor : IPlaybackSettingsEditor
    {
        private ILightshipPlaybackSettings _editorPlaybackSettings;

        private static class Contents
        {
            public static readonly GUIContent usePlaybackLabel =
                new GUIContent
                (
                    "Enabled",
                    "When enabled, a dataset will be used to simulate live AR input in Play Mode."
                );

            public static readonly GUIContent datasetPathLabel =
                new GUIContent
                (
                    "Dataset Path",
                    "The absolute path to the folder containing the dataset to use for playback in-Editor. " +
                    "Does not need to be within the Unity project directory."
                );

            public static readonly GUIContent runManuallyLabel =
                new GUIContent
                (
                    "Run Manually",
                    "Use the space bar to move forward frame by frame."
                );

            public static readonly GUIContent loopInfinitelyLabel =
                new GUIContent
                (
                    "Loop Infinitely",
                    "When enabled, the playback dataset will play continuously rather than halt at the end. " +
                    "To prevent immediate jumps in pose and tracking, the dataset will alternate running forwards and backwards."
                );
        }

        public void InitializeSerializedProperties(SerializedObject lightshipSettings)
        {
            _editorPlaybackSettings = LightshipSettings.Instance.EditorPlaybackSettings;
        }

        public void DrawGUI()
        {
            var currUsedPlayback = _editorPlaybackSettings.UsePlayback;
            var newUsePlayback = EditorGUILayout.Toggle(Contents.usePlaybackLabel, currUsedPlayback);
            if (newUsePlayback != currUsedPlayback)
            {
                _editorPlaybackSettings.UsePlayback = newUsePlayback;
            }

            EditorGUI.BeginDisabledGroup(!newUsePlayback);

            DrawDatasetPathGUI();

            var currRunManually = _editorPlaybackSettings.RunManually;
            var changedRunManually = EditorGUILayout.Toggle(Contents.runManuallyLabel, currRunManually);
            if (changedRunManually != currRunManually)
            {
                _editorPlaybackSettings.RunManually = changedRunManually;
            }

            var currLoopInfinitely = _editorPlaybackSettings.LoopInfinitely;
            var changedLoopInfinitely = EditorGUILayout.Toggle(Contents.loopInfinitelyLabel, currLoopInfinitely);
            if (changedLoopInfinitely != currLoopInfinitely)
            {
                _editorPlaybackSettings.LoopInfinitely = changedLoopInfinitely;
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawDatasetPathGUI()
        {
            EditorGUILayout.BeginHorizontal();

            var currPath = _editorPlaybackSettings.PlaybackDatasetPath;
            var changedPath = EditorGUILayout.TextField(Contents.datasetPathLabel, currPath);

            if (changedPath != currPath)
            {
                _editorPlaybackSettings.PlaybackDatasetPath = changedPath;
            }

            var browse = GUILayout.Button("Browse", GUILayout.Width(125));

            EditorGUILayout.EndHorizontal();

            if (browse)
            {
                var browsedPath =
                    EditorUtility.OpenFolderPanel
                    (
                        "Select Dataset Directory",
                        changedPath,
                        ""
                    );

                if (browsedPath.Length > 0)
                {
                    _editorPlaybackSettings.PlaybackDatasetPath = browsedPath;
                }
            }
        }
    }
}
