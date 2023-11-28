// Copyright 2022-2023 Niantic.

#if UNITY_EDITOR

using System;
using Niantic.Lightship.AR.Meshing;
using Niantic.Lightship.AR.Subsystems.Meshing;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.AR.Editor
{
    [CustomEditor(typeof(LightshipMeshingExtension))]
    internal class LightshipMeshingExtensionEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetFrameRate;

        private SerializedProperty _maximumIntegrationDistance;
        private SerializedProperty _voxelSize;
        private SerializedProperty _enableDistanceBasedVolumetricCleanup;

        private SerializedProperty _meshBlockSize;
        private SerializedProperty _meshCullingDistance;
        private SerializedProperty _enableMeshDecimation;

        static class Contents
        {
            public static readonly string meshBlockSizeWarning =
                "The Mesh Block Size will be automatically rounded to the closest multiple of the Voxel Size.";
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_targetFrameRate);

            EditorGUILayout.PropertyField(_maximumIntegrationDistance);
            EditorGUILayout.PropertyField(_voxelSize);
            EditorGUILayout.PropertyField(_enableDistanceBasedVolumetricCleanup);

            EditorGUILayout.PropertyField(_meshBlockSize);
            const float tolerance = 0.0001f;
            if (Math.Abs(_meshBlockSize.floatValue - Mathf.Round(_meshBlockSize.floatValue / _voxelSize.floatValue) * _voxelSize.floatValue) > tolerance)
            {
                EditorGUILayout.HelpBox(Contents.meshBlockSizeWarning, MessageType.Warning);
            }

            EditorGUILayout.PropertyField(_meshCullingDistance);
            EditorGUILayout.PropertyField(_enableMeshDecimation);

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _targetFrameRate = serializedObject.FindProperty("_targetFrameRate");
            _maximumIntegrationDistance = serializedObject.FindProperty("_maximumIntegrationDistance");
            _voxelSize = serializedObject.FindProperty("_voxelSize");
            _enableDistanceBasedVolumetricCleanup = serializedObject.FindProperty("_enableDistanceBasedVolumetricCleanup");
            _meshBlockSize = serializedObject.FindProperty("_meshBlockSize");
            _meshCullingDistance = serializedObject.FindProperty("_meshCullingDistance");
            _enableMeshDecimation = serializedObject.FindProperty("_enableMeshDecimation");
        }
    }
}
#endif // UNITY_EDITOR
