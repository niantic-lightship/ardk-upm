#if UNITY_EDITOR
using System;
using Niantic.Lightship.AR.Subsystems;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ARLocation))]
internal class ARLocationEditor : Editor
{
    private bool _previousIncludeInBuildState;
    private const string _arMeshContainerNameString = "MeshContainer";
    private ARLocationManifest _manifest;
    private const string _defaultARLocationName = "AR Location";

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
        UpdateARLocation(arLocation);
    }

    private void LayOutIncludeMeshInBuild(ARLocation arLocation)
    {
         arLocation.IncludeMeshInBuild = EditorGUILayout.Toggle("Include Mesh in Build", arLocation.IncludeMeshInBuild);
    }

    private void LayOutARLocationManifest(ARLocation arLocation)
    {
        TryMigrateLocationManifestAsset(arLocation);
        if (!string.IsNullOrEmpty(arLocation.AssetGuid) && _manifest == null)
        {
            _manifest = LoadManifestFromGuid(arLocation.AssetGuid);
            if (!_manifest)
            {
                Debug.LogError($"Could not load ARLocationManifest for {arLocation.name}");
            }
        }

        var updatedManifest = (ARLocationManifest)EditorGUILayout.ObjectField("AR Location Manifest",
            _manifest, typeof(ARLocationManifest), false);

        if (updatedManifest != _manifest)
        {
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
                arLocation.name = _manifest.LocationName;
                arLocation.MeshContainer = Instantiate(_manifest.MockAsset, arLocation.transform);
                arLocation.MeshContainer.name = _arMeshContainerNameString;
                arLocation.MeshContainer.tag = arLocation.IncludeMeshInBuild ? "Untagged" : "EditorOnly";
            }
            else
            {
                arLocation.name = _defaultARLocationName;
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
            Debug.LogError($"Could not load ARLocationManifest {assetGuid}");
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
            Debug.LogError($"Could not get guid at path {assetPath}");
            return null;
        }

        return guid;
    }

    private void LayOutPayload(ARLocation arLocation)
    {
        if (_manifest)
        {
            EditorGUILayout.LabelField("Payload", arLocation.Payload?.ToBase64());
        }
        else
        {
            string payload = EditorGUILayout.TextField("Payload", arLocation.Payload?.ToBase64());
            if (payload != arLocation.Payload?.ToBase64())
            {
                var bytes = new Span<byte>(new byte[payload.Length]);
                bool valid = Convert.TryFromBase64String(payload, bytes, out int bytesWritten);
                if (!valid)
                {
                    Debug.LogError("Not a valid ARPersistentAnchorPayload");
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
