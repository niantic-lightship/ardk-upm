// Copyright 2022-2024 Niantic.

#if UNITY_EDITOR

using Niantic.Lightship.AR.Occlusion;
using Niantic.Lightship.AR.Semantics;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.AR.Editor
{
    [CustomEditor(typeof(LightshipOcclusionExtension))]
    internal class LightshipOcclusionExtensionEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetFrameRate;

        private SerializedProperty _bypassManagerUpdates;
        private SerializedProperty _optimalOcclusionDistanceMode;
        private SerializedProperty _principalOccludee;

        private SerializedProperty _isOcclusionSuppressionEnabled;
        private SerializedProperty _semanticSegmentationManager;
        private SerializedProperty _suppressionChannels;

        private SerializedProperty _isOcclusionStabilizationEnabled;
        private SerializedProperty _meshManager;

        private SerializedProperty _useCustomBackgroundMaterial;
        private SerializedProperty _customBackgroundMaterial;

        private SerializedProperty _smoothEdgePreferred;

        private static class Contents
        {
            public static readonly string highFrameRateWarning =
                "A target framerate over 20 could negatively affect performance on older devices.";

            public static readonly string noSemanticSegmentationManagerWarning =
                "There must be an ARSemanticSegmentationManager in the scene to enable semantic depth suppression.";

            public static readonly string noMeshManagerWarning =
                "There must be an ARMeshManager in the scene to enable depth stabilization.";

            public static readonly string noPrincipalOccludeeWarning =
                "Specify a GameObject with a renderer component in order to use " +
                "the SpecifiedGameObject optimal occlusion mode.";

            public static GUIContent modeLabel = new GUIContent("Mode");
            public static GUIContent enabledLabel = new GUIContent("Enabled");
        }

        // Fields get reset whenever the object hierarchy changes, in addition to when this Editor loses focus,
        private bool _triedLookingForSemanticsManager = false;
        private bool _triedLookingForMeshManager = false;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_targetFrameRate);
            if (_targetFrameRate.intValue > LightshipOcclusionSubsystem.MaxRecommendedFrameRate)
            {
                EditorGUILayout.HelpBox(Contents.highFrameRateWarning, MessageType.Warning);
            }

            var occlusionManager = Selection.activeGameObject.GetComponent<AROcclusionManager>();
            if (occlusionManager.requestedOcclusionPreferenceMode !=
                UnityEngine.XR.ARSubsystems.OcclusionPreferenceMode.NoOcclusion)
            {
                EditorGUILayout.LabelField("Bypass Occlusion Manager Updates", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_bypassManagerUpdates,Contents.enabledLabel);
                EditorGUILayout.LabelField("Optimal Occlusion", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_optimalOcclusionDistanceMode, Contents.modeLabel);
                if (_optimalOcclusionDistanceMode.enumValueIndex == (int)LightshipOcclusionExtension.OptimalOcclusionDistanceMode.SpecifiedGameObject)
                {
                    EditorGUILayout.PropertyField(_principalOccludee);

                    if (_principalOccludee.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(Contents.noPrincipalOccludeeWarning, MessageType.Error);
                    }
                }

                EditorGUILayout.LabelField("Occlusion Suppression", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_isOcclusionSuppressionEnabled, Contents.enabledLabel);

                var lightshipRenderPassEnabled = false;
                if (_isOcclusionSuppressionEnabled.boolValue)
                {
                    EditorGUILayout.PropertyField(_semanticSegmentationManager);

                    // If we haven't tried auto-filling the field yet, try
                    if (_semanticSegmentationManager.objectReferenceValue == null && !_triedLookingForSemanticsManager)
                    {
                        _triedLookingForSemanticsManager = true;
                        _semanticSegmentationManager.objectReferenceValue = FindObjectOfType<ARSemanticSegmentationManager>();
                    }

                    // Now that we've tried auto-filling, show the correct UI based on whether an
                    // ARSemanticSegmentationManager is referenced
                    if (_semanticSegmentationManager.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(Contents.noSemanticSegmentationManagerWarning, MessageType.Error);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(_suppressionChannels);
                        lightshipRenderPassEnabled = true;
                    }
                }

                EditorGUILayout.LabelField("Occlusion Stabilization", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_isOcclusionStabilizationEnabled, Contents.enabledLabel);

                if (_isOcclusionStabilizationEnabled.boolValue)
                {
                    EditorGUILayout.PropertyField(_meshManager);

                    if (_meshManager.objectReferenceValue == null && !_triedLookingForMeshManager)
                    {
                        _triedLookingForMeshManager = true;
                        _meshManager.objectReferenceValue = FindObjectOfType<ARMeshManager>();
                    }

                    if (_meshManager.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(Contents.noMeshManagerWarning, MessageType.Error);
                    }
                    else
                    {
                        lightshipRenderPassEnabled = true;
                    }
                }

                if (lightshipRenderPassEnabled)
                {
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                    EditorGUILayout.PropertyField(_useCustomBackgroundMaterial);
                    if (_useCustomBackgroundMaterial.boolValue)
                    {
                        ++EditorGUI.indentLevel;
                        EditorGUILayout.PropertyField(_customBackgroundMaterial);
                        --EditorGUI.indentLevel;
                    }
                    else
                    {
                        _customBackgroundMaterial.objectReferenceValue = null;
                    }

                    EditorGUILayout.PropertyField(_smoothEdgePreferred);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _targetFrameRate = serializedObject.FindProperty("_targetFrameRate");
            _bypassManagerUpdates = serializedObject.FindProperty("_bypassOcclusionManagerUpdates");
            _optimalOcclusionDistanceMode = serializedObject.FindProperty("_optimalOcclusionDistanceMode");
            _principalOccludee = serializedObject.FindProperty("_principalOccludee");
            _isOcclusionSuppressionEnabled = serializedObject.FindProperty("_isOcclusionSuppressionEnabled");
            _semanticSegmentationManager = serializedObject.FindProperty("_semanticSegmentationManager");
            _suppressionChannels = serializedObject.FindProperty("_suppressionChannels");
            _isOcclusionStabilizationEnabled = serializedObject.FindProperty("_isOcclusionStabilizationEnabled");
            _meshManager = serializedObject.FindProperty("_meshManager");
            _useCustomBackgroundMaterial = serializedObject.FindProperty("_useCustomBackgroundMaterial");
            _customBackgroundMaterial = serializedObject.FindProperty("_customBackgroundMaterial");
            _smoothEdgePreferred = serializedObject.FindProperty("_preferSmoothEdges");
        }
    }
}

#endif // UNITY_EDITOR
