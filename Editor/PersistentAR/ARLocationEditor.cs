#if UNITY_EDITOR
using System;
using Niantic.Lightship.AR.Subsystems;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ARLocation))]
internal class ARLocationEditor : Editor
{
    private ARLocationManifest _previousARLocationManifest;
    private bool _previousIncludeInBuildState;

    private void Awake()
    {
        var arLocation = (ARLocation)target;
        _previousARLocationManifest = arLocation.ARLocationManifest;
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
        arLocation.ARLocationManifest = (ARLocationManifest)EditorGUILayout.ObjectField("AR Location Manifest",
            arLocation.ARLocationManifest, typeof(ARLocationManifest), false);
    }

    private void LayOutPayload(ARLocation arLocation)
    {
        if (arLocation.ARLocationManifest)
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

        if (_previousARLocationManifest != arLocation.ARLocationManifest)
        {
            arLocation.Payload = arLocation.ARLocationManifest
                ? new ARPersistentAnchorPayload(arLocation.ARLocationManifest.MeshOriginAnchorPayload)
                : null;
            if (arLocation.MeshContainer)
            {
                DestroyImmediate(arLocation.MeshContainer);
            }

            if (arLocation.ARLocationManifest)
            {
                arLocation.name = arLocation.ARLocationManifest.LocationName;
                arLocation.MeshContainer = Instantiate(arLocation.ARLocationManifest.MockAsset, arLocation.transform);
                arLocation.MeshContainer.hideFlags = HideFlags.HideInHierarchy;
                arLocation.MeshContainer.tag = arLocation.IncludeMeshInBuild ? "Untagged" : "EditorOnly";
            }
            else
            {
                arLocation.MeshContainer = null;
            }
            _previousARLocationManifest = arLocation.ARLocationManifest;
            EditorUtility.SetDirty(target);
        }
    }
}
#endif
