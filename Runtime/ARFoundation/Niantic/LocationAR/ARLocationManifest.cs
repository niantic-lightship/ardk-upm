#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Niantic.Lightship.AR.Editor.Utilities;
using Niantic.Lightship.AR.RCA.Editor.Utilities;

using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems
{
    [Serializable]
    public sealed class ARLocationManifest : ScriptableObject
    {
        [SerializeField] [HideInInspector]
        private string _nodeIdentifier;

        [SerializeField] [HideInInspector]
        private string _meshOriginAnchorPayload;

        [SerializeField] [HideInInspector]
        private float _locationLatitude;

        [SerializeField] [HideInInspector]
        private float _locationLongitude;

        [SerializeField]
        private GameObject _mockAsset;

        private string _locationName;

        private Material[] _materials;

        private Mesh _mesh;

        public string LocationName
        {
            get
            {
                if (string.IsNullOrEmpty(_locationName))
                {
                    _locationName = Path.GetFileNameWithoutExtension
                        (AssetDatabase.GetAssetPath(this));
                }

                return _locationName;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(_locationName, value))
                {
                    return;
                }

                if (string.IsNullOrEmpty(_locationName))
                {
                    _locationName = value;
                    return;
                }

                var assetPath = AssetDatabase.GetAssetPath(this);

                var manifests = _AssetDatabaseUtilities.FindAssets<ARLocationManifest>();
                if (manifests.Any(m => string.Equals(m.LocationName, value)))
                {
                    Debug.LogWarning
                    (
                        $"Cannot rename location \'{_locationName}\'. " +
                        $"A location named \'{value}\' already exists."
                    );

                    // If value was changed by in the Project Browser instead of the Inspector,
                    // revert the name
                    if (string.Equals(Path.GetFileNameWithoutExtension(assetPath), value))
                    {
                        // Without the delay, Project Browser won't display corrected name until after asset
                        // is re-imported. Can't force asset re-import because of the name change, so a delay is
                        // the solution.
                        var oldName = _locationName + ".asset";
                        EditorApplication.delayCall += () => AssetDatabase.RenameAsset
                            (assetPath, oldName);
                    }

                    return;
                }

                _locationName = value;
                AssetDatabase.RenameAsset(assetPath, value + ".asset");
            }
        }

        public float LocationLatitude
        {
            get => _locationLatitude;
            set
            {
                _locationLatitude = value;
                EditorUtility.SetDirty(this);
            }
        }

        public float LocationLongitude
        {
            get => _locationLongitude;
            set
            {
                _locationLongitude = value;
                EditorUtility.SetDirty(this);
            }
        }

        internal string NodeIdentifier
        {
            get => _nodeIdentifier;
            set
            {
                _nodeIdentifier = value;
                EditorUtility.SetDirty(this);
            }
        }

        internal string MeshOriginAnchorPayload
        {
            get => _meshOriginAnchorPayload;
            set
            {
                _meshOriginAnchorPayload = value;
                EditorUtility.SetDirty(this);
            }
        }

        public Mesh Mesh
        {
            get
            {
                if (_mesh == null)
                {
                    var assetPath = AssetDatabase.GetAssetPath(this);
                    _mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

                    if (_mesh == null)
                    {
                        Debug.LogError
                            ($"No mesh found as sub-asset of {name} (ARLocationManifest)");
                    }
                }

                return _mesh;
            }
        }

        public Material[] Materials
        {
            get
            {
                if (_materials == null)
                {
                    var assetPath = AssetDatabase.GetAssetPath(this);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    _materials = assets.OfType<Material>().ToArray();
                    Array.Sort(_materials, (x, y) => x.name.CompareTo(y.name));
                    var textures = assets.OfType<Texture2D>().ToArray();
                    Array.Sort(textures, (x, y) => x.name.CompareTo(y.name));

                    for (var i = 0; i < _materials.Length; i++)
                    {
                        _materials[i].mainTexture = textures[i];
                    }

                    if (_materials == null)
                    {
                        Debug.LogError
                            ($"No material found as sub-asset of {name} (ARLocationManifest)");
                    }
                }

                return _materials;
            }
        }

        internal GameObject MockAsset
        {
            get => _mockAsset;
            set
            {
                _mockAsset = value;
                EditorUtility.SetDirty(this);
            }
        }

        private void OnValidate()
        {
            LocationName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(this));
        }

        internal void _CreateMockAsset()
        {
            // Create root
            var rootGo = new GameObject("Root");
            rootGo.AddComponent<_TransformFixer>();

            // Create mesh and components
            var meshGo = new GameObject(LocationName + "(LocationMesh)");
            var meshFilter = meshGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = Mesh;

            var renderer = meshGo.AddComponent<MeshRenderer>();
            var tryGetMaterials = Materials;

            if (tryGetMaterials != null)
            {
                renderer.sharedMaterials = tryGetMaterials;
            }

            meshGo.transform.SetParent(rootGo.transform, true);
            meshGo.AddComponent<_TransformFixer>();

            // Save asset
            var manifestPath = AssetDatabase.GetAssetOrScenePath(this);
            var assetPath =
                _ProjectBrowserUtilities.BuildAssetPath
                (
                    LocationName + "(MockLocation).prefab",
                    Path.GetDirectoryName(manifestPath)
                );

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, assetPath);
            MockAsset = prefab;

            // Cleanup in scene
            DestroyImmediate(rootGo);
        }
    }
}
#endif
