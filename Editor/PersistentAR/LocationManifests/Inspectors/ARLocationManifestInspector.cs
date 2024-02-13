// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.PersistentAnchors;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor.Inspectors
{
    [CustomEditor(typeof(ARLocationManifest))]
    internal class ARLocationManifestInspector : UnityEditor.Editor
    {
        private static GUIStyle _rightJustifiedLabelStyle;
        private readonly int _colOneWidth = 200;
        private bool _anchorFoldoutState;

        private bool _copied;
        private const int _MaxPayloadHintLength = 40;

        private ARLocationManifest Target => (ARLocationManifest)target;

        public static GUIStyle RightJustifiedLabelStyle
        {
            get
            {
                if (_rightJustifiedLabelStyle == null)
                {
                    _rightJustifiedLabelStyle = new GUIStyle(EditorStyles.label);
                    _rightJustifiedLabelStyle.alignment = TextAnchor.MiddleRight;
                    _rightJustifiedLabelStyle.wordWrap = true;
                }

                return _rightJustifiedLabelStyle;
            }
        }

        private static class Contents
        {
            public static readonly GUIStyle rightJustifiedLabelStyle =
                new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight, wordWrap = true };

            public static readonly GUIStyle boldLabelStyle = new GUIStyle(EditorStyles.boldLabel);

            public static readonly GUIContent locationNameLabel =
                new GUIContent
                (
                    "Location Name",
                    "The name of the location"
                );

            public static readonly GUIContent locationMeshLabel =
                new GUIContent
                (
                    "Location Mesh",
                    "The mesh of the location"
                );

            public static readonly GUIContent locationLatitudeLabel =
                new GUIContent
                (
                    "Location Latitude",
                    "The latitude of the location"
                );

            public static readonly GUIContent locationLongitudeLabel =
                new GUIContent
                (
                    "Location Longitude",
                    "The longitude of the location"
                );

            public static readonly GUIContent mockLocationAssetLabel =
                new GUIContent
                (
                    "Mock Location Asset",
                    "A prefab asset with a mesh for using this location in Mock mode"
                );

            public static readonly GUIContent anchorPayloadLabel = new GUIContent("Anchor Payload");

            public static readonly GUIContent createButtonLabel =
                new GUIContent
                (
                    "Create",
                    "Create a prefab for this AR Location that can be used in Mock Mode."
                );

            public static readonly GUIContent copyButtonLabel =
                new GUIContent
                (
                    "Copy",
                    "Copy payload string to clipboard"
                );
        }

        private void OnEnable()
        {
            _copied = false;
        }

        public override void OnInspectorGUI()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(Contents.locationNameLabel, GUILayout.Width(_colOneWidth));
                var newLocationName = EditorGUILayout.DelayedTextField(Target.LocationName);
                if (!string.Equals(Target.LocationName, newLocationName))
                {
                    Target.LocationName = newLocationName;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(Contents.locationMeshLabel, GUILayout.Width(_colOneWidth));
                DrawMeshGUI();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(Contents.locationLatitudeLabel, GUILayout.Width(_colOneWidth));
                var newLocationLatitude = EditorGUILayout.DelayedFloatField(Target.LocationLatitude);
                if (Math.Abs(Target.LocationLatitude - newLocationLatitude) > .00001)
                {
                    Target.LocationLatitude = newLocationLatitude;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(Contents.locationLongitudeLabel, GUILayout.Width(_colOneWidth));
                var newLocationLongitude = EditorGUILayout.DelayedFloatField(Target.LocationLongitude);
                if (Math.Abs(Target.LocationLongitude - newLocationLongitude) > .00001)
                {
                    Target.LocationLongitude = newLocationLongitude;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(Contents.mockLocationAssetLabel, GUILayout.Width(_colOneWidth));
                DrawMockAssetGUI();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            EditorGUILayout.LabelField
            (
                Contents.anchorPayloadLabel,
                Contents.boldLabelStyle,
                GUILayout.Width(_colOneWidth)
            );
            DrawAnchorPayloadGUI(Target.MeshOriginAnchorPayload, Target.NodeIdentifier);
            GUILayout.Space(50);
        }

        private void DrawMeshGUI()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(Target.Mesh, typeof(GameObject), false);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawMockAssetGUI()
        {
            GUILayout.BeginHorizontal();

            var asset = Target.MockAsset;
            var newAsset = EditorGUILayout.ObjectField(asset, typeof(GameObject), false) as GameObject;

            if (asset != newAsset)
            {
                if (newAsset == null)
                {
                    Target.MockAsset = null;
                }

                else if (PrefabUtility.GetPrefabAssetType(newAsset) == PrefabAssetType.NotAPrefab)
                {
                    Log.Info("Invalid mock scene asset selected. Must be a prefab.");
                }

                else
                {
                    Target.MockAsset = newAsset;
                }
            }

            if (asset == null)
            {
                if (GUILayout.Button(Contents.createButtonLabel))
                {
                    Target._CreateMockAsset();
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawAnchorPayloadGUI(string payload, string anchorIdentifier)
        {
            GUILayout.BeginVertical();
            var payloadHint = payload;
            if (payloadHint.Length > _MaxPayloadHintLength)
            {
                payloadHint = payload.Substring(0, _MaxPayloadHintLength) + "...";
            }

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField
            (
                new GUIContent
                (
                    payloadHint,
                    $"The base 64 string representation of the {nameof(ARPersistentAnchorPayload)}'s data"
                )
            );
            if (GUILayout.Button(Contents.copyButtonLabel))
            {
                GUIUtility.systemCopyBuffer = payload;
                _copied = true;
            }

            GUILayout.EndHorizontal();

            if (_copied)
            {
                GUILayout.Label("Copied to clipboard!", RightJustifiedLabelStyle);
            }

            GUILayout.EndVertical();
        }
    }
}
