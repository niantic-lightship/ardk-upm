// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Semantics;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor.SemanticSegmentation
{
    [CustomEditor(typeof(ARSemanticSegmentationManager))]
    internal class ARSemanticSegmentationManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetFrameRate;

        private static class Tooltips
        {
            public const string HighFrameRateWarning = "A target framerate over 20 could negatively affect performance on older devices.";

            public static readonly GUIContent TargetFrameRate = new GUIContent
                ("Target Frame Rate", "Frame rate that semantic segmentation inference will aim to run at");

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_targetFrameRate, Tooltips.TargetFrameRate);
            if (_targetFrameRate.intValue > LightshipOcclusionSubsystem.MaxRecommendedFrameRate)
            {
                EditorGUILayout.HelpBox(Tooltips.HighFrameRateWarning, MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _targetFrameRate = serializedObject.FindProperty("_targetFrameRate");
        }
    }
}
