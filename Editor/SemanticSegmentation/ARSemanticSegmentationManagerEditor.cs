// Copyright 2023 Niantic, Inc. All Rights Reserved.

using Niantic.Lightship.AR.ARFoundation;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor.SemanticSegmentation
{
    [CustomEditor(typeof(ARSemanticSegmentationManager))]
    internal class ARSemanticSegmentationManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetFrameRate;

        static class Tooltips
        {
            public static readonly GUIContent highFrameRateWarning =
                new GUIContent
                (
                    "A target framerate over 20 could negatively affect performance on older devices."
                );

            public static readonly GUIContent targetFrameRate = new GUIContent
                ("Target Frame Rate", "Frame rate that semantic segmentation inference will aim to run at");

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_targetFrameRate, Tooltips.targetFrameRate);
            if (_targetFrameRate.intValue > LightshipOcclusionSubsystem.MaxRecommendedFrameRate)
            {
                EditorGUILayout.HelpBox(Tooltips.highFrameRateWarning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _targetFrameRate = serializedObject.FindProperty("_targetFrameRate");
        }
    }
}
