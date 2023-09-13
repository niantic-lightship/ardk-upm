// Copyright 2023 Niantic, Inc. All Rights Reserved.

#if UNITY_EDITOR

using Niantic.Lightship.AR.ARFoundation;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    [CustomEditor(typeof(LightshipOcclusionExtension))]
    internal class LightshipOcclusionExtensionEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetFrameRate;

        private SerializedProperty _optimalOcclusionDistanceMode;
        private SerializedProperty _principalOccludee;

        private SerializedProperty _isSemanticDepthSuppressionEnabled;
        private SerializedProperty _semanticSegmentationManager;
        private SerializedProperty _suppressionChannels;

        private SerializedProperty _useCustomBackgroundMaterial;
        private SerializedProperty _customBackgroundMaterial;

        static class Tooltips
        {
            public static readonly GUIContent highFrameRateWarning =
                new GUIContent
                (
                    "A target framerate over 20 could negatively affect performance on older devices."
                );

            public static readonly GUIContent targetFrameRate = new GUIContent
                ("Target Frame Rate", "Frame rate that depth inference will aim to run at");

            public static readonly GUIContent optimalOcclusionDistanceMode = new GUIContent(
                "Optimal Occlusion Distance Mode",
                "The method used when determining the distance at which occlusions will have the best visual quality. ClosestOccluder" +
                "will average depths from the whole FoV, whereas SpecifiedGameObject will use the depth to a particular object.");

            public static readonly GUIContent principalOccludee = new GUIContent(
                "Principal Occludee",
                "The principal AR Object being occluded in SpecifiedGameObject mode. Occlusions will look most effective for this objects, so it" +
                "should usually be the focal object for the experience.");

            public static readonly GUIContent isSemanticDepthSuppressionEnabled = new GUIContent(
                "Enable Semantic Depth Suppression",
                "If this is enabled, the occlusion extension will filter the occlusion buffer so that it does " +
                "not occlude for particular semantic channels. This can help to reduce visual artefacts such as characters " +
                "falling through the floor or disappearing into the sky.");

            public static readonly GUIContent semanticSegmentationManager = new GUIContent(
                "Semantic Segmentation Manager",
                "Enabling Semantic Depth Suppression requires a semantic segmentation manager to provide access to the semantics data.");

            public static readonly GUIContent suppressionChannels = new GUIContent(
                "Suppression Channels",
                "The list of semantic channels to be suppressed in the depth buffer. Pixels classified as any of these " +
                "channels will not occlude AR Characters.");

            public static readonly GUIContent useCustomMaterial = new GUIContent(
                "Use Custom Material",
                "When false, a material is generated automatically from the shader included in the platform-specific package. When true, the Custom Material is used instead, overriding the automatically generated one. This is not necessary for most AR experiences.");

            public static readonly GUIContent customMaterial = new GUIContent(
                "Custom Material",
                "The material to use for background rendering.");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_targetFrameRate, Tooltips.targetFrameRate);
            if (_targetFrameRate.intValue > LightshipOcclusionSubsystem.MaxRecommendedFrameRate)
            {
                EditorGUILayout.HelpBox(Tooltips.highFrameRateWarning);
            }

            EditorGUILayout.PropertyField(_optimalOcclusionDistanceMode, Tooltips.optimalOcclusionDistanceMode);
            if (_optimalOcclusionDistanceMode.enumValueIndex == (int)LightshipOcclusionExtension.OptimalOcclusionDistanceMode.SpecifiedGameObject)
            {
                EditorGUILayout.PropertyField(_principalOccludee, Tooltips.principalOccludee);
            }

            EditorGUILayout.PropertyField(_isSemanticDepthSuppressionEnabled,
                Tooltips.isSemanticDepthSuppressionEnabled);

            if (_isSemanticDepthSuppressionEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(_semanticSegmentationManager, Tooltips.semanticSegmentationManager);
                EditorGUILayout.PropertyField(_suppressionChannels, Tooltips.suppressionChannels);

                EditorGUILayout.PropertyField(_useCustomBackgroundMaterial, Tooltips.useCustomMaterial);

                if (_useCustomBackgroundMaterial.boolValue)
                {
                    ++EditorGUI.indentLevel;
                    EditorGUILayout.PropertyField(_customBackgroundMaterial, Tooltips.customMaterial);
                    --EditorGUI.indentLevel;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _targetFrameRate = serializedObject.FindProperty("_targetFrameRate");
            _optimalOcclusionDistanceMode = serializedObject.FindProperty("_optimalOcclusionDistanceMode");
            _principalOccludee = serializedObject.FindProperty("_principalOccludee");
            _isSemanticDepthSuppressionEnabled = serializedObject.FindProperty("_isSemanticDepthSuppressionEnabled");
            _semanticSegmentationManager = serializedObject.FindProperty("_semanticSegmentationManager");
            _suppressionChannels = serializedObject.FindProperty("_suppressionChannels");
            _useCustomBackgroundMaterial = serializedObject.FindProperty("_useCustomBackgroundMaterial");
            _customBackgroundMaterial = serializedObject.FindProperty("_customBackgroundMaterial");
        }
    }
}

#endif // UNITY_EDITOR
