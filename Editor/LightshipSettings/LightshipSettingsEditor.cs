// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities.Logging;
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
        internal const string XREnvironmentViewPath = "Window/XR/AR Foundation/XR Environment";

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
            public static readonly GUIContent preferLidarLabel = new GUIContent("Prefer LiDAR if Available");
            public static readonly GUIContent environmentViewLabel = new GUIContent("Environment Prefab");
            public static readonly GUIContent environmentViewButton =
                new GUIContent
                (
                    "Open XR Environment Window",
                    "To set an environment prefab, open the scene view and use the XR Environment overlay."
                );

            private static readonly GUIContent helpIcon = EditorGUIUtility.IconContent("_Help");

            public static readonly GUIContent playbackLabel =
                new GUIContent
                    (
                        "",
                        helpIcon.image,
                        "Enable playback to use recorded camera and sensor data to drive your app's AR session." +
                        "Click for documentation."
                    );

            public static readonly GUIContent simulationLabel =
                new GUIContent
                (
                    "",
                    helpIcon.image,
                    "Enable the Niantic Lightship Simulation loader for the Standalone platform in " +
                    "the XR Plug-in Management menu to use Lightship with ARFoundation's simulation mode." +
                    "Click for documentation"
                );

            public static readonly GUIContent useZBufferDepthInSimulationLabel = new GUIContent("Use Z-Buffer Depth");
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
        private SerializedProperty _useLightshipWorldPositioningProperty;
        private SerializedProperty _locationAndCompassDataSourceProperty;
        private SerializedProperty _spoofLocationInfoProperty;
        private SerializedProperty _spoofCompassInfoProperty;
        private SerializedProperty _unityLogLevelProperty;
        private SerializedProperty _fileLogLevelProperty;
        private SerializedProperty _stdOutLogLevelProperty;
        private SerializedProperty _useZBufferDepthInSimulationProperty;
        private SerializedProperty _useSimulationPersistentAnchorInSimulationProperty;
        private SerializedProperty _lightshipPersistentAnchorParamsProperty;
        private IPlaybackSettingsEditor[] _playbackSettingsEditors;
        private Texture _enabledIcon;
        private Texture _disabledIcon;

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
            _useLightshipWorldPositioningProperty = _lightshipSettings.FindProperty("_useLightshipWorldPositioning");

            _locationAndCompassDataSourceProperty = _lightshipSettings.FindProperty("_locationAndCompassDataSource");
            _spoofLocationInfoProperty = _lightshipSettings.FindProperty("_spoofLocationInfo");
            _spoofCompassInfoProperty = _lightshipSettings.FindProperty("_spoofCompassInfo");

            _unityLogLevelProperty = _lightshipSettings.FindProperty("_unityLogLevel");
            _fileLogLevelProperty = _lightshipSettings.FindProperty("_fileLogLevel");
            _stdOutLogLevelProperty = _lightshipSettings.FindProperty("_stdoutLogLevel");

            // Simulation sub-properties
            _useZBufferDepthInSimulationProperty = _lightshipSettings.FindProperty("_lightshipSimulationParams._useZBufferDepth");
            _useSimulationPersistentAnchorInSimulationProperty = _lightshipSettings.FindProperty("_lightshipSimulationParams._useSimulationPersistentAnchor");
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
                // Disable changes to the asset during runtime
                EditorGUI.BeginDisabledGroup(Application.isPlaying);

                // -- Put new Lightship settings here --
                DrawLightshipSettings();

                EditorGUILayout.Space(20);
                DrawPlaybackSettings();

                EditorGUILayout.Space(20);
                // -- Put new simulation settings here --
                DrawLightshipSimulationSettings();

                // -- Put experimental settings here, when there are any --
#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
                // DrawExperimentalSettings();
#endif

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

            // Depth settings

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

            // Semantic Segmentation settings
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Semantic Segmentation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
                (_useLightshipSemanticSegmentationProperty, Contents.enabledLabel);


            // Meshing settings
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Meshing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useLightshipMeshingProperty, Contents.enabledLabel);
            EditorGUI.indentLevel++;
            // Put Meshing sub-settings here
            EditorGUI.indentLevel--;

            // Persistent Anchors settings
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Persistent Anchors", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
                (_useLightshipPersistentAnchorProperty, Contents.enabledLabel);

            EditorGUI.indentLevel++;
            // Put Persistent Anchors sub-settings here
            EditorGUI.indentLevel--;

            // Scanning settings
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Scanning", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useLightshipScanningProperty, Contents.enabledLabel);
            EditorGUI.indentLevel++;
            // Put Scanning sub-settings here
            EditorGUI.indentLevel--;

            // Object Detection settings
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Object Detection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
                (_useLightshipObjectDetectionProperty, Contents.enabledLabel);

            EditorGUI.indentLevel++;
            // Put Object Detection sub-settings here
            EditorGUI.indentLevel--;

            // World Positioning settings
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("World Positioning System", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useLightshipWorldPositioningProperty, Contents.enabledLabel);

            EditorGUI.indentLevel++;
            // Put World Positioning sub-settings here
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);
            DrawLocationSourceSettings();

            EditorGUILayout.Space(10);
            DrawLoggingSettings();
        }

        private void DrawLoggingSettings()
        {
            EditorGUILayout.LabelField("Logging", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
            (
                _unityLogLevelProperty,
                new GUIContent("Unity Log Level", tooltip: "Log level for Unity's built-in logging system")
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
                    "Recommended to be set to 'off' unless its a niantic support case. File Location: {Project-Root}/data/log.txt"
                )
            );
        }

        private void DrawPlaybackSettings()
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Playback", Contents.boldFont18Style, GUILayout.Width(80));
            if (EditorGUILayout.LinkButton(Contents.playbackLabel))
            {
                Application.OpenURL("https://lightship.dev/docs/ardk/how-to/unity/setting_up_playback/");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var currPlatform = GUILayout.Toolbar(_platformSelected, Contents._platforms);
            GUILayout.EndHorizontal();

            // If the playback dataset text field is focused when the Playback platform is changed,
            // the text field content will not switch to the new platform's dataset path, causing confusion.
            // To prevent this, we clear the focus when the platform is changed.
            if (currPlatform != _platformSelected)
            {
                GUI.FocusControl(null);
                _platformSelected = currPlatform;
            }

            _playbackSettingsEditors[_platformSelected].DrawGUI();
        }

        private void DrawLightshipSimulationSettings()
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Simulation", Contents.boldFont18Style, GUILayout.Width(95));
            if (EditorGUILayout.LinkButton(Contents.simulationLabel))
            {
                Application.OpenURL("https://lightship.dev/docs/ardk/how-to/unity/simulation_mocking/");
            }
            GUILayout.EndHorizontal();

            // Simulation status label
            LayOutSimulationEnabled();

            EditorGUI.BeginDisabledGroup(!IsLightshipSimulatorEnabled());

            // Environment prefab
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Simulation Environment", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Contents.environmentViewLabel, GUILayout.Width(EditorGUIUtility.labelWidth));
            bool clicked = GUILayout.Button(Contents.environmentViewButton, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            if (clicked)
            {
                EditorApplication.delayCall += () =>
                {
                    EditorApplication.ExecuteMenuItem(XREnvironmentViewPath);
                };
            }

            // Depth
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField
            (
                _useZBufferDepthInSimulationProperty,
                Contents.useZBufferDepthInSimulationLabel
            );

            // Persistent anchors (currently forcing the use of the simulation mock system)
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Persistent Anchors", EditorStyles.boldLabel);
            // EditorGUILayout.PropertyField
            // (
            //     _useSimulationPersistentAnchorInSimulationProperty,
            //     Contents.useLightshipPersistentAnchorInSimulationLabel
            // );
            //
            // EditorGUI.indentLevel++;
            if (_useSimulationPersistentAnchorInSimulationProperty.boolValue) // always true
            {
                // Persistent anchor sub-settings
                EditorGUIUtility.labelWidth = 285;
                EditorGUILayout.PropertyField
                    (_lightshipPersistentAnchorParamsProperty, GUILayout.ExpandWidth(true));
            }
            // EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawLocationSourceSettings()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Location & Compass", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_locationAndCompassDataSourceProperty, new GUIContent("Data Source"));

            EditorGUILayout.PropertyField(_spoofLocationInfoProperty);
            EditorGUILayout.PropertyField(_spoofCompassInfoProperty);
        }

        private void DrawExperimentalSettings()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField
            (
                "Experimental",
                Contents.boldFont18Style
            );
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
