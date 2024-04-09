// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Loader;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    /// <summary>
    /// This Editor renders to the XR Plug-in Management category of the Project Settings window.
    /// </summary>
    [CustomEditor(typeof(LightshipSettings))]
    internal class LightshipSettingsEditor : UnityEditor.Editor
    {
        internal const string ProjectValidationSettingsPath = "Project/XR Plug-in Management/Project Validation";
        internal const string XRPluginManagementPath = "Project/XR Plug-in Management";

        private enum Platform
        {
            Editor = 0,
            Device = 1
        }

        private static class Contents
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
            public static readonly GUIContent preferLidarLabel = new GUIContent
                ("Prefer LiDAR if Available");
            public static readonly GUIContent environmentPrefabLabel =
                new GUIContent("Environment Prefab");
            public static readonly GUIContent useZBufferDepthInSimulationLabel =
                new GUIContent("Use Z-Buffer Depth");
            public static readonly GUIContent useLightshipPersistentAnchorInSimulationLabel =
                new GUIContent("Use Lightship Persistent Anchors");

            private static GUIStyle _sdkEnabledStyle;

            public static GUIStyle sdkEnabledStyle
            {
                get
                {
                    return _sdkEnabledStyle ??= new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                }
            }

            public static readonly GUILayoutOption[] sdkEnabledOptions =
            {
                GUILayout.MinWidth(0)
            };

            private static GUIStyle _sdkDisabledStyle;

            public static GUIStyle sdkDisabledStyle
            {
                get
                {
                    return _sdkDisabledStyle ??= new GUIStyle(EditorStyles.miniButton)
                    {
                        stretchWidth = true, fontStyle = FontStyle.Bold
                    };
                }
            }

            private static GUIStyle _boldFont18Style;

            public static GUIStyle boldFont18Style
            {
                get
                {
                    return _boldFont18Style ??= new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 18
                    };
                }
            }
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
        private SerializedProperty _useLightshipObjectDetectionProperty;
        private SerializedProperty _unityLogLevelProperty;
        private SerializedProperty _fileLogLevelProperty;
        private SerializedProperty _stdOutLogLevelProperty;
        private SerializedProperty _environmentPrefabProperty;
        private SerializedProperty _useZBufferDepthInSimulationProperty;
        private SerializedProperty _useLightshipPersistentAnchorInSimulationProperty;
        private SerializedProperty _lightshipPersistentAnchorParamsProperty;
        private IPlaybackSettingsEditor[] _playbackSettingsEditors;
        private Texture _enabledIcon;
        private Texture _disabledIcon;
        private GameObject _previousEnvironmentPrefab;

        private void OnEnable()
        {
            _lightshipSettings = new SerializedObject(LightshipSettings.Instance);

            _apiKeyProperty = _lightshipSettings.FindProperty("_apiKey");
            _useLightshipDepthProperty = _lightshipSettings.FindProperty("_useLightshipDepth");
            _useLightshipMeshingProperty = _lightshipSettings.FindProperty("_useLightshipMeshing");
            _preferLidarIfAvailableProperty = _lightshipSettings.FindProperty("_preferLidarIfAvailable");
            _useLightshipPersistentAnchorProperty = _lightshipSettings.FindProperty("_useLightshipPersistentAnchor");
            _useLightshipSemanticSegmentationProperty =
                _lightshipSettings.FindProperty("_useLightshipSemanticSegmentation");
            _useLightshipScanningProperty = _lightshipSettings.FindProperty("_useLightshipScanning");
            _useLightshipObjectDetectionProperty =
                _lightshipSettings.FindProperty("_useLightshipObjectDetection");
                _lightshipSettings.FindProperty("__useLightshipObjectDetectionProperty");
            _unityLogLevelProperty = _lightshipSettings.FindProperty("_unityLogLevel");
            _fileLogLevelProperty = _lightshipSettings.FindProperty("_fileLogLevel");
            _stdOutLogLevelProperty = _lightshipSettings.FindProperty("_stdoutLogLevel");
            // Simulation sub-properties
            _environmentPrefabProperty = _lightshipSettings.FindProperty("_lightshipSimulationParams._environmentPrefab");
            _useZBufferDepthInSimulationProperty = _lightshipSettings.FindProperty("_lightshipSimulationParams._useZBufferDepth");
            _useLightshipPersistentAnchorInSimulationProperty = _lightshipSettings.FindProperty("_lightshipSimulationParams._useLightshipPersistentAnchor");
            _lightshipPersistentAnchorParamsProperty = _lightshipSettings.FindProperty("_lightshipSimulationParams._simulationPersistentAnchorParams");

            _playbackSettingsEditors =
                new IPlaybackSettingsEditor[] { new EditorPlaybackSettingsEditor(), new DevicePlaybackSettingsEditor() };

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

                // -- Put new Lightship settings here --
                DrawLightshipSettings();

                // -- Put new simulation settings here --
                DrawLightshipSimulationSettings();

                EditorGUI.EndDisabledGroup();
                if (change.changed)
                {
                    _lightshipSettings.ApplyModifiedProperties();
                }
            }
        }

        private void DrawLightshipSettings()
        {
            EditorGUIUtility.labelWidth = 220;

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            var editorSDKEnabled =
                LightshipEditorUtilities.GetStandaloneIsLightshipPluginEnabled() ||
                IsLightshipSimulatorEnabled();
            var androidSDKEnabled = LightshipEditorUtilities.GetAndroidIsLightshipPluginEnabled();
            var iosSDKEnabled = LightshipEditorUtilities.GetIosIsLightshipPluginEnabled();

            LayOutSDKEnabled("Editor", editorSDKEnabled, BuildTargetGroup.Standalone);
            LayOutSDKEnabled("Android", androidSDKEnabled, BuildTargetGroup.Android);
            LayOutSDKEnabled("iOS", iosSDKEnabled, BuildTargetGroup.iOS);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Credentials", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_apiKeyProperty, Contents.apiKeyLabel);

            var navigateToLightship = GUILayout.Button("Get API Key", GUILayout.Width(125));
            if (navigateToLightship)
            {
                Application.OpenURL("https://lightship.dev/account/projects");
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useLightshipDepthProperty, Contents.enabledLabel);
            EditorGUI.indentLevel++;
            // Put Depth sub-settings here
            if (_useLightshipDepthProperty.boolValue)
            {
                EditorGUILayout.PropertyField
                    (_preferLidarIfAvailableProperty, Contents.preferLidarLabel);
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Semantic Segmentation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
                (_useLightshipSemanticSegmentationProperty, Contents.enabledLabel);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Meshing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useLightshipMeshingProperty, Contents.enabledLabel);
            EditorGUI.indentLevel++;
            // Put Meshing sub-settings here
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Persistent Anchors", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
                (_useLightshipPersistentAnchorProperty, Contents.enabledLabel);

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
            EditorGUILayout.LabelField("Object Detection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
                (_useLightshipObjectDetectionProperty, Contents.enabledLabel);

            EditorGUI.indentLevel++;
            // Put Object Detection sub-settings here
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            _platformSelected = GUILayout.Toolbar(_platformSelected, Contents._platforms);
            GUILayout.EndHorizontal();

            _playbackSettingsEditors[_platformSelected].DrawGUI();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Logging", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField
            (
                _unityLogLevelProperty,
                new GUIContent
                    ("Unity Log Level", tooltip: "Log level for Unity's built-in logging system")
            );

            EditorGUILayout.PropertyField
            (
                _stdOutLogLevelProperty,
                new GUIContent
                (
                    "Stdout Log Level",
                    tooltip: "Log level for stdout logging system. Recommended to be set to 'off'"
                )
            );

            EditorGUILayout.PropertyField
            (
                _fileLogLevelProperty,
                new GUIContent
                (
                    "File Log Level",
                    tooltip: "Log level for logging things into a file. " +
                    "Recommended to be set to 'off' unless its a niantic support case. File Location: [add location here]"
                )
            );

            EditorGUI.indentLevel--;
        }

        private void DrawLightshipSimulationSettings()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField
            (
                "Simulation",
                Contents.boldFont18Style
            );

            // Simulation status label
            LayOutSimulationEnabled();

            // Environment prefab
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Simulation Environment", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
                (_environmentPrefabProperty, Contents.environmentPrefabLabel);

            // Depth
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
            (
                _useZBufferDepthInSimulationProperty,
                Contents.useZBufferDepthInSimulationLabel
            );

            // Persistent anchors
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Persistent Anchors", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
            (
                _useLightshipPersistentAnchorInSimulationProperty,
                Contents.useLightshipPersistentAnchorInSimulationLabel
            );

            EditorGUI.indentLevel++;
            if (!_useLightshipPersistentAnchorInSimulationProperty.boolValue)
            {
                // Persistent anchor sub-settings
                EditorGUIUtility.labelWidth = 285;
                EditorGUILayout.PropertyField
                    (_lightshipPersistentAnchorParamsProperty, GUILayout.ExpandWidth(true));
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);
        }

        private void LayOutSDKEnabled(string platform, bool enabled, BuildTargetGroup group = BuildTargetGroup.Unknown)
        {
            if (enabled)
            {
                EditorGUILayout.LabelField
                (
                    new GUIContent
                    (
                        $"{platform} : SDK Enabled",
                        _enabledIcon,
                        $"Niantic Lightship is selected as the plug-in provider for {platform} XR. " +
                        "The SDK is enabled for this platform."
                    ),
                    Contents.sdkEnabledStyle,
                    Contents.sdkEnabledOptions
                );
            }
            else
            {
                bool clicked = GUILayout.Button
                (
                    new GUIContent
                    (
                        $"{platform} : SDK Disabled",
                        _disabledIcon,
                        $"Niantic Lightship is not selected as the plug-in provider for {platform} XR." +
                        "The SDK will not be used. Click to open Project Validation for more info on changing" +
                        " plug-in providers to enable Lightship SDK."
                    ),
                    Contents.sdkDisabledStyle
                );
                if (clicked)
                {
                    // From OpenXRProjectValidationRulesSetup.cs,
                    // Delay opening the window since sometimes other settings in the player settings provider redirect to the
                    // project validation window causing serialized objects to be nullified
                    EditorApplication.delayCall += () =>
                    {
                        if (group is BuildTargetGroup.Standalone or BuildTargetGroup.Android or BuildTargetGroup.iOS)
                        {
                            EditorUserBuildSettings.selectedBuildTargetGroup = group;
                        }

                        SettingsService.OpenProjectSettings(ProjectValidationSettingsPath);
                    };
                }
            }
        }

        private void LayOutSimulationEnabled()
        {
            if (!IsLightshipSimulatorEnabled())
            {
                bool clicked = GUILayout.Button
                (
                    new GUIContent
                    (
                        $" Lightship Simulation Disabled",
                        _disabledIcon,
                        $"Niantic Lightship Simulation is not enabled.\n\nTo enable Lightship simulation, " +
                        "navigate to the XR Plug-in Management settings and select Niantic Lightship Simulation " +
                        "as the plug-in provider for Standalone XR."
                    ),
                    Contents.sdkDisabledStyle
                );
                if (clicked)
                {
                    EditorApplication.delayCall += () =>
                    {
                        SettingsService.OpenProjectSettings(XRPluginManagementPath);
                    };
                }
            }
            else
            {
                EditorGUILayout.LabelField
                (
                    new GUIContent
                    (
                        $" Lightship Simulation Enabled",
                        _enabledIcon,
                        $"Niantic Lightship Simulation is selected as the plug-in provider for Standalone XR."
                    ),
                    Contents.sdkEnabledStyle
                );
            }
        }

        private static bool IsLightshipSimulatorEnabled()
        {
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (null == generalSettings)
                return false;

            var managerSettings = generalSettings.AssignedSettings;
            if (null == managerSettings)
                return false;

            var simulationLoaderIsActive = managerSettings.activeLoaders.Any(loader => loader is LightshipSimulationLoader);
            return simulationLoaderIsActive;
        }
    }
}
