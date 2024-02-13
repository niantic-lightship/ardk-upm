// Copyright 2022-2024 Niantic.
#if UNITY_EDITOR
using System;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.PersistentAnchors;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.VpsCoverage;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ARLocation))]
internal class ARLocationEditor : Editor
{
    private bool _previousIncludeInBuildState;
    private const string _arMeshContainerNameString = "MeshContainer";
    private ARLocationManifest _manifest;
    internal const string DefaultARLocationName = "AR Location";

    private static class Contents
    {
        public static readonly GUIContent includeMeshInBuildLabel =
            new GUIContent
            (
                "Include Mesh in Build",
                "Determines whether the location mesh should be included in builds," +
                "by setting its tag to \"Untagged\" or \"EditorOnly\""
            );

        public static readonly GUIContent arLocationManifestLabel =
            new GUIContent
            (
                "AR Location Manifest",
                $"The {nameof(ARLocationManifest)} ScriptableObject asset associated with this {nameof(ARLocation)}"
            );

        public static readonly GUIContent payloadFromManifestLabel =
            new GUIContent
            (
                "Payload",
                $"The base 64 string representation of the {nameof(ARPersistentAnchorPayload)} from the provided manifest"
            );

        public static readonly GUIContent payloadFromUserLabel =
            new GUIContent
            (
                "Payload",
                $"The base 64 string from which to create an {nameof(ARPersistentAnchorPayload)}." +
                $"Overriden by the {nameof(ARLocationManifest)}'s payload if one is set."
            );

        public static readonly GUIContent latitudeLabel =
            new GUIContent
            (
                "Gps Latitude",
                "The latitude of the location, as defined by the manifest"
            );

        public static readonly GUIContent longitudeLabel =
            new GUIContent
            (
                "Gps Longitude",
                "The longitude of the location, as defined by the manifest"
            );
    }

    private void Awake()
    {
        var arLocation = (ARLocation)target;
        _previousIncludeInBuildState = arLocation.IncludeMeshInBuild;
    }

    public override void OnInspectorGUI()
    {
        var arLocation = (ARLocation)target;
        LayOutIncludeMeshInBuild(arLocation);
        LayOutARLocationManifest(arLocation);
        LayOutPayload(arLocation);
        LayOutGpsLocation(arLocation);
        UpdateARLocation(arLocation);
    }

    private void LayOutGpsLocation(ARLocation arLocation)
    {
        var location = arLocation.GpsLocation;
        EditorGUILayout.LabelField(Contents.latitudeLabel, new GUIContent($"{location.Latitude:0.000000}"));
        EditorGUILayout.LabelField(Contents.longitudeLabel, new GUIContent($"{location.Longitude:0.000000}"));
    }

    private void LayOutIncludeMeshInBuild(ARLocation arLocation)
    {
        arLocation.IncludeMeshInBuild =
            EditorGUILayout.Toggle
            (
                Contents.includeMeshInBuildLabel,
                arLocation.IncludeMeshInBuild
            );
    }

    private void LayOutARLocationManifest(ARLocation arLocation)
    {
        TryMigrateLocationManifestAsset(arLocation);
        if (!string.IsNullOrEmpty(arLocation.AssetGuid) && _manifest == null)
        {
            _manifest = LoadManifestFromGuid(arLocation.AssetGuid);
            if (!_manifest)
            {
                Log.Error($"Could not load ARLocationManifest for {arLocation.name}");
            }
        }

        var updatedManifest =
            (ARLocationManifest)EditorGUILayout.ObjectField
            (
                Contents.arLocationManifestLabel,
                _manifest,
                typeof(ARLocationManifest),
                false
            );

        if (updatedManifest != _manifest)
        {
            string previousManifestName = _manifest ? _manifest.LocationName : null;
            _manifest = updatedManifest;
            arLocation.AssetGuid = GetAssetGuidFromManifest(_manifest);

            arLocation.Payload = _manifest
                ? new ARPersistentAnchorPayload(_manifest.MeshOriginAnchorPayload)
                : null;
            if (arLocation.MeshContainer)
            {
                DestroyImmediate(arLocation.MeshContainer);
                Resources.UnloadUnusedAssets();
            }

            if (_manifest)
            {
                // If the name is managed by us, rename it. Otherwise don't touch it
                if ((previousManifestName != null && arLocation.name.StartsWith(previousManifestName)) ||
                    arLocation.name.StartsWith(DefaultARLocationName))
                {
                    arLocation.name = _manifest.LocationName;
                    GameObjectUtility.EnsureUniqueNameForSibling(arLocation.gameObject);
                }

                arLocation.MeshContainer = Instantiate(_manifest.MockAsset, arLocation.transform);
                arLocation.MeshContainer.name = _arMeshContainerNameString;
                arLocation.MeshContainer.tag = arLocation.IncludeMeshInBuild ? "Untagged" : "EditorOnly";
                arLocation.GpsLocation = new LatLng
                    (_manifest.LocationLatitude, _manifest.LocationLongitude);
            }
            else
            {
                if ((previousManifestName != null && arLocation.name.StartsWith(previousManifestName)) ||
                    arLocation.name.StartsWith(DefaultARLocationName))
                {
                    arLocation.name = DefaultARLocationName;
                    GameObjectUtility.EnsureUniqueNameForSibling(arLocation.gameObject);
                }

                arLocation.MeshContainer = null;
            }

            EditorUtility.SetDirty(target);
        }
    }

    // One time migration code to remove the hard linked ARLocationManifest and cache as asset guid instead
    private void TryMigrateLocationManifestAsset(ARLocation arLocation)
    {
        if (arLocation.ARLocationManifest != null)
        {
            var guid = GetAssetGuidFromManifest(arLocation.ARLocationManifest);
            if (guid == null)
            {
                return;
            }

            arLocation.AssetGuid = guid;
            arLocation.ARLocationManifest = null;
            EditorUtility.SetDirty(target);
        }
    }

    private ARLocationManifest LoadManifestFromGuid(string assetGuid)
    {
        var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
        if (string.IsNullOrEmpty(assetPath))
        {
            Log.Error($"Could not load ARLocationManifest {assetGuid}");
            return null;
        }

        var manifest = AssetDatabase.LoadAssetAtPath<ARLocationManifest>(assetPath);
        return manifest;
    }

    private string GetAssetGuidFromManifest(ARLocationManifest manifest)
    {
        var assetPath = AssetDatabase.GetAssetPath(manifest);

        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            Log.Error($"Could not get guid at path {assetPath}");
            return null;
        }

        return guid;
    }

    private void LayOutPayload(ARLocation arLocation)
    {
        if (_manifest)
        {
            EditorGUILayout.LabelField
            (
                Contents.payloadFromManifestLabel,
                new GUIContent(arLocation.Payload?.ToBase64())
            );
        }
        else
        {
            string payload = EditorGUILayout.DelayedTextField
            (
                Contents.payloadFromUserLabel,
                arLocation.Payload?.ToBase64()
            );
            if (payload != arLocation.Payload?.ToBase64())
            {
                var bytes = new Span<byte>(new byte[payload.Length]);
                bool valid = Convert.TryFromBase64String(payload, bytes, out int bytesWritten);
                if (!valid)
                {
                    Log.Error("Not a valid base 64 string");
                }

                arLocation.Payload = valid ? new ARPersistentAnchorPayload(payload) : null;
                EditorUtility.SetDirty(target);
            }
        }
    }

    private void UpdateARLocation(ARLocation arLocation)
    {
        if (arLocation != null && _previousIncludeInBuildState != arLocation.IncludeMeshInBuild)
        {
            _previousIncludeInBuildState = arLocation.IncludeMeshInBuild;
            if (arLocation.MeshContainer != null)
            {
                arLocation.MeshContainer.tag =
                    arLocation.IncludeMeshInBuild ? "Untagged" : "EditorOnly";

                EditorUtility.SetDirty(target);
            }
        }
    }
}
#endif
