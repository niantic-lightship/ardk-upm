// Copyright 2022-2025 Niantic.

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Scanning;

namespace Niantic.Lightship.AR.Editor
{
    [CustomEditor(typeof(ARScanningManager))]
    internal class ARScanningManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _scanPath;
        private SerializedProperty _fullResolutionEnabled;
        private SerializedProperty _scanTargetId;
        private SerializedProperty _scanRecordingFramerate;
        private SerializedProperty _enableRaycastVisualization;
        private SerializedProperty _enableVoxelVisualization;
        private SerializedProperty _useEstimatedDepth;

        // Fields get reset whenever the object hierarchy changes, in addition to when this Editor loses focus,
        private bool _triedLookingForOcclusionManager;
        private AROcclusionManager _occlusionManagerRef;

        private static class Contents
        {
            public const string ComponentEnabledWarning =
                "ARScanningManager is enabled, so it will begin recording immediately " +
                "with the current configuration settings. To control when recording begins or modify these settings" +
                "from a script, disable the component here and enable it at runtime.";
            public const string VisualizationDepthDisabledError =
                "Scanning visualization requires Lightship Depth to be enabled.";
            public const string VisualizationDepthMethodError =
                "Scanning visualization requires either lidar or estimated depth recording to be enabled.";
            public const string VisualizationLidarWarning =
                "Under the current configuration, scanning visualization will only function on devices " +
                "that support native depth such as lidar. To enable visualization for all devices, " +
                "enable Record Estimated Depth.";
            public const string MultipleVisualizationsWarning =
                "Displaying both visualization methods may lead to unexpected results.";
            public const string MissingOcclusionManagerError =
                "No AROcclusionManager was found in the scene. An enabled AROcclusionManager is required to record or " +
                "visualize with lidar data.";

            public static readonly GUIContent ScanPathLabel = new GUIContent("Scan Path");
            public static readonly GUIContent ScanTargetLabel = new GUIContent("POI Target ID");
            public static readonly GUIContent FullResolutionLabel = new GUIContent("Full Resolution Enabled");
            public static readonly GUIContent RecordingFramerateLabel = new GUIContent("Recording Framerate");
            public static readonly GUIContent UseEstimatedDepthLabel = new GUIContent("Record Estimated Depth");
            public static readonly GUIContent VoxelVisualizationLabel = new GUIContent("Enable Voxel Visualization");
            public static readonly GUIContent RaycastVisualizationLabel = new GUIContent("Enable Raycast Visualization");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var lidarEnabled = LightshipSettings.Instance.PreferLidarIfAvailable;
            var estimatedDepthRecordingEnabled = _useEstimatedDepth.boolValue;

            var scanningManager = (ARScanningManager)target;
            if (scanningManager.enabled && !Application.isPlaying)
            {
                EditorGUILayout.HelpBox(Contents.ComponentEnabledWarning, MessageType.Warning);
            }

            EditorGUILayout.LabelField("Location", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope(1))
            {
                EditorGUILayout.PropertyField(_scanPath, Contents.ScanPathLabel);
                EditorGUILayout.PropertyField(_scanTargetId, Contents.ScanTargetLabel);
            }

            EditorGUILayout.LabelField("Recording Settings", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope(1))
            {
                EditorGUILayout.PropertyField(_fullResolutionEnabled, Contents.FullResolutionLabel);
                EditorGUILayout.IntSlider(_scanRecordingFramerate, 0, 30, Contents.RecordingFramerateLabel);
                EditorGUILayout.PropertyField(_useEstimatedDepth, Contents.UseEstimatedDepthLabel);
            }

            EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope(1))
            {
                EditorGUILayout.PropertyField(_enableVoxelVisualization, Contents.VoxelVisualizationLabel);
                EditorGUILayout.PropertyField(_enableRaycastVisualization, Contents.RaycastVisualizationLabel);

                // Error if there is no depth source, warn if lidar is the only source
                if (_enableVoxelVisualization.boolValue || _enableRaycastVisualization.boolValue)
                {
                    var depthEnabled = LightshipSettings.Instance.UseLightshipDepth;

                    // Error if there is no depth source.  Warn if lidar is the only source.
                    if (!depthEnabled)
                    {
                        EditorGUILayout.HelpBox(Contents.VisualizationDepthDisabledError, MessageType.Error);
                    }
                    else if (!lidarEnabled && !estimatedDepthRecordingEnabled)
                    {
                        EditorGUILayout.HelpBox(Contents.VisualizationDepthMethodError, MessageType.Error);
                    }
                    else if (lidarEnabled && !estimatedDepthRecordingEnabled)
                    {
                        EditorGUILayout.HelpBox(Contents.VisualizationLidarWarning, MessageType.Warning);
                    }

                    // Discourage from enabling both visualizations
                    if (_enableVoxelVisualization.boolValue && _enableRaycastVisualization.boolValue)
                    {
                        EditorGUILayout.HelpBox(Contents.MultipleVisualizationsWarning, MessageType.Warning);
                    }
                }
            }

            // Warn if Prefer Lidar is enabled but the required Occlusion Manager is missing
            if (lidarEnabled && !estimatedDepthRecordingEnabled)
            {
                if (!_triedLookingForOcclusionManager)
                {
                    _triedLookingForOcclusionManager = true;
                    _occlusionManagerRef = FindObjectOfType<AROcclusionManager>();
                }

                if (_occlusionManagerRef == null || !_occlusionManagerRef.enabled)
                {
                    EditorGUILayout.HelpBox(Contents.MissingOcclusionManagerError, MessageType.Warning);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _scanPath = serializedObject.FindProperty("_scanPath");
            _scanTargetId = serializedObject.FindProperty("_scanTargetId");
            _fullResolutionEnabled = serializedObject.FindProperty("_fullResolutionEnabled");
            _scanRecordingFramerate = serializedObject.FindProperty("_scanRecordingFramerate");
            _enableRaycastVisualization = serializedObject.FindProperty("_enableRaycastVisualization");
            _enableVoxelVisualization = serializedObject.FindProperty("_enableVoxelVisualization");
            _useEstimatedDepth = serializedObject.FindProperty("_useEstimatedDepth");
        }
    }
}

#endif // UNITY_EDITOR
