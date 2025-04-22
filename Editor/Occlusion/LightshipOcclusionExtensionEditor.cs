// Copyright 2022-2025 Niantic.

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
        private SerializedProperty _occlusionTechnique;

        private SerializedProperty _bypassManagerUpdates;
        private SerializedProperty _overrideOcclusionManagerSettings;
        private SerializedProperty _optimalOcclusionDistanceMode;
        private SerializedProperty _principalOccludee;

        private SerializedProperty _isOcclusionSuppressionEnabled;
        private SerializedProperty _suppressionChannels;

        private SerializedProperty _isOcclusionStabilizationEnabled;
        private SerializedProperty _meshManager;

        private SerializedProperty _useCustomMaterial;
        private SerializedProperty _customMaterial;

        private SerializedProperty _smoothEdgePreferred;

        private static class Contents
        {
            public const string HighFrameRateWarning =
                "A target framerate over 20 could negatively affect performance on older devices.";

            public const string NoSemanticSegmentationManagerWarning =
                "Create an ARSemanticSegmentationManager to enable semantic depth suppression.";

            public const string NoMeshManagerWarning =
                "Place an ARMeshManager on a child object to enable depth stabilization.";

            public const string ObjectScaleWarning = "The local scale of this object is not (1, 1, 1). " +
                "This may cause unintended effects for rendering objects.";

            public const string NoPrincipalOccludeeWarning =
                "Specify a GameObject with a renderer component in order to use " +
                "the SpecifiedGameObject optimal occlusion mode.";

            public static readonly GUIContent TechniqueLabel = new GUIContent("Technique");
            public static readonly GUIContent ModeLabel = new GUIContent("Mode");
            public static readonly GUIContent OverrideSettingsLabel = new GUIContent("Override Settings");
            public static readonly GUIContent BypassUpdatesLabel = new GUIContent("Bypass Updates");
            public static readonly GUIContent EnabledLabel = new GUIContent("Enabled");

            public const string FusedMeshPrefabPath =
                "Packages/com.nianticlabs.lightship/Assets/Prefabs/FusedMesh.prefab";
        }

        // Fields get reset whenever the object hierarchy changes, in addition to when this Editor loses focus,
        private bool _triedLookingForMeshManager;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_targetFrameRate);
            if (_targetFrameRate.intValue > LightshipOcclusionSubsystem.MaxRecommendedFrameRate)
            {
                EditorGUILayout.HelpBox(Contents.HighFrameRateWarning, MessageType.Warning);
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Preferred Technique", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_occlusionTechnique, Contents.TechniqueLabel);

                EditorGUILayout.LabelField("Occlusion Manager Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_bypassManagerUpdates, Contents.BypassUpdatesLabel);
                EditorGUILayout.PropertyField(_overrideOcclusionManagerSettings, Contents.OverrideSettingsLabel);

                EditorGUILayout.LabelField("Optimal Occlusion Mode", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_optimalOcclusionDistanceMode, Contents.ModeLabel);

                if (_optimalOcclusionDistanceMode.enumValueIndex ==
                    (int)LightshipOcclusionExtension.OptimalOcclusionDistanceMode.SpecifiedGameObject)
                {
                    EditorGUILayout.PropertyField(_principalOccludee);

                    if (_principalOccludee.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(Contents.NoPrincipalOccludeeWarning, MessageType.Error);
                    }
                }
            }

            EditorGUILayout.LabelField("Occlusion Suppression", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_isOcclusionSuppressionEnabled, Contents.EnabledLabel);

            var lightshipRenderPassEnabled = false;
            if (_isOcclusionSuppressionEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(_suppressionChannels);
                lightshipRenderPassEnabled = true;
            }

            EditorGUILayout.LabelField("Occlusion Stabilization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_isOcclusionStabilizationEnabled, Contents.EnabledLabel);

            if (_isOcclusionStabilizationEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(_meshManager);
                var localTransform = Selection.activeGameObject.GetComponent<Transform>();
                bool isLocalTransformIdentity = localTransform.localScale == Vector3.one;

                if (_meshManager.objectReferenceValue == null && !_triedLookingForMeshManager)
                {
                    _triedLookingForMeshManager = true;
                    _meshManager.objectReferenceValue = FindObjectOfType<ARMeshManager>();
                }

                if (!isLocalTransformIdentity)
                {
                    EditorGUILayout.HelpBox(Contents.ObjectScaleWarning, MessageType.Warning);

                    if (GUILayout.Button("Reset local scale"))
                    {
                        // Correct the scale on the current object
                        localTransform.localScale = Vector3.one;
                    }
                }

                if (_meshManager.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(Contents.NoMeshManagerWarning, MessageType.Error);
                    if (GUILayout.Button("Create an ARMeshManager"))
                    {
                        // Create an ARMeshManager on a child object to avoid re-scaling the current object
                        var newObj = new GameObject("ARMeshManager");
                        newObj.transform.SetParent(Selection.activeGameObject.transform);
                        var meshManager = newObj.AddComponent<ARMeshManager>();
                        // The Lightship fused mesh prefab is required in order to properly occlude
                        meshManager.meshPrefab =
                            AssetDatabase.LoadAssetAtPath<MeshFilter>(Contents.FusedMeshPrefabPath);

                        _meshManager.objectReferenceValue = meshManager;
                        lightshipRenderPassEnabled = true;
                    }
                }
                else
                {
                    lightshipRenderPassEnabled = true;
                }
            }

            EditorGUILayout.LabelField("Prefer Smooth Edges", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_smoothEdgePreferred, Contents.EnabledLabel);
            if (_smoothEdgePreferred.boolValue)
            {
                lightshipRenderPassEnabled = true;
            }

            if (lightshipRenderPassEnabled)
            {
                if (_occlusionTechnique.enumValueIndex !=
                    (int)LightshipOcclusionExtension.OcclusionTechnique.Automatic)
                {
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                    EditorGUILayout.PropertyField(_customMaterial);
                }
                else
                {
                    _customMaterial.objectReferenceValue = null;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            _targetFrameRate = serializedObject.FindProperty("_targetFrameRate");
            _occlusionTechnique = serializedObject.FindProperty("_occlusionTechnique");
            _bypassManagerUpdates = serializedObject.FindProperty("_requestBypassOcclusionManagerUpdates");
            _overrideOcclusionManagerSettings = serializedObject.FindProperty("_overrideOcclusionManagerSettings");

            _principalOccludee = serializedObject.FindProperty("_principalOccludee");
            _optimalOcclusionDistanceMode = serializedObject.FindProperty("_optimalOcclusionDistanceMode");

            _meshManager = serializedObject.FindProperty("_meshManager");
            _suppressionChannels = serializedObject.FindProperty("_requestedSuppressionChannels");

            _customMaterial = serializedObject.FindProperty("_customMaterial");
            _useCustomMaterial = serializedObject.FindProperty("_useCustomMaterial");

            _smoothEdgePreferred = serializedObject.FindProperty("_requestPreferSmoothEdges");
            _isOcclusionSuppressionEnabled = serializedObject.FindProperty("_requestOcclusionSuppressionEnabled");
            _isOcclusionStabilizationEnabled = serializedObject.FindProperty("_requestOcclusionStabilizationEnabled");
        }
    }
}

#endif // UNITY_EDITOR
