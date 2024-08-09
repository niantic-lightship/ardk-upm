// Copyright 2022-2024 Niantic.

using System;
using System.Threading;
using System.Threading.Tasks;

using Draco;
using Niantic.Lightship.AR.PersistentAnchors;

using UnityEngine;
using UnityEngine.Serialization;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// MonoBehaviour that manages the downloading and rendering of location meshes
    /// </summary>
    public class LocationMeshManager : MonoBehaviour
    {
        // The material to use for vertex colored meshes
        // Use the provided vertex shader to render the meshes with colored vertices,
        //   or use a custom shader to render the meshes with a custom material
        [SerializeField]
        private Material _vertexColorMaterial;

        // The material to use for textured meshes
        [SerializeField]
        [Tooltip("Specify a material to use for textured meshes, or leave empty to use the `Standard` material")]
        private Material _texturedMeshMaterial;

        private bool isUpdatingMesh;
        private DracoMeshLoader Draco;
        private CancellationTokenSource _cancellationTokenSource;

        private void Start()
        {
            if (_texturedMeshMaterial == null)
            {
                _texturedMeshMaterial = new Material(Shader.Find("Standard"));

                // If the material is not found, log an error
                if (_texturedMeshMaterial == null)
                {
                    // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                    Debug.LogError
                    (
                        "LocationMeshManager: Failed to load the standard shader, please provide a textured mesh material",
                        this
                    );
                }
            }

            if (_vertexColorMaterial == null)
            {
                var tryGetShader = Shader.Find("Custom/VertexColor");
                if (tryGetShader != null)
                {
                    _vertexColorMaterial = new Material(tryGetShader);
                }

                // If the material is not found, log an error
                if (_vertexColorMaterial == null)
                {
                    // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                    Debug.LogError
                    (
                        "LocationMeshManager: Failed to find the vertex color shader, please provide a vertex color material",
                        this
                    );
                }
            }
        }

        private void OnEnable()
        {
            Draco ??= new DracoMeshLoader();
            _cancellationTokenSource = new CancellationTokenSource();

            isUpdatingMesh = false;
        }

        private void OnDisable()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Get the mesh for an anchor
        /// </summary>
        public async Task<GameObject> GetLocationMeshForAnchorAsync
        (
            ARPersistentAnchor anchor,
            ulong maxDownloadSizeKb = 0,
            bool addCollider = false,
            MeshDownloadRequestResponse.MeshAlgorithm meshFormat =
                MeshDownloadRequestResponse.MeshAlgorithm.VERTEX_COLORED,
            bool cancelOnDisable = true
        )
        {
            var payload = Convert.ToBase64String(anchor.GetDataAsBytes());
            var nodeId = ARPersistentAnchorDeserializationUtility
                .GetOriginNodeIdAssociatedWithPayload(payload);

            return await GenerateLocationMeshAsync
            (
                nodeId,
                maxDownloadSizeKb,
                addCollider,
                meshFormat,
                cancelOnDisable
            );
        }

        /// <summary>
        /// Get the mesh for an anchor payload
        /// </summary>
        public async Task<GameObject> GetLocationMeshForPayloadAsync
        (
            string payload,
            ulong maxDownloadSizeKb = 0,
            bool addCollider = false,
            MeshDownloadRequestResponse.MeshAlgorithm meshFormat =
                MeshDownloadRequestResponse.MeshAlgorithm.VERTEX_COLORED,
            bool cancelOnDisable = true
        )
        {
            var nodeId = ARPersistentAnchorDeserializationUtility
                .GetOriginNodeIdAssociatedWithPayload(payload);

            return await GetLocationMeshForNodeAsync
            (
                nodeId,
                maxDownloadSizeKb,
                addCollider,
                meshFormat,
                cancelOnDisable
            );
        }

        /// <summary>
        /// Query if the manager is currently downloading or building a mesh
        /// </summary>
        public bool IsUpdatingMesh
        {
            get => isUpdatingMesh;
        }

        /// <summary>
        /// Get or set the material to use for textured meshes
        /// A custom material can be provided to render the meshes with a custom shader
        /// </summary>
        public Material TexturedMeshMaterial
        {
            get => _texturedMeshMaterial;
            set
            {
                if (IsUpdatingMesh)
                {
                    // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                    Debug.LogWarning
                    (
                        "The textured mesh material is changed while a mesh is being built, " +
                        "this may cause unexpected behavior or visual artifacts"
                    );
                }

                _texturedMeshMaterial = value;
            }
        }

        /// <summary>
        /// Get or set the material to use for vertex colored meshes
        /// A custom material can be provided to render the meshes with a custom shader
        /// </summary>
        public Material VertexColorMaterial
        {
            get => _vertexColorMaterial;
            set
            {
                if (IsUpdatingMesh)
                {
                    // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                    Debug.LogWarning
                    (
                        "The vertex color material is changed while a mesh is being built, " +
                        "this may cause unexpected behavior or visual artifacts"
                    );
                }

                _vertexColorMaterial = value;
            }
        }

        /// <summary>
        /// Get the mesh for a node
        /// </summary>
        internal async Task<GameObject> GetLocationMeshForNodeAsync
        (
            string nodeId,
            ulong maxDownloadSizeKb = 0,
            bool addCollider = false,
            MeshDownloadRequestResponse.MeshAlgorithm meshFormat =
                MeshDownloadRequestResponse.MeshAlgorithm.VERTEX_COLORED,
            bool cancelOnDisable = true
        )
        {
            return await GenerateLocationMeshAsync
            (
                nodeId,
                maxDownloadSizeKb,
                addCollider,
                meshFormat,
                cancelOnDisable
            );
        }

        // Generates a mesh for the specified nodeId
        // @note This creates a new GameObject, it is up to the caller to clean this object up correctly
        // This is a utility method, it is not recommended to call this method directly
        // This creates a new GameObject, it is up to the caller to clean this object up correctly
        // The top level GameObject is named "nodeId-meshFormat", with a child GameObject named "MeshContainer"
        // The MeshContainer is transformed to position the mesh in Unity space and should not be moved, use the top level
        //   GameObject to move the mesh in Unity space
        private async Task<GameObject> GenerateLocationMeshAsync
        (
            string nodeId,
            ulong maxDownloadSizeKb = 0,
            bool addCollider = false,
            MeshDownloadRequestResponse.MeshAlgorithm meshFormat =
                MeshDownloadRequestResponse.MeshAlgorithm.VERTEX_COLORED,
            bool cancelOnDisable = true
        )
        {
            // Create a cancellation token to cancel the download if the manager is disabled
            CancellationToken cancellationToken = default;
            if (cancelOnDisable)
            {
                if (_cancellationTokenSource == null)
                {
                    // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                    Debug.LogWarning
                    (
                        "Trying to cancel on disable without a valid cancellation token " +
                        "source, mesh download will not be canceled on disable"
                    );
                }
                else
                {
                    cancellationToken = _cancellationTokenSource.Token;
                }
            }

            // Create gameobjects to hold and position the mesh
            var newMeshGo = new GameObject($"{nodeId}-{meshFormat}");
            var meshContainer = new GameObject("MeshContainer");

            // Disable the mesh container until all meshes are loaded to prevent pop-in
            meshContainer.SetActive(false);
            meshContainer.transform.SetParent(newMeshGo.transform, false);

            // Download and build the mesh
            var success = await TryDownloadLocationMeshAsync
            (
                meshContainer.transform,
                nodeId,
                false,
                maxDownloadSizeKb,
                addCollider,
                meshFormat,
                cancellationToken
            );

            // If the mesh was not successfully downloaded and built, destroy the new gameobject
            if (!success)
            {
                if (Application.isEditor)
                {
                    DestroyImmediate(newMeshGo);
                    // For editor, force an immediate unload to free mesh memory.
                    // For device, this needs to be handled manually
                    Resources.UnloadUnusedAssets();
                }
                else
                {
                    Destroy(newMeshGo);
                }

                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                Debug.Log("Mesh download canceled before return, destroying object");
                if (Application.isEditor)
                {
                    DestroyImmediate(newMeshGo);
                    // For editor, force an immediate unload to free mesh memory.
                    // For device, this needs to be handled manually
                    Resources.UnloadUnusedAssets();
                }
                else
                {
                    Destroy(newMeshGo);
                }

                return null;
            }

            return newMeshGo;
        }

        // Utility method to download and build the mesh for the specified nodeId under the provided root transform
        // This is a utility method, it is not recommended to call this method directly
        private async Task<bool> TryDownloadLocationMeshAsync
        (
            Transform root,
            string nodeId,
            bool plotGraph = false,
            ulong maxDownloadSizeKb = 0,
            bool addCollider = false,
            MeshDownloadRequestResponse.MeshAlgorithm meshFormat =
                MeshDownloadRequestResponse.MeshAlgorithm.VERTEX_COLORED,
            CancellationToken cancellationToken = default
        )
        {
            isUpdatingMesh = true;

            // Get the mesh urls of all meshes in the space for the specified nodeId
            // If requesting a textured mesh, the textureUrl will also be populated
            var nodesToLoad = await MeshDownloadHelper.GetMeshUrlsForNode
            (
                nodeId,
                maxDownloadSizeKb,
                meshFormat,
                cancellationToken
            );

            if (cancellationToken.IsCancellationRequested)
            {
                isUpdatingMesh = false;
                return false;
            }

            // Validate the returned list
            if (nodesToLoad != null && nodesToLoad.Count > 0)
            {
                // Create a space holder for the meshes
                string spaceId = nodesToLoad[0].spaceId + "_space";

                if (!root)
                {
                    // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                    Debug.Log("Root transform is null, cannot create mesh holder");
                    isUpdatingMesh = false;
                    return false;
                }

                var targetSpace = new GameObject(spaceId);
                targetSpace.transform.SetParent(root, false);

                var identity = Matrix4x4.identity;
                var toUnitySpace = FromServiceToUnity(identity);

                targetSpace.transform.localPosition = toUnitySpace.GetPosition();
                targetSpace.transform.localRotation = MatrixUtils.RotationFromMatrix(toUnitySpace);
                targetSpace.transform.localScale = new Vector3(1, 1, 1);

                var localizedNodeMatrix4x4 = Matrix4x4.identity;

                foreach (var nodeToLoad in nodesToLoad)
                {
                    // Use the provided node as the origin of the downloaded mesh
                    if (nodeToLoad.nodeId == nodeId)
                    {
                        localizedNodeMatrix4x4 = FromServiceToUnity
                        (
                            Matrix4x4.TRS
                            (
                                nodeToLoad.position,
                                nodeToLoad.rotation,
                                new Vector3(1.0f, 1.0f, 1.0f)
                            )
                        );
                    }
                }

                var allMeshesSucceeded = true;
                // For each mesh, download and build the mesh
                foreach (var nodeToLoad in nodesToLoad)
                {
                    // Default draw position and rotation to the origin node
                    Vector3 drawPosition = toUnitySpace.GetPosition();
                    Quaternion drawRotation = MatrixUtils.RotationFromMatrix(toUnitySpace);

                    // If the node is not the origin node, calculate the draw position and rotation
                    if (nodeToLoad.nodeId != nodeId)
                    {
                        var nodeToLoadMatrix4X4 = FromServiceToUnity
                        (
                            Matrix4x4.TRS
                            (
                                nodeToLoad.position,
                                nodeToLoad.rotation,
                                new Vector3(1.0f, 1.0f, 1.0f)
                            )
                        );

                        var drawMMatrix4X4 = localizedNodeMatrix4x4.inverse * nodeToLoadMatrix4X4;

                        drawPosition = drawMMatrix4X4.GetPosition();
                        drawRotation = MatrixUtils.RotationFromMatrix(drawMMatrix4X4);
                    }

                    // Try to generate the mesh for the node
                    var success = await TryDownloadAndBuildSingleMeshAsync
                    (
                        targetSpace.transform,
                        drawPosition,
                        drawRotation,
                        nodeToLoad.meshUrl,
                        nodeToLoad.nodeId,
                        addCollider,
                        nodeToLoad.textureUrl,
                        cancellationToken
                    );

                    // Check if canceled after each mesh operation, GenerateLocationMeshAsync will
                    //  destroy the holder gameobject and all meshes if canceled
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                        Debug.Log("Mesh download canceled before return, destroying object");
                        isUpdatingMesh = false;
                        return false;
                    }

                    // If the mesh was not successfully downloaded and built, set allMeshesSucceeded to false
                    if (!success)
                    {
                        allMeshesSucceeded = false;
                    }
                }

                if (!allMeshesSucceeded)
                {
                    // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                    // Just log here, one or more of the submeshes failed to download, but this is not fatal
                    Debug.LogWarning("Failed to download all meshes");
                }
            }
            else
            {
                isUpdatingMesh = false;
                return false;
            }

            // Invert the meshcontainer after drawing meshes
            root.transform.localRotation = Quaternion.Euler(180, 180, 0);
            root.gameObject.SetActive(true);
            isUpdatingMesh = false;
            return true;
        }

        // Utility method to download and build a single mesh
        // This is a utility method, it is not recommended to call this method directly
        private async Task<bool> TryDownloadAndBuildSingleMeshAsync
        (
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            string url,
            string nodeId,
            bool addCollider = false,
            string textureUrl = null,
            CancellationToken cancellationToken = default
        )
        {
            // Download the draco mesh
            var meshBytes = await MeshDownloadHelper.DownloadMeshFromSignedUrl(url);

            // Early exit if the download was canceled, don't do any more downloads or work
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            Material mat = null;
            if (!string.IsNullOrEmpty(textureUrl))
            {
                var textureBytes = await MeshDownloadHelper.DownloadMeshFromSignedUrl(textureUrl);
                // Early exit if the download was canceled, don't do any more downloads or work
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                texture.LoadImage(textureBytes);
                FlipTextureVerticallyInPlace(texture);
                mat = new Material(_texturedMeshMaterial);
                mat.mainTexture = texture;
            }

            // Call the draco library to build the mesh
            var generatedMesh = await BuildDracoMeshAsync
                (meshBytes, position, rotation, nodeId, parent, addCollider, mat);

            // This can be cast to "return generatedMesh" but is confusing because the mesh is a GameObject
            if (generatedMesh)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // Use draco to build a mesh from the provided byte[] and position/rotation
        private async Task<GameObject> BuildDracoMeshAsync
        (
            byte[] meshBytes,
            Vector3 position,
            Quaternion rotation,
            string nodeId,
            Transform parent = null,
            bool addCollider = false,
            Material mat = null
        )
        {
            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var result = await DracoDecoder.DecodeMesh(meshDataArray[0], meshBytes);

            if (result.success)
            {
                var go = new GameObject();
                if (parent)
                {
                    go.transform.SetParent(parent, false);
                }

                Mesh mesh = new Mesh();

                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();

                var meshFilter = go.AddComponent<MeshFilter>();
                var meshRenderer = go.AddComponent<MeshRenderer>();

                meshFilter.mesh = mesh;
                if (mat != null)
                {
                    meshRenderer.material = mat;
                }
                else
                {
                    meshRenderer.material = _vertexColorMaterial;
                }

                if (addCollider)
                {
                    var newCollider = go.AddComponent<MeshCollider>();
                    newCollider.sharedMesh = mesh;
                }

                go.transform.localPosition = position;
                go.transform.localRotation = rotation;
                go.transform.localScale = new Vector3(1, 1, 1);

                go.name = nodeId;

                return go;
            }

            return null;
        }

        // Coordinate space conversion utilities
        private static readonly Matrix4x4 s_signedXCordinateMatrix4x4 =
            new Matrix4x4
            (
                new Vector4(-1, 0, 0, 0),
                new Vector4(0, 1, 0, 0),
                new Vector4(0, 0, 1, 0),
                new Vector4(0, 0, 0, 1)
            );

        private static Matrix4x4 FromServiceToUnity(Matrix4x4 matrix)
        {
            // Sx [R|T] Sx
            //    [0|1]
            return s_signedXCordinateMatrix4x4 * matrix * s_signedXCordinateMatrix4x4;
        }

        private static Matrix4x4 FromUnityToService(Matrix4x4 matrix)
        {
            return FromServiceToUnity(matrix);
        }

        private void FlipTextureVerticallyInPlace(Texture2D original)
        {
            var originalPixels = original.GetPixels();

            var newPixels = new Color[originalPixels.Length];

            var width = original.width;
            var height = original.height;

            for (var i = 0; i < width; i++)
            {
                for (var j = 0; j < height; j++)
                {
                    newPixels[i + j * width] = originalPixels[i + (height - j - 1) * width];
                }
            }

            original.SetPixels(newPixels);
            original.Apply();
        }
    }
}
