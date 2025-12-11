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
        private SerializedProperty _fullResolutionFramerate;
        private SerializedProperty _scanTargetId;
        private SerializedProperty _scanRecordingFramerate;
        private SerializedProperty _enableRaycastVisualization;
        private SerializedProperty _enableVoxelVisualization;
        private SerializedProperty _useEstimatedDepth;
        private SerializedProperty _minimumVoxelSize;
        private SerializedProperty _nearDepth;
        private SerializedProperty _farDepth;

        // Fields get reset whenever the object hierarchy changes, in addition to when this Editor loses focus
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
            public const string DepthLimitedFpsWarning =
                "Recording depth when lidar is not available will limit the recording framerate to the update " +
                "rate of the NSDK depth feature, regardless of the Recording Framerate setting.\n\nTo increase the update " +
                "rate of the depth feature from its default of 10 FPS, add a Lightship Occlusion Extension component " +
                "and set the Target Frame Rate";

            public static readonly GUIContent ScanPathLabel = new GUIContent("Scan Path");
            public static readonly GUIContent ScanTargetLabel = new GUIContent("POI Target ID");
            public static readonly GUIContent FullResolutionLabel = new GUIContent("Full Resolution Enabled");
            public static readonly GUIContent RecordingFramerateLabel = new GUIContent("Recording Framerate", "The base framerate for recording small resolution frames and depth data.");
            public static readonly GUIContent FullResolutionFramerateLabel = new GUIContent("Full Resolution Framerate", "The framerate for recording full resolution frames. This can be lower than Recording Framerate to save on file size or compute.");
            public static readonly GUIContent UseEstimatedDepthLabel = new GUIContent("Record Estimated Depth");
            public static readonly GUIContent VoxelVisualizationLabel = new GUIContent("Enable Voxel Visualization");
            public static readonly GUIContent RaycastVisualizationLabel = new GUIContent("Enable Raycast Visualization");
            public static readonly GUIContent VoxelSizeLabel = new GUIContent("Min Voxel Size (m)", "The minimum size of voxels for voxel visualization, in meters. Smaller values result in higher resolution but require more memory and computation. The size will increase as more voxels are allocated.");
            public static readonly GUIContent NearDepthLabel = new GUIContent("Near Depth (m)", "Closest distance for depth integration, in meters. Objects closer than this distance will not be visualized or reconstructed.");
            public static readonly GUIContent FarDepthLabel = new GUIContent("Far Depth (m)", "Farthest distance for depth integration, in meters. Objects farther than this distance will not be visualized or reconstructed.");
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
                EditorGUILayout.IntSlider(_scanRecordingFramerate, 0, 30, Contents.RecordingFramerateLabel);
                EditorGUILayout.PropertyField(_fullResolutionEnabled, Contents.FullResolutionLabel);
                if (_fullResolutionEnabled.boolValue)
                {
                    using (new EditorGUI.IndentLevelScope(1))
                    {
                        EditorGUILayout.IntSlider(_fullResolutionFramerate, 0, 30, Contents.FullResolutionFramerateLabel);
                    }
                }
                EditorGUILayout.PropertyField(_useEstimatedDepth, Contents.UseEstimatedDepthLabel);
            }

            EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope(1))
            {
                EditorGUILayout.PropertyField(_enableVoxelVisualization, Contents.VoxelVisualizationLabel);

                // Voxel size control
                if (_enableVoxelVisualization.boolValue)
                {
                    using (new EditorGUI.IndentLevelScope(1))
                    {
                        EditorGUILayout.Slider(_minimumVoxelSize, 0.005f, 0.1f, Contents.VoxelSizeLabel);
                    }
                }

                EditorGUILayout.PropertyField(_enableRaycastVisualization, Contents.RaycastVisualizationLabel);

                if (_enableVoxelVisualization.boolValue || _enableRaycastVisualization.boolValue)
                {
                    // Depth range controls
                    EditorGUILayout.Slider(_nearDepth, 0.0f, 5.0f, Contents.NearDepthLabel);
                    EditorGUILayout.Slider(_farDepth, 0.5f, 10.0f, Contents.FarDepthLabel);

                    if (_nearDepth.floatValue >= _farDepth.floatValue)
                    {
                        EditorGUILayout.HelpBox("Near Depth must be less than Far Depth.", MessageType.Error);
                    }

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

            // Warn about capped FPS if multidepth recording is enabled (this will apply for non-lidar devices)
            // 10 FPS is the default multidepth update rate
            if (estimatedDepthRecordingEnabled && _scanRecordingFramerate.intValue > 10)
            {
                EditorGUILayout.HelpBox(Contents.DepthLimitedFpsWarning, MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _scanPath = serializedObject.FindProperty("_scanPath");
            _scanTargetId = serializedObject.FindProperty("_scanTargetId");
            _fullResolutionEnabled = serializedObject.FindProperty("_fullResolutionEnabled");
            _fullResolutionFramerate = serializedObject.FindProperty("_fullResolutionFramerate");
            _scanRecordingFramerate = serializedObject.FindProperty("_scanRecordingFramerate");
            _enableRaycastVisualization = serializedObject.FindProperty("_enableRaycastVisualization");
            _enableVoxelVisualization = serializedObject.FindProperty("_enableVoxelVisualization");
            _useEstimatedDepth = serializedObject.FindProperty("_useEstimatedDepth");
            _minimumVoxelSize = serializedObject.FindProperty("_minimumVoxelSize");
            _nearDepth = serializedObject.FindProperty("_nearDepth");
            _farDepth = serializedObject.FindProperty("_farDepth");
        }
    }
}

#endif // UNITY_EDITOR
