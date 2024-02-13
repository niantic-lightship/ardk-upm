// Copyright 2022-2024 Niantic.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;

using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.UnityAssets;

using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.LocationAR
{
    /// <summary>
    /// Information associated with a scanned AR Location
    /// </summary>
    [PublicAPI]
    [Serializable]
    [PreferBinarySerialization]
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

        [SerializeField] [HideInInspector]
        private string _localizationTargetId;

        private string _locationName;

        private Material[] _materials;

        private Mesh _mesh;

        /// <summary>
        /// The name of the location
        /// </summary>
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

                var manifests = AssetDatabaseUtilities.FindAssets<ARLocationManifest>();
                if (manifests.Any(m => string.Equals(m.LocationName, value)))
                {
                    Log.Warning
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

        /// <summary>
        /// The target ID of the localization
        /// </summary>
        public string LocalizationTargetId
        {
            get => _localizationTargetId;
            set
            {
                _localizationTargetId = value;
                EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// The latitude of the location
        /// </summary>
        public float LocationLatitude
        {
            get => _locationLatitude;
            set
            {
                _locationLatitude = value;
                EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// The longitude of the location
        /// </summary>
        public float LocationLongitude
        {
            get => _locationLongitude;
            set
            {
                _locationLongitude = value;
                EditorUtility.SetDirty(this);
            }
        }

        public string AnchorPayload
        {
            get => _meshOriginAnchorPayload;
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

        /// <summary>
        /// The mesh of the location
        /// </summary>
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
                        Log.Error
                            ($"No mesh found as sub-asset of {name} (ARLocationManifest)");
                    }
                }

                return _mesh;
            }
        }

        /// <summary>
        /// The materials used to render the location's mesh
        /// </summary>
        public Material[] Materials
        {
            get
            {
                if (_materials == null)
                {
                    var assetPath = AssetDatabase.GetAssetPath(this);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    _materials = assets.OfType<Material>().ToArray();
                    Array.Sort(_materials, (x, y) => IntInStringComparator(x.name, y.name));
                    var textures = assets.OfType<Texture2D>().ToArray();
                    Array.Sort(textures, (x, y) => IntInStringComparator(x.name, y.name));

                    for (var i = 0; i < _materials.Length; i++)
                    {
                        _materials[i].mainTexture = textures[i];
                    }

                    if (_materials == null)
                    {
                        Log.Error
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
            rootGo.AddComponent<TransformFixer>();

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
            meshGo.AddComponent<TransformFixer>();

            // Save asset
            var manifestPath = AssetDatabase.GetAssetOrScenePath(this);
            var assetPath =
                ProjectBrowserUtilities.BuildAssetPath
                (
                    LocationName + "(MockLocation).prefab",
                    Path.GetDirectoryName(manifestPath)
                );

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, assetPath);
            MockAsset = prefab;

            // Cleanup in scene
            DestroyImmediate(rootGo);
        }

        // Sort two strings by comparing getting all of the ints (a4b3c1 -> 431)
        // Used by material/texture sorter which only have meaningful suffices (ie: mat_31)
        private int IntInStringComparator(string x, string y)
        {
            var parsedX = Int32.TryParse(x.Where(Char.IsDigit).ToArray(), out var xCount);
            var parsedY = Int32.TryParse(y.Where(Char.IsDigit).ToArray(), out var yCount);

            // If no int is gotten out, use string compare
            if (!(parsedX && parsedY))
            {
                return x.CompareTo(y);
            }

            // If x is lower than y, it should come first
            return xCount - yCount;
        }
    }
}
#endif
