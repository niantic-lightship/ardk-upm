// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.UnityAssets;
using UnityEditor;
using UnityEditor.Build;

using UnityEngine;
using UnityEngine.Rendering;

namespace Niantic.Lightship.AR.Editor
{
    internal class ARLocationAssetProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets
        (
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            if (importedAssets.Length == 0)
            {
                return;
            }

            var zips = importedAssets.Where
                    (a => string.Equals(Path.GetExtension(a), ".zip"))
                .ToArray();

            if (zips.Length == 0)
            {
                return;
            }

            // Have to delay it a frame in order for all imports to work synchronously
            EditorApplication.delayCall += () => ProcessAllImports(zips);
        }

        private static void ProcessAllImports(string[] zips)
        {
            var allManifests = new List<ARLocationManifest>();
            foreach (var path in zips)
            {
                if (TryCreateLocationManifest(path, out ARLocationManifest manifest))
                {
                    allManifests.Add(manifest);
                }
                else
                {
                    break;
                }
            }

            if (allManifests.Count == 0)
            {
                return;
            }

            AssetDatabase.SaveAssets();

            foreach (var manifest in allManifests)
            {
                manifest._CreateMockAsset();
            }
        }

        private static string InvalidFileCharacters = "[\"*/:<>?\\|]";

        private static bool TryCreateLocationManifest(string zipPath, out ARLocationManifest manifest)
        {
            manifest = null;

            var isValidZip =
                FindArchivedFiles
                (
                    zipPath,
                    out GsbFileRepresentation.LocationData locationData,
                    out List<GsbFileRepresentation.MeshData> meshDataList,
                    out Quaternion originToDefaultRotation,
                    out Vector3 originToDefaultTranslation
                );

            if (!isValidZip)
            {
                // This was not an GSB zip, don't delete zip
                CleanupCreatedAssets(zipPath, meshDataList, false);
                return false;
            }

            Log.Info("Importing: " + zipPath);

            var dir = Path.GetDirectoryName(zipPath);

            var locationName = locationData.LocalizationTargetName;
            if (string.IsNullOrEmpty(locationName))
            {
                locationName = "Unnamed";
            }
            else
            {
                // Remove characters that Unity does not allow in asset paths
                locationName = Regex.Replace(locationName, InvalidFileCharacters, string.Empty);
                locationName = locationName.Trim();
                if (string.IsNullOrEmpty(locationName))
                {
                    locationName = "Unnamed";
                }
            }

            var manifestPath = ProjectBrowserUtilities.BuildAssetPath
                (locationName + ".asset", dir);

            manifest = CreateManifest(locationData, manifestPath);

            try
            {
                AssetDatabase.StartAssetEditing();

                // Need to create a copy in order to organize as sub-asset of the manifest
                var meshCopy = GenerateMeshAsset(meshDataList, originToDefaultRotation, originToDefaultTranslation);
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

        // GSB zips contain one or more meshes, each with a corresponding texture and edge data
        // The top level mesh is the default mesh of the space, which is the largest mesh
        // All additional meshes will be nested zip files, which contain a mesh, texture, and edge data
        //
        // For example, a fused mesh zip folder looks like:
        // GSB.zip
        //  - Default.fbx
        //  - Default.jpeg
        //  - Default.json
        //  - AdditionalMesh1.zip
        //      - AdditionalMesh1.fbx
        //      - AdditionalMesh1.jpeg
        //      - AdditionalMesh1.json
        //  - AdditionalMesh2.zip
        //      ...
        //
        // The default mesh is not necessarily the origin mesh, so we need to apply the inverse transform
        // to place it at the origin. This information can be found within Default.json, which contains
        // a LocalToSpace field if there is a transform to apply.
        // The inverted transform must be applied to the default mesh, and all additional meshes for localization
        // to work correctly.
        private static bool FindArchivedFiles
        (
            string zipPath,
            out GsbFileRepresentation.LocationData locationData,
            out List<GsbFileRepresentation.MeshData> meshData,
            out Quaternion originToDefaultRotation,
            out Vector3 originToDefaultTranslation
        )
        {
            Texture2D tex = null;
            locationData = new GsbFileRepresentation.LocationData();
            meshData = new List<GsbFileRepresentation.MeshData>();
            originToDefaultRotation = Quaternion.identity;
            originToDefaultTranslation = Vector3.zero;

            var assetTargetDir = Path.GetDirectoryName(zipPath);
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

                    var mesh = ImportMesh(meshEntries.First(), "ARLocationMesh.fbx", assetTargetDir);
                    locationData = ParseLocationData(locationEntries.First());

                    // Some nodes do not have textures
                    if (texEntries.Any())
                    {
                        var buildTarget = EditorUserBuildSettings.activeBuildTarget;
                        if (buildTarget != BuildTarget.Android && buildTarget != BuildTarget.iOS)
                        {
                            Debug.LogError
                            (
                                "The current editor build target is not Android or iOS. " +
                                "Textures may be compressed in a manner that cannot be built to Android or iOS devices. " +
                                "Change the build target to iOS or Android and reimport the zip if you intend to " +
                                "include ARLocationManifest meshes on these platforms"
                            );
                        }

                        tex = ImportTexture(texEntries.First(), assetTargetDir);
                    }

                    var initialMeshData = new GsbFileRepresentation.MeshData()
                    {
                        GeneratedMesh = mesh,
                        NodeIdentifier = locationData.NodeIdentifier,
                        RotationToTarget = RotationFromEdge(locationData.LocalToSpace),
                        TranslationToTarget = TranslationFromEdge(locationData.LocalToSpace),
                        Texture = tex
                    };

                    // If the default mesh is not the origin mesh, invert the transform to make it the origin
                    // This is the inverse of the default -> origin transform
                    if (!string.IsNullOrEmpty(locationData.LocalToSpace.destination))
                    {
                        // Doing SO3 transform inverse
                        var defaultToOriginRotation = initialMeshData.RotationToTarget;
                        var defaultToOriginTranslation = initialMeshData.TranslationToTarget;
                        originToDefaultRotation = Quaternion.Inverse(defaultToOriginRotation);
                        originToDefaultTranslation = originToDefaultRotation * defaultToOriginTranslation * -1;
                    }

                    meshData = UnpackAdditionalMeshes(additionalMeshes, assetTargetDir);
                    meshData.Insert(0, initialMeshData);

                    return !(meshData.Count == 0 ||
                        string.IsNullOrEmpty(locationData.DefaultAnchorPayload));
                }
            }
        }

        private static List<GsbFileRepresentation.MeshData> UnpackAdditionalMeshes(IEnumerable<ZipArchiveEntry> zippedEntries, string targetDir)
        {
            var ret = new List<GsbFileRepresentation.MeshData>();

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
                    if (!textureEntries.Any())
                    {
                        // Skip submeshes without textures
                        continue;
                    }

                    var texture = ImportTexture(textureEntries.First(), targetDir);

                    var mesh = meshEntries.First();
                    var strippedName = mesh.Name.Split('.')[0];
                    var imported = ImportMesh(mesh, $"ARTempMesh{strippedName}.fbx", targetDir);

                    var meshData = new GsbFileRepresentation.MeshData()
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

        private static Vector3 TranslationFromEdge(GsbFileRepresentation.EdgeRepresentation edgeData)
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

        private static Quaternion RotationFromEdge(GsbFileRepresentation.EdgeRepresentation edgeData)
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

        // Generates a mesh from the given list of meshes
        // The mesh transforms provided in the GSB zip are in the origin mesh's space, so we need to apply
        // an additional origin to default transform
        private static Mesh GenerateMeshAsset
        (
            List<GsbFileRepresentation.MeshData> meshData,
            Quaternion originToDefaultRotation,
            Vector3 originToDefaultTranslation
        )
        {
            var newMesh = new Mesh();
            newMesh.indexFormat = IndexFormat.UInt32;
            newMesh.name = "Mesh";

            var count = 0;
            var combineInstances = new CombineInstance[meshData.Count];
            var meshes = new Mesh[meshData.Count];
            var matrices = new Matrix4x4[meshData.Count];

            // Apply the origin to default transform to all meshes
            var originToDefault = Matrix4x4.TRS
            (
                originToDefaultTranslation,
                originToDefaultRotation,
                Vector3.one
            );

            foreach (var mesh in meshData)
            {
                var meshCopy = UnityEngine.Object.Instantiate(mesh.GeneratedMesh);
                meshCopy.name = $"{mesh.NodeIdentifier}";
                meshes[count] = meshCopy;

                // This is the original transform that must be applied to each mesh to place it
                // in the origin mesh's space
                var meshToOrigin = Matrix4x4.TRS
                (
                    mesh.TranslationToTarget,
                    mesh.RotationToTarget,
                    Vector3.one
                );

                // Apply the origin to default transform to each mesh to place it in the default space
                var meshToDefault =  originToDefault * meshToOrigin;

                // GSB meshes are in NAR space, convert to Unity space
                var convertedTRS = meshToDefault.FromArdkToUnity();
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

        private static GsbFileRepresentation.LocationData ParseLocationData(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            {
                using (var reader = new StreamReader(stream))
                {
                    var anchorFileText = reader.ReadToEnd();
                    var locationData = JsonUtility.FromJson<GsbFileRepresentation.LocationData>(anchorFileText);

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

        private static UnityEngine.Mesh ImportMesh(ZipArchiveEntry entry, string name, string targetDir)
        {
            var assetPath = ProjectBrowserUtilities.BuildAssetPath(name, targetDir);

            using (var stream = entry.Open())
                using (var fs = new FileStream(assetPath, FileMode.OpenOrCreate))
                    stream.CopyTo(fs);

            _isImportingMesh = true;
            AssetDatabase.ImportAsset(assetPath);
            _isImportingMesh = false;

            return AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(assetPath);
        }

        private static bool _isImportingTex;

        private static Texture2D ImportTexture(ZipArchiveEntry entry, string targetDir)
        {
            var assetPath = ProjectBrowserUtilities.BuildAssetPath(entry.Name, targetDir);

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
            {
                return;
            }

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

        private static ARLocationManifest CreateManifest(GsbFileRepresentation.LocationData locationData, string assetPath)
        {
            var manifest = ScriptableObject.CreateInstance<ARLocationManifest>();
            manifest.NodeIdentifier = locationData.NodeIdentifier;
            manifest.MeshOriginAnchorPayload = locationData.DefaultAnchorPayload;
            manifest.LocalizationTargetId = locationData.LocalizationTargetID;
            manifest.LocationName = Path.GetFileNameWithoutExtension(assetPath);
            manifest.LocationLatitude = locationData.GpsLocation.latitude;
            manifest.LocationLongitude = locationData.GpsLocation.longitude;

            AssetDatabase.CreateAsset(manifest, assetPath);

            return manifest;
        }

        private static void CleanupCreatedAssets
        (
            string zipPath,
            List<GsbFileRepresentation.MeshData> generatedMeshes,
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
