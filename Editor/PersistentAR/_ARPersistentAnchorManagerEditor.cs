// Copyright 2022-2024 Niantic.

using System;

using Niantic.Lightship.AR.PersistentAnchors;
using Niantic.Lightship.AR.Subsystems.PersistentAnchor;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;

using UnityEditor;

using UnityEngine;

namespace Niantic.Lightship.AR.Editor.PersistentAR
{
    [CustomEditor(typeof(ARPersistentAnchorManager))]
    internal class _ARPersistentAnchorManagerEditor :
        UnityEditor.Editor
    {
        private static bool _driftMitigationFoldout = true;
        private static bool _performanceFoldout = true;
        private static bool _diagnosticsFoldout = true;

        private LightshipVpsUsageUtility.LightshipVpsUsageMode _previousVpsUsageMode;

        private SerializedProperty _DefaultAnchorGameObject;
        private SerializedProperty _VpsUsageMode;
        private SerializedProperty _ContinuousLocalizationEnabled;

        private SerializedProperty _InterpolationEnabled;
        private SerializedProperty _TemporalFusionEnabled;
        private SerializedProperty _JpegCompressionQuality;
        private SerializedProperty _InitialServiceRequestIntervalSeconds;

        private SerializedProperty _ContinuousServiceRequestIntervalSeconds;
        private SerializedProperty _SyncFusionWindow;
        private SerializedProperty _CloudLocalizationTemporalFusionWindowSize;
        private SerializedProperty _DiagnosticsEnabled;

        private GUIContent temporalFusionWindowSizeLabel = new GUIContent("Cloud Localization Temporal Fusion Window Size");

#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
        private bool _experimentalFoldout = false;

        private SerializedProperty _LimitedLocalizationsOnly;
#endif

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_DefaultAnchorGameObject);

            // Disable all fields when in play mode
            // Vps config currently doesn't support dynamic changes while subsystem is running,
            // so we disable the fields to prevent confusion
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                DrawFields();
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFields()
        {
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_VpsUsageMode);
            var usageMode = (LightshipVpsUsageUtility.LightshipVpsUsageMode)_VpsUsageMode.intValue;
            if (usageMode != _previousVpsUsageMode)
            {
                ApplyNewVpsUsageMode(usageMode);
                _previousVpsUsageMode = usageMode;
            }

            EditorGUILayout.Space();

            // Start vps param change check
            EditorGUI.BeginChangeCheck();
            _driftMitigationFoldout = EditorGUILayout.Foldout(_driftMitigationFoldout, new GUIContent("Drift Mitigation"), true);
            if (_driftMitigationFoldout)
            {
                EditorGUILayout.PropertyField(_ContinuousLocalizationEnabled);
                using (new EditorGUI.DisabledScope(_ContinuousLocalizationEnabled.boolValue == false))
                {
                    EditorGUILayout.PropertyField(_InterpolationEnabled);
                    EditorGUILayout.PropertyField(_TemporalFusionEnabled);
                }
            }

            EditorGUILayout.Space();
            _performanceFoldout = EditorGUILayout.Foldout(_performanceFoldout, new GUIContent("Performance"), true);
            if (_performanceFoldout)
            {
                EditorGUILayout.PropertyField(_JpegCompressionQuality);
                EditorGUILayout.PropertyField(_InitialServiceRequestIntervalSeconds);
                if (_InitialServiceRequestIntervalSeconds.floatValue < 0)
                {
                    _InitialServiceRequestIntervalSeconds.floatValue = 0;
                }

                using (new EditorGUI.DisabledScope(_ContinuousLocalizationEnabled.boolValue == false))
                {
                    EditorGUILayout.PropertyField(_ContinuousServiceRequestIntervalSeconds);
                    if (_ContinuousServiceRequestIntervalSeconds.floatValue < 0)
                    {
                        _ContinuousServiceRequestIntervalSeconds.floatValue = 0;
                    }

#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
                    using (new EditorGUI.DisabledScope(_TemporalFusionEnabled.boolValue == false))
                    {
                        EditorGUILayout.PropertyField(_SyncFusionWindow);
                        if (_SyncFusionWindow.boolValue)
                        {
                            var requestRate = _ContinuousServiceRequestIntervalSeconds.floatValue
                                .ZeroOrReciprocal();

                            _CloudLocalizationTemporalFusionWindowSize.intValue =
                                (int)XRPersistentAnchorConfiguration
                                    .DetermineFusionWindowFromRequestRate
                                        (requestRate);

                            EditorGUILayout.LabelField
                            (
                                temporalFusionWindowSizeLabel,
                                new GUIContent
                                    (_CloudLocalizationTemporalFusionWindowSize.intValue.ToString())
                            );
                        }
                        else
                        {
                            EditorGUILayout.PropertyField
                                (_CloudLocalizationTemporalFusionWindowSize);
                        }
                    }
#endif
                }
            }

            // End vps param change check
            var haveParamsChanged = EditorGUI.EndChangeCheck();

            if (haveParamsChanged)
            {
                _VpsUsageMode.intValue = (int)LightshipVpsUsageUtility.LightshipVpsUsageMode.Custom;
                _previousVpsUsageMode = LightshipVpsUsageUtility.LightshipVpsUsageMode.Custom;
            }

            EditorGUILayout.Space();
            _diagnosticsFoldout = EditorGUILayout.Foldout(_diagnosticsFoldout, new GUIContent("Diagnostics"), true);
            if (_diagnosticsFoldout)
            {
                EditorGUILayout.PropertyField(_DiagnosticsEnabled);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
            EditorGUILayout.Space();
            _experimentalFoldout = EditorGUILayout.Foldout(_experimentalFoldout, new GUIContent("Experimental"), true);
            if (_experimentalFoldout)
            {
                EditorGUILayout.PropertyField(_LimitedLocalizationsOnly);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
#endif
        }

        private void ApplyNewVpsUsageMode(LightshipVpsUsageUtility.LightshipVpsUsageMode usageMode)
        {
            if (usageMode == LightshipVpsUsageUtility.LightshipVpsUsageMode.Custom)
            {
                return;
            }

            if (usageMode == LightshipVpsUsageUtility.LightshipVpsUsageMode.Default)
            {
                usageMode = LightshipVpsUsageUtility.LightshipVpsUsageMode.SingleLocalization;
            }

            var config = LightshipVpsUsageUtility.CreateConfiguration(usageMode);

            _ContinuousLocalizationEnabled.boolValue = config.ContinuousLocalizationEnabled;
            _InterpolationEnabled.boolValue = config.TransformUpdateSmoothingEnabled;
            _TemporalFusionEnabled.boolValue = config.TemporalFusionEnabled;
            _JpegCompressionQuality.intValue = config.JpegCompressionQuality;
            _InitialServiceRequestIntervalSeconds.floatValue = config.CloudLocalizerInitialRequestsPerSecond;
            _ContinuousServiceRequestIntervalSeconds.floatValue = config.CloudLocalizerContinuousRequestsPerSecond.ZeroOrReciprocal();
            if (_TemporalFusionEnabled.boolValue)
            {
#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
                _SyncFusionWindow.boolValue = true;
                _CloudLocalizationTemporalFusionWindowSize.intValue =
                    (int)XRPersistentAnchorConfiguration
                        .DetermineFusionWindowFromRequestRate
                            (_ContinuousServiceRequestIntervalSeconds.floatValue.ZeroOrReciprocal());
#endif
            }
        }

        protected void OnEnable()
        {
            _DefaultAnchorGameObject = serializedObject.FindProperty("_defaultAnchorGameobject");
            _VpsUsageMode = serializedObject.FindProperty("_VpsUsageMode");
            _previousVpsUsageMode = (LightshipVpsUsageUtility.LightshipVpsUsageMode)_VpsUsageMode.intValue;
            _ContinuousLocalizationEnabled = serializedObject.FindProperty("_ContinuousLocalizationEnabled");

            _InterpolationEnabled = serializedObject.FindProperty("_InterpolationEnabled");
            _TemporalFusionEnabled = serializedObject.FindProperty("_TemporalFusionEnabled");
            _JpegCompressionQuality = serializedObject.FindProperty("_JpegCompressionQuality");
            _InitialServiceRequestIntervalSeconds = serializedObject.FindProperty("_InitialServiceRequestIntervalSeconds");

            _ContinuousServiceRequestIntervalSeconds = serializedObject.FindProperty("_ContinuousServiceRequestIntervalSeconds");
            _CloudLocalizationTemporalFusionWindowSize = serializedObject.FindProperty("_CloudLocalizationTemporalFusionWindowSize");
            _SyncFusionWindow = serializedObject.FindProperty("_SyncFusionWindow");
            _DiagnosticsEnabled = serializedObject.FindProperty("_DiagnosticsEnabled");

#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
            _LimitedLocalizationsOnly = serializedObject.FindProperty("_LimitedLocalizationsOnly");
#endif
        }
    }
}
