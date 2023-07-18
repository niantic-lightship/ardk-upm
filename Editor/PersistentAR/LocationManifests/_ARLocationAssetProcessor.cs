using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Niantic.Lightship.AR.Utilities.UnityAsset;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;
using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;

namespace Niantic.Lightship.AR.Editor
{
    internal class _ARLocationAssetProcessor : AssetPostprocessor
    {
        [Serializable]
        private struct LocationData
        {
            public string NodeIdentifier;
            public string DefaultAnchorPayload;
            public string AnchorPayload;
            public string LocalizationTargetName;
            public string LocalizationTargetID;
            public EdgeRepresentation LocalToSpace;
        }

        [Serializable]
        private struct MeshData
        {
            public Mesh GeneratedMesh;
            public string NodeIdentifier;
            public Vector3 TranslationToTarget;
            public Quaternion RotationToTarget;
            public Texture2D Texture;
        }

        [Serializable]
        private struct NodeRepresentation
        {
            public string identifier;
        }

        [Serializable]
        private struct EdgeRepresentation
        {
            public string source;
            public string destination;
            public TransformRepresentation sourceToDestination;
        }

        [Serializable]
        private struct TransformRepresentation
        {
            public Translation translation;
            public Rotation rotation;
            public float scale;
        }

        [Serializable]
        private struct Translation
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private struct Rotation
        {
            public float x;
            public float y;
            public float z;
            public float w;
        }

        private static void OnPostprocessAllAssets
        (
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            if (importedAssets.Length == 0)
                return;

            var zips = importedAssets.Where
                    (a => string.Equals(Path.GetExtension(a), ".zip"))
                .ToArray();

            if (zips.Length == 0)
                return;

            // Have to delay it a frame in order for all imports to work synchronously
            EditorApplication.delayCall += () => ProcessAllImports(zips);
        }

        private static void ProcessAllImports(string[] zips)
        {
            var allManifests = new List<ARLocationManifest>();
            foreach (var path in zips)
            {
                if (TryCreateLocationManifest(path, out ARLocationManifest manifest))
                    allManifests.Add(manifest);
                else
                    break;
            }

            if (allManifests.Count == 0)
                return;

            AssetDatabase.SaveAssets();

            foreach (var manifest in allManifests)
            {
                manifest._CreateMockAsset();
            }
        }

        private static bool TryCreateLocationManifest(string zipPath, out ARLocationManifest manifest)
        {
            manifest = null;

            var isValidZip =
                FindArchivedFiles
                (
                    zipPath,
                    out LocationData locationData,
                    out List<MeshData> meshDataList
                );

            if (!isValidZip)
            {
                // This was not an GSB zip, don't delete zip
                CleanupCreatedAssets(zipPath, meshDataList, false);
                return false;
            }

            Debug.Log("Importing: " + zipPath);

            var dir = Path.GetDirectoryName(zipPath);

            var locationName = locationData.LocalizationTargetName;
            if (string.IsNullOrEmpty(locationName))
                locationName = "Unnamed";

            var manifestPath = _ProjectBrowserUtilities.BuildAssetPath
                (locationName + ".asset", dir);

            manifest = CreateManifest(locationData, manifestPath);

            try
            {
                AssetDatabase.StartAssetEditing();

                // Need to create a copy in order to organize as sub-asset of the manifest
                var meshCopy = GenerateMeshAsset(meshDataList);
                AssetDatabase.AddObjectToAsset(meshCopy, manifest);

                // Does a second enumeration for textures and materials, but this is cleaner
                var count = 0;
                Texture2D texCopy = null;
                Material mat = null;
                foreach (var data in meshDataList)
                {
                    if (data.Texture != null)
                    {
                        texCopy = UnityEngine.Object.Instantiate(data.Texture);
                    }
                    else
                    {
                        texCopy = new Texture2D(2048, 2048);
                    }

                    texCopy.name = $"submesh_texture_{count}";

                    if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
                    {
                        mat = new Material
                        (
                            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline
                                .defaultShader
                        );
                    }
                    else
                    {
                        mat = new Material(Shader.Find("Standard"));
                    }

                    mat.name = $"submesh_material_{count}";

                    AssetDatabase.AddObjectToAsset(mat, manifest);
                    AssetDatabase.AddObjectToAsset(texCopy, manifest);
                    count++;
                }

                Selection.activeObject = manifest;

                // When pinged without delay, project browser window is displayed for a moment
                // before elements are alphabetically sorted, potentially leading to objects moving around.
                var createdManifest = manifest;
                EditorApplication.delayCall += () => EditorGUIUtility.PingObject(createdManifest);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            CleanupCreatedAssets(zipPath, meshDataList);

            return isValidZip;
        }

        private static bool FindArchivedFiles
        (
            string zipPath,
            out LocationData locationData,
            out List<MeshData> meshData
        )
        {
            Texture2D tex = null;
            locationData = new LocationData();
            meshData = new List<MeshData>();

            using (var file = File.OpenRead(zipPath))
            {
                using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    var validEntries = zip.Entries.Where(e => !e.Name.StartsWith("._"));
                    var meshEntries = validEntries.Where
                        (e => Path.GetExtension(e.Name).Equals(".fbx"));

                    var texEntries = validEntries.Where
                        (e => Path.GetExtension(e.Name).Equals(".jpeg"));

                    var locationEntries = validEntries.Where
                        (e => Path.GetExtension(e.Name).Equals(".json"));

                    var additionalMeshes = validEntries.Where
                        (e => Path.GetExtension(e.Name).Equals(".zip"));

                    if (!(meshEntries.Any() && locationEntries.Any()))
                        return false;

                    var mesh = ImportMesh(meshEntries.First(), "ARLocationMesh.fbx");
                    locationData = ParseLocationData(locationEntries.First());

                    // Some nodes do not have textures
                    if (texEntries.Any())
                        tex = ImportTexture(texEntries.First());

                    var initialMeshData = new MeshData()
                    {
                        GeneratedMesh = mesh,
                        NodeIdentifier = locationData.NodeIdentifier,
                        RotationToTarget = RotationFromEdge(locationData.LocalToSpace),
                        TranslationToTarget = TranslationFromEdge(locationData.LocalToSpace),
                        Texture = tex
                    };

                    meshData = UnpackAdditionalMeshes(additionalMeshes);
                    meshData.Insert(0, initialMeshData);

                    return !(meshData.Count == 0 ||
                        string.IsNullOrEmpty(locationData.DefaultAnchorPayload));
                }
            }
        }

        private static List<MeshData> UnpackAdditionalMeshes(IEnumerable<ZipArchiveEntry> zippedEntries)
        {
            var ret = new List<MeshData>();

            foreach (var entry in zippedEntries)
            {
                using (var zip = new ZipArchive(entry.Open(), ZipArchiveMode.Read))
                {
                    var validEntries = zip.Entries.Where(e => !e.Name.StartsWith("._"));
                    var meshEntries = validEntries.Where
                        (e => Path.GetExtension(e.Name).Equals(".fbx"));

                    var textureEntries = validEntries.Where
                        (e => Path.GetExtension(e.Name).Equals(".jpeg"));

                    var edgeEntries = validEntries.Where
                        (e => Path.GetExtension(e.Name).Equals(".json"));

                    var edgeData = ParseLocationData(edgeEntries.First());
                    var texture = ImportTexture(textureEntries.First());

                    var mesh = meshEntries.First();
                    var strippedName = mesh.Name.Split('.')[0];
                    var imported = ImportMesh(mesh, $"ARTempMesh{strippedName}.fbx");

                    var meshData = new MeshData()
                    {
                        GeneratedMesh = imported,
                        NodeIdentifier = strippedName,
                        TranslationToTarget = TranslationFromEdge(edgeData.LocalToSpace),
                        RotationToTarget = RotationFromEdge(edgeData.LocalToSpace),
                        Texture = texture
                    };

                    ret.Add(meshData);
                }
            }

            return ret;
        }

        private static Vector3 TranslationFromEdge(EdgeRepresentation edgeData)
        {
            var transform = edgeData.sourceToDestination;

            var translation = new Vector3
            (
                transform.translation.x,
                transform.translation.y,
                transform.translation.z
            );

            return translation;
        }

        private static Quaternion RotationFromEdge(EdgeRepresentation edgeData)
        {
            var transform = edgeData.sourceToDestination;

            var isRotationEmpty = IsFloatZero(transform.rotation.x) &&
                IsFloatZero(transform.rotation.y) &&
                IsFloatZero(transform.rotation.z) &&
                IsFloatZero(transform.rotation.w);

            Quaternion rotation;
            if (isRotationEmpty)
            {
                rotation = Quaternion.identity;
            }
            else
            {
                rotation = new Quaternion
                (
                    transform.rotation.x,
                    transform.rotation.y,
                    transform.rotation.z,
                    transform.rotation.w
                );
            }
            return rotation;
        }

        private const float _ToleranceForZero = 0.0001f;
        private static bool IsFloatZero(float x)
        {
            if (Math.Abs(x) < _ToleranceForZero)
            {
                return true;
            }

            return false;
        }

        // Generates a mesh
        private static Mesh GenerateMeshAsset(List<MeshData> meshData)
        {
            var newMesh = new Mesh();
            newMesh.indexFormat = IndexFormat.UInt32;
            newMesh.name = "Mesh";

            var count = 0;
            var combineInstances = new CombineInstance[meshData.Count];
            var meshes = new Mesh[meshData.Count];
            var matrices = new Matrix4x4[meshData.Count];

            foreach (var mesh in meshData)
            {
                var meshCopy = UnityEngine.Object.Instantiate(mesh.GeneratedMesh);
                meshCopy.name = $"{mesh.NodeIdentifier}";
                meshes[count] = meshCopy;

                var trs = Matrix4x4.TRS
                (
                    mesh.TranslationToTarget,
                    mesh.RotationToTarget,
                    Vector3.one
                );

                // GSB meshes are in NAR space, convert to Unity space
                var convertedTRS = trs.FromArdkToUnity();
                matrices[count] = convertedTRS;

                var combineInstance = new CombineInstance()
                {
                    mesh = meshes[count], transform = matrices[count]
                };

                combineInstances[count] = combineInstance;
                count++;
            }

            // Do not merge subMeshes to preserve texturing, use matrices for transform
            newMesh.CombineMeshes(combineInstances, false, true);
            newMesh.subMeshCount = combineInstances.Length;

            return newMesh;
        }

        private static LocationData ParseLocationData(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            {
                using (var reader = new StreamReader(stream))
                {
                    var anchorFileText = reader.ReadToEnd();
                    var locationData = JsonUtility.FromJson<LocationData>(anchorFileText);

                    // For backwards compatibility, if there is no DefaultAnchorPayload but an AnchorPayload,
                    //  copy over the data
                    if (string.IsNullOrEmpty(locationData.DefaultAnchorPayload) &&
                        !string.IsNullOrEmpty(locationData.AnchorPayload))
                    {
                        locationData.DefaultAnchorPayload = locationData.AnchorPayload;
                    }

                    return locationData;
                }
            }
        }

        private static bool _isImportingMesh;

        private static UnityEngine.Mesh ImportMesh(ZipArchiveEntry entry, string name)
        {
            var absPath = _ProjectBrowserUtilities.BuildAssetPath(name, Application.dataPath);
            var assetPath = FileUtil.GetProjectRelativePath(absPath);

            using (var stream = entry.Open())
                using (var fs = new FileStream(assetPath, FileMode.OpenOrCreate))
                    stream.CopyTo(fs);

            _isImportingMesh = true;
            AssetDatabase.ImportAsset(assetPath);
            _isImportingMesh = false;

            return AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(assetPath);
        }

        private static bool _isImportingTex;

        private static Texture2D ImportTexture(ZipArchiveEntry entry)
        {
            var absPath = _ProjectBrowserUtilities.BuildAssetPath(entry.Name, Application.dataPath);
            var assetPath = FileUtil.GetProjectRelativePath(absPath);

            using (var stream = entry.Open())
            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    var data = ms.ToArray();
                    File.WriteAllBytes(assetPath, data);
                }
            }

            _isImportingTex = true;
            AssetDatabase.ImportAsset(assetPath);
            _isImportingTex = false;

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private void OnPreprocessTexture()
        {
            if (!_isImportingTex)
                return;

            var textureImporter = assetImporter as TextureImporter;
            textureImporter.isReadable = true; // Unity takes care of resetting this value
        }

        private void OnPreprocessModel()
        {
            if (!_isImportingMesh)
                return;

            var modelImporter = assetImporter as ModelImporter;
            modelImporter.bakeAxisConversion = true;
        }

        private static ARLocationManifest CreateManifest(LocationData locationData, string assetPath)
        {
            var manifest = ScriptableObject.CreateInstance<ARLocationManifest>();
            manifest.NodeIdentifier = locationData.NodeIdentifier;
            manifest.MeshOriginAnchorPayload = locationData.DefaultAnchorPayload;
            manifest.LocalizationTargetId = locationData.LocalizationTargetID;
            manifest.LocationName = Path.GetFileNameWithoutExtension(assetPath);

            AssetDatabase.CreateAsset(manifest, assetPath);

            return manifest;
        }

        private static void CleanupCreatedAssets
        (
            string zipPath,
            List<MeshData> generatedMeshes,
            bool deleteZip = true
        )
        {
            // Cleanup
            if (File.Exists(zipPath) && deleteZip)
            {
                AssetDatabase.DeleteAsset(zipPath);
            }

            foreach (var mesh in generatedMeshes)
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(mesh.GeneratedMesh));
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(mesh.Texture));
            }
        }
    }
}
