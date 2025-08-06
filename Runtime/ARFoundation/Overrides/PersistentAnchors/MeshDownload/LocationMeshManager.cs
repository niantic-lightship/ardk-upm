// Copyright 2022-2025 Niantic.

using System;
using System.Threading;
using System.Threading.Tasks;

using Niantic.Lightship.AR.Core;

using UnityEngine;

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

        private CancellationTokenSource _cancellationTokenSource;
        private MeshDownloadClient _meshDownloadClient;

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

            var unityContextHandle = LightshipUnityContext.UnityContextHandle;
            var ardkHandle = LightshipUnityContext.GetARDKHandle(unityContextHandle);
            _meshDownloadClient = new MeshDownloadClient(ardkHandle);
        }

        private void OnEnable()
        {
            _cancellationTokenSource = new CancellationTokenSource();
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
        /// Get the mesh for an anchor payload
        /// </summary>
        public async Task<GameObject> GetLocationMeshForPayloadAsync
        (
            string payload,
            ulong maxDownloadSizeKb = 0,
            bool addCollider = false,
            bool getTexture = false,
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
            return await _meshDownloadClient.GenerateLocationMeshAsyncFromPayload(payload, getTexture, addCollider, (uint)maxDownloadSizeKb, _vertexColorMaterial, _texturedMeshMaterial, cancellationToken);
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
                _vertexColorMaterial = value;
            }
        }
    }
}
