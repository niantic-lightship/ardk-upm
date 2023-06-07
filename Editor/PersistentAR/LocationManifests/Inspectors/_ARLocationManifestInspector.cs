using System;
using Niantic.Lightship.AR.Subsystems;

using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor.Inspectors
{
    [CustomEditor(typeof(ARLocationManifest))]
    internal class _ARLocationManifestInspector : UnityEditor.Editor
    {
        private static GUIStyle _payloadStyle;
        private static GUIStyle _centeredLabelStyle;
        private readonly int _colOneWidth = 200;
        private bool _anchorFoldoutState;

        private string _copiedAnchorIdentifier;
        private float _timeout;
        private const int _MaxPayloadHintLength = 40;

        private ARLocationManifest Target => (ARLocationManifest)target;

        public static GUIStyle PayloadStyle
        {
            get
            {
                if (_payloadStyle == null)
                {
                    _payloadStyle = new GUIStyle(EditorStyles.textArea);
                    _payloadStyle.wordWrap = false;
                }

                return _payloadStyle;
            }
        }

        public static GUIStyle CenteredLabelStyle
        {
            get
            {
                if (_centeredLabelStyle == null)
                {
                    _centeredLabelStyle = new GUIStyle(EditorStyles.label);
                    _centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
                    _centeredLabelStyle.wordWrap = true;
                }

                return _centeredLabelStyle;
            }
        }

        public override void OnInspectorGUI()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Location Name", GUILayout.Width(_colOneWidth));
                var newLocationName = EditorGUILayout.DelayedTextField(Target.LocationName);
                if (!string.Equals(Target.LocationName, newLocationName))
                {
                    Target.LocationName = newLocationName;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Location Mesh", GUILayout.Width(_colOneWidth));
                DrawMeshGUI();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Location Latitude", GUILayout.Width(_colOneWidth));
                var newLocationLatitude = EditorGUILayout.DelayedFloatField(Target.LocationLatitude);
                if (Math.Abs(Target.LocationLatitude - newLocationLatitude) > .00001)
                {
                    Target.LocationLatitude = newLocationLatitude;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Location Longitude", GUILayout.Width(_colOneWidth));
                var newLocationLongitude = EditorGUILayout.DelayedFloatField(Target.LocationLongitude);
                if (Math.Abs(Target.LocationLongitude - newLocationLongitude) > .00001)
                {
                    Target.LocationLongitude = newLocationLongitude;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Mock Location Asset", GUILayout.Width(_colOneWidth));
                DrawMockAssetGUI();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            EditorGUILayout.LabelField("Anchor Payload", GUILayout.Width(_colOneWidth));
            DrawAnchorPayloadGUI(Target.MeshOriginAnchorPayload, Target.NodeIdentifier);
            GUILayout.Space(50);
        }

        private void DrawMeshGUI()
        {
            EditorGUILayout.ObjectField(Target.Mesh, typeof(GameObject), false);
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
                    Debug.Log("Invalid mock scene asset selected. Must be a prefab.");
                }

                else
                {
                    Target.MockAsset = newAsset;
                }
            }

            if (asset == null)
            {
                if (GUILayout.Button(new GUIContent("Create",
                        "Create a prefab for this AR Location that can be used in Mock Mode.")))
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

            if (GUILayout.Button(payloadHint, PayloadStyle))
            {
                GUIUtility.systemCopyBuffer = payload;
                _timeout = Time.realtimeSinceStartup + 1;
                _copiedAnchorIdentifier = anchorIdentifier;
            }

            if (anchorIdentifier.Equals(_copiedAnchorIdentifier) && Time.realtimeSinceStartup < _timeout)
            {
                GUILayout.Label("Copied!", CenteredLabelStyle);
            }
            else
            {
                GUILayout.Label("Click to copy", CenteredLabelStyle);
            }

            GUILayout.EndVertical();
        }
    }
}
