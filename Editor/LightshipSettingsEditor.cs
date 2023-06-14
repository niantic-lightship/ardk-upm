using Niantic.Lightship.AR.Loader;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    /// <summary>
    /// This Editor renders to the XR Plug-in Management category of the Project Settings window.
    /// </summary>
    [CustomEditor(typeof(LightshipSettings))]
    class LightshipSettingsEditor : UnityEditor.Editor
    {
        private string[] _platforms = {"Editor", "Device"};
        private int _platformSelected = 0;

        private SerializedObject _serializedObject;
        private SerializedProperty _apiKeyProperty;
        private SerializedProperty _useLightshipDepthProperty;
        private SerializedProperty _useLightshipMeshingProperty;
        private SerializedProperty _lightshipDepthFrameRateProperty;
        private SerializedProperty _preferLidarIfAvailableProperty;
        private SerializedProperty _useLightshipPersistentAnchorProperty;
        private SerializedProperty _useLightshipSemanticSegmentationProperty;
        private SerializedProperty _useLightshipScanningProperty;
        private SerializedProperty _usePlaybackOnEditorProperty;
        private SerializedProperty _usePlaybackOnDeviceProperty;
        private SerializedProperty _playbackDatasetNameEditorProperty;
        private SerializedProperty _playbackDatasetNameDeviceProperty;
        private SerializedProperty _runPlaybackManuallyEditorProperty;
        private SerializedProperty _runPlaybackManuallyDeviceProperty;

        void OnEnable()
        {
            _serializedObject = new SerializedObject(LightshipSettings.Instance);
            _apiKeyProperty = _serializedObject.FindProperty("_apiKey");
            _useLightshipDepthProperty = _serializedObject.FindProperty("_useLightshipDepth");
            _useLightshipMeshingProperty = _serializedObject.FindProperty("_useLightshipMeshing");
            _lightshipDepthFrameRateProperty = _serializedObject.FindProperty("_lightshipDepthFrameRate");
            _preferLidarIfAvailableProperty = _serializedObject.FindProperty("_preferLidarIfAvailable");
            _useLightshipPersistentAnchorProperty = _serializedObject.FindProperty("_useLightshipPersistentAnchor");
            _useLightshipSemanticSegmentationProperty = _serializedObject.FindProperty("_useLightshipSemanticSegmentation");
            _useLightshipScanningProperty = _serializedObject.FindProperty("_useLightshipScanning");
            _usePlaybackOnEditorProperty = _serializedObject.FindProperty("_usePlaybackOnEditor");
            _usePlaybackOnDeviceProperty = _serializedObject.FindProperty("_usePlaybackOnDevice");
            _playbackDatasetNameEditorProperty = _serializedObject.FindProperty("_playbackDatasetPathEditor");
            _playbackDatasetNameDeviceProperty = _serializedObject.FindProperty("_playbackDatasetPathDevice");
            _runPlaybackManuallyEditorProperty = _serializedObject.FindProperty("_runPlaybackManuallyEditor");
            _runPlaybackManuallyDeviceProperty = _serializedObject.FindProperty("_runPlaybackManuallyDevice");
        }

        public override void OnInspectorGUI()
        {
            _serializedObject.Update();
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                // Disable changes during runtime
                EditorGUI.BeginDisabledGroup(Application.isPlaying);

                EditorGUIUtility.labelWidth = 175;

                EditorGUILayout.LabelField("Credentials", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_apiKeyProperty, new GUIContent("Api Key"));

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipDepthProperty, new GUIContent("Enabled"));
                EditorGUI.indentLevel++;
                // Put Depth sub-settings here
                if (_useLightshipDepthProperty.boolValue)
                {
                    EditorGUILayout.IntSlider(_lightshipDepthFrameRateProperty, 1, 90, new GUIContent("Framerate"));
                    // Temporarily disable the ability to prefer using LiDAR if available until meshing and gameboard
                    // features work with arf platform depth
                    /*
                    EditorGUILayout.PropertyField(_preferLidarIfAvailableProperty,
                        new GUIContent("Prefer LiDAR if Available"));
                    */
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Meshing", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipMeshingProperty, new GUIContent("Enabled"));
                EditorGUI.indentLevel++;
                // Put Meshing sub-settings here
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Persistent Anchors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipPersistentAnchorProperty, new GUIContent("Enabled"));
                EditorGUI.indentLevel++;
                // Put Persistent Anchors sub-settings here
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Semantic Segmentation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipSemanticSegmentationProperty, new GUIContent("Enabled"));
                EditorGUI.indentLevel++;
                // Put Semantic Segmentation sub-settings here
                EditorGUI.indentLevel--;

                // Temporarily disable the ability to enable scanning subsystem until it is ready
                /*
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Scanning", EditorStyles.boldLabel);
                // TODO(sxian): Enable scanning by default after the implementation is landed.
                EditorGUILayout.PropertyField(_useLightshipScanningProperty, new GUIContent("Enabled"));
                EditorGUI.indentLevel++;
                // Put Scanning sub-settings here
                EditorGUI.indentLevel--;
                */

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                _platformSelected = GUILayout.Toolbar(_platformSelected, _platforms);
                GUILayout.EndHorizontal();

                DrawEditorPlaybackGui();

                EditorGUI.EndDisabledGroup();

                if (change.changed)
                {
                    _serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private void DrawEditorPlaybackGui()
        {
            if (_platformSelected == 0)
            {
                EditorGUILayout.PropertyField(_usePlaybackOnEditorProperty, new GUIContent("Enabled"));
                if (_usePlaybackOnEditorProperty.boolValue)
                {
                    EditorGUI.indentLevel++;
                    DrawDatasetPathGUI();
                    EditorGUILayout.PropertyField(_runPlaybackManuallyEditorProperty, new GUIContent("Run Manually"));
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.PropertyField(_usePlaybackOnDeviceProperty, new GUIContent("Enabled"));
                if (_usePlaybackOnDeviceProperty.boolValue)
                {
                    EditorGUI.indentLevel++;
                    DrawDatasetPathGUI();
                    EditorGUILayout.PropertyField(_runPlaybackManuallyDeviceProperty, new GUIContent("Run Manually"));
                    EditorGUI.indentLevel--;
                }
            }
        }

        private const char FindIcon = '\u25c9';

        private void DrawDatasetPathGUI()
        {
            EditorGUILayout.BeginHorizontal();

            if (_platformSelected == 0)
                EditorGUILayout.PropertyField(_playbackDatasetNameEditorProperty, new GUIContent("Dataset Path"));

            else
                EditorGUILayout.PropertyField(_playbackDatasetNameDeviceProperty, new GUIContent("Dataset Path"));

            if (GUILayout.Button(FindIcon.ToString(), GUILayout.Width(50)))
            {
                if (_platformSelected == 0)
                {
                    var path = EditorUtility.OpenFolderPanel("Select Dataset Directory",
                        _playbackDatasetNameEditorProperty.stringValue, "");
                    if (path.Length > 0)
                    {
                        _playbackDatasetNameEditorProperty.stringValue = path;
                    }
                }
                else
                {
                    var path = EditorUtility.OpenFolderPanel("Select Dataset Directory",
                        _playbackDatasetNameDeviceProperty.stringValue, "");
                    if (path.Length > 0)
                    {
                        _playbackDatasetNameDeviceProperty.stringValue = path;
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
