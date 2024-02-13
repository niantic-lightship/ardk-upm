// Copyright 2022-2024 Niantic.
using System.Collections.Generic;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.VpsCoverage;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor.Inspectors
{
    [CustomEditor(typeof(CoverageClientManager))]
    internal class CoverageClientManagerInspector : UnityEditor.Editor
    {
        private CoverageClientManager Target => (CoverageClientManager)target;

        private SerializedProperty _useCurrentLocationProperty;
        private SerializedProperty _queryLatitudeProperty;
        private SerializedProperty _queryLongitudeProperty;

        private void OnEnable()
        {
            _useCurrentLocationProperty = serializedObject.FindProperty("_useCurrentLocation");
            _queryLatitudeProperty = serializedObject.FindProperty("_queryLatitude");
            _queryLongitudeProperty = serializedObject.FindProperty("_queryLongitude");
        }

        private LocalizationTarget[] CreateLocalizationTargets(ARLocationManifest[] locationManifests)
        {
            if (locationManifests == null)
            {
                return null;
            }
            List<LocalizationTarget> localizationTargets = new List<LocalizationTarget>();
            for (int i = 0; i < locationManifests.Length; i++)
            {
                if (locationManifests[i] != null)
                {
                    var localizationTarget = new LocalizationTarget(
                        locationManifests[i].NodeIdentifier,
                        // Adding the full namespace here because without it, pgo does not compile due to conflicting classes.
                        new Niantic.Lightship.AR.VpsCoverage.LatLng(locationManifests[i].LocationLatitude,
                            locationManifests[i].LocationLongitude),
                        locationManifests[i].LocationName,
                        string.Empty,
                        locationManifests[i].MeshOriginAnchorPayload);
                    localizationTargets.Add(localizationTarget);
                }
            }
            return localizationTargets.ToArray();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();

            var privateARLocations = CreateLocalizationTargets(Target.PrivateARLocations);
            Target.PrivateARLocalizationTargets = privateARLocations;

            GUILayout.BeginHorizontal();
            {
                GUIContent labelContent = new GUIContent("Use Current Location", null,
                    "Enable to use device's current location when querying coverage");
                EditorGUILayout.PropertyField(_useCurrentLocationProperty, labelContent, true);
            }
            GUILayout.EndHorizontal();

            // only display query latitude and longitude fields in inspector if useCurrentLocation toggle value is false
            if (!Target.UseCurrentLocation)
            {
                GUILayout.BeginHorizontal();
                {
                    GUIContent labelContent = new GUIContent("Query Latitude", null,
                        "Specified location latitude to use if not using device location");
                    EditorGUILayout.PropertyField(_queryLatitudeProperty, labelContent, true);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUIContent labelContent = new GUIContent("Query Longitude", null,
                        "Specified location longitude to use if not using device location");
                    EditorGUILayout.PropertyField(_queryLongitudeProperty, labelContent, true);
                }
                GUILayout.EndHorizontal();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
