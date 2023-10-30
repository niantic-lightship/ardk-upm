// Copyright 2023 Niantic, Inc. All Rights Reserved.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Loader;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.Editor
{
    /// <summary>
    /// This Editor renders to the XR Plug-in Management category of the Project Settings window.
    /// </summary>
    [CustomEditor(typeof(LightshipSettings))]
    class LightshipSettingsEditor : UnityEditor.Editor
    {
        private enum Platform
        {
            Editor = 0,
            Device = 1
        }

        static class Contents
        {
            public static readonly GUIContent[] _platforms =
            {
                new GUIContent
                (
                    Platform.Editor.ToString(),
                    "Playback settings for Play Mode in the Unity Editor"
                ),
                new GUIContent
                (
                    Platform.Device.ToString(),
                    "Playback settings for running on a physical device"
                )
            };

            public static readonly GUIContent apiKeyLabel = new GUIContent("API Key");
            public static readonly GUIContent enabledLabel = new GUIContent("Enabled");
            public static readonly GUIContent preferLidarLabel = new GUIContent("Prefer LiDAR if Available");
        }

        private int _platformSelected = 0;

        private SerializedObject _lightshipSettings;

        private SerializedProperty _apiKeyProperty;
        private SerializedProperty _useLightshipDepthProperty;
        private SerializedProperty _useLightshipMeshingProperty;
        private SerializedProperty _preferLidarIfAvailableProperty;
        private SerializedProperty _useLightshipPersistentAnchorProperty;
        private SerializedProperty _useLightshipSemanticSegmentationProperty;
        private SerializedProperty _useLightshipScanningProperty;
        private SerializedProperty _overrideLoggingLevelProperty;
        private SerializedProperty _logLevelProperty;

        private IPlaybackSettingsEditor[] _playbackSettingsEditors;
        private Texture _enabledIcon;
        private Texture _disabledIcon;

        void OnEnable()
        {
            _lightshipSettings = new SerializedObject(LightshipSettings.Instance);
            _apiKeyProperty = _lightshipSettings.FindProperty("_apiKey");
            _useLightshipDepthProperty = _lightshipSettings.FindProperty("_useLightshipDepth");
            _useLightshipMeshingProperty = _lightshipSettings.FindProperty("_useLightshipMeshing");
            _preferLidarIfAvailableProperty = _lightshipSettings.FindProperty("_preferLidarIfAvailable");
            _useLightshipPersistentAnchorProperty = _lightshipSettings.FindProperty("_useLightshipPersistentAnchor");
            _useLightshipSemanticSegmentationProperty = _lightshipSettings.FindProperty
                ("_useLightshipSemanticSegmentation");
            _useLightshipScanningProperty = _lightshipSettings.FindProperty("_useLightshipScanning");
            _overrideLoggingLevelProperty = _lightshipSettings.FindProperty("_overrideLoggingLevel");
            _logLevelProperty = _lightshipSettings.FindProperty("_logLevel");

            _playbackSettingsEditors =
                new IPlaybackSettingsEditor[] { new EditorDeviceSettingsEditor(), new DevicePlaybackSettingsEditor() };

            foreach (var editor in _playbackSettingsEditors)
            {
                editor.InitializeSerializedProperties(_lightshipSettings);
            }

            _enabledIcon = EditorGUIUtility.IconContent("TestPassed").image;
            _disabledIcon = EditorGUIUtility.IconContent("Warning").image;
        }

        public override void OnInspectorGUI()
        {
            _lightshipSettings.Update();

            using (var change = new EditorGUI.ChangeCheckScope())
            {
                // Disable changes during runtime
                EditorGUI.BeginDisabledGroup(Application.isPlaying);

                EditorGUIUtility.labelWidth = 175;

                EditorGUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                var editorSDKEnabled = LightshipEditorUtilities.GetStandaloneIsLightshipPluginEnabled();
                var editorContent = editorSDKEnabled
                    ? new GUIContent("Editor : SDK Enabled", _enabledIcon)
                    : new GUIContent("Editor : SDK Disabled", _disabledIcon);
                EditorGUILayout.LabelField(editorContent, EditorStyles.boldLabel);
                var androidSDKEnabled = LightshipEditorUtilities.GetAndroidIsLightshipPluginEnabled();
                var androidContent = androidSDKEnabled
                    ? new GUIContent("Android : SDK Enabled", _enabledIcon)
                    : new GUIContent("Android : SDK Disabled", _disabledIcon);
                EditorGUILayout.LabelField(androidContent, EditorStyles.boldLabel);
                var iosSDKEnabled = LightshipEditorUtilities.GetIosIsLightshipPluginEnabled();
                var iosContent = iosSDKEnabled
                    ? new GUIContent("iOS : SDK Enabled", _enabledIcon)
                    : new GUIContent("iOS : SDK Disabled", _disabledIcon);
                EditorGUILayout.LabelField(iosContent, EditorStyles.boldLabel);

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(10);

                EditorGUILayout.LabelField("Credentials", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_apiKeyProperty, Contents.apiKeyLabel);

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipDepthProperty, Contents.enabledLabel);
                EditorGUI.indentLevel++;
                // Put Depth sub-settings here
                if (_useLightshipDepthProperty.boolValue)
                {
                    EditorGUILayout.PropertyField(_preferLidarIfAvailableProperty, Contents.preferLidarLabel);
                }

                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Semantic Segmentation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipSemanticSegmentationProperty, Contents.enabledLabel);

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Meshing", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipMeshingProperty, Contents.enabledLabel);
                EditorGUI.indentLevel++;
                // Put Meshing sub-settings here
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Persistent Anchors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipPersistentAnchorProperty, Contents.enabledLabel);
                EditorGUI.indentLevel++;
                // Put Persistent Anchors sub-settings here
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Scanning", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipScanningProperty, Contents.enabledLabel);
                EditorGUI.indentLevel++;
                // Put Scanning sub-settings here
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                _platformSelected = GUILayout.Toolbar(_platformSelected, Contents._platforms);
                GUILayout.EndHorizontal();

                _playbackSettingsEditors[_platformSelected].DrawGUI();

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Logging", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_overrideLoggingLevelProperty, new GUIContent("Override Log Level Visibility"));
                EditorGUI.indentLevel++;
                // Put Logging sub-settings here
                if (_overrideLoggingLevelProperty.boolValue)
                {
                    EditorGUILayout.PropertyField
                    (
                        _logLevelProperty,
                        new GUIContent("Lowest Log Level to Display")
                    );
                }

                EditorGUI.indentLevel--;

                EditorGUI.EndDisabledGroup();

                if (change.changed)
                {
                    _lightshipSettings.ApplyModifiedProperties();
                }
            }
        }
    }
}
