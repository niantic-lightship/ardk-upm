// Copyright 2022-2024 Niantic.

using System.Linq;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Subsystems.Semantics;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Semantics
{
    public sealed class LightshipSemanticsOverlay : LightshipPostBackgroundRenderer
    {
        private static readonly int s_semanticsTextureId = Shader.PropertyToID("_Semantics");
        private static readonly int s_samplerMatrixId = Shader.PropertyToID("_SamplerMatrix");
        private static readonly int s_extrinsics = Shader.PropertyToID("_Extrinsics");
        private static readonly int s_intrinsicsId = Shader.PropertyToID("_Intrinsics");
        private static readonly int s_imageWidthId = Shader.PropertyToID("_ImageWidth");
        private static readonly int s_imageHeightId = Shader.PropertyToID("_ImageHeight");
        private static readonly int s_backProjectionDistanceId = Shader.PropertyToID("_BackprojectionDistance");

        /// <summary>
        /// Whether the semantics metadata (such as number of channels) is available.
        /// </summary>
        public bool IsMetadataAvailable => _channelNames != null;

        /// <summary>
        /// The shader name to use for the semantics overlay.
        /// </summary>
        public const string KShaderName = "Lightship/SemanticsOverlay";

        protected override string ShaderName
        {
            get => KShaderName;
        }

        protected override string RendererName
        {
            get => "Lightship Semantics Overlay";
        }

        // Components
        private Camera _camera;
        private LightshipSemanticsSubsystem _semanticsSubsystem;

        // Resources
        private Texture2D _tempTexture;
        private Mesh _mesh;

        // Helpers
        private string[] _channelNames;
        private int _currentChannelIndex = 3;

        /// <summary>
        /// Distance in meters (from the camera) to back-project the image.
        /// </summary>
        public float BackProjectionDistance { get; set; } = 5.0f;

        /// <summary>
        /// The intrinsics matrix of the semantics image.
        /// </summary>
        public XRCameraIntrinsics? Intrinsics { get; private set; } = null;

        /// <summary>
        /// Sets the current channel to display.
        /// </summary>
        public void SetChannel(int index)
        {
            _currentChannelIndex = index;
            ValidateCurrentChannel();
        }

        /// <summary>
        /// Sets the current channel to display.
        /// </summary>
        public void SetChannel(string channelName)
        {
            if (_channelNames != null)
            {
                var index = System.Array.IndexOf(_channelNames, channelName);
                if (index < 0)
                {
                    Debug.LogError($"Invalid channel name: {channelName}");
                    return;
                }

                _currentChannelIndex = index;
            }
            else
            {
                Debug.LogError("Metadata is not available yet.");
            }
        }

        private void ValidateCurrentChannel()
        {
            if (_channelNames != null)
            {
                if (_currentChannelIndex < 0 || _currentChannelIndex >= _channelNames.Length)
                {
                    Debug.LogError($"Invalid channel index: {_currentChannelIndex}. Changing to default channel.");
                    _currentChannelIndex = 3;
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _camera = Camera.GetComponent<Camera>();
            _mesh = CreateMesh();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_tempTexture != null)
            {
                Destroy(_tempTexture);
            }

            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!TryAcquireSubsystems())
            {
                return;
            }

            if (!_semanticsSubsystem.running)
            {
                return;
            }

            // Cache channel names
            if (!IsMetadataAvailable)
            {
                if (!_semanticsSubsystem.TryGetChannelNames(out var channelNames))
                {
                    return;
                }

                _channelNames = channelNames.ToArray();
                ValidateCurrentChannel();
            }

            // Update the material with the latest semantics data
            if (FetchSemanticsData(
                    _channelNames[_currentChannelIndex],
                    out var texture,
                    out var intrinsics,
                    out var samplerMatrix))
            {
                // Cache intrinsics
                Intrinsics = new XRCameraIntrinsics(
                    new Vector2(intrinsics.m00, intrinsics.m11),
                    new Vector2(intrinsics.m02, intrinsics.m12),
                    new Vector2Int(texture.width, texture.height));

                // Bind the texture and metadata to the material
                Material.SetTexture(s_semanticsTextureId, texture);
                Material.SetInt(s_imageWidthId, texture.width);
                Material.SetInt(s_imageHeightId, texture.height);

                // Bind image intrinsics
                Material.SetVector(s_intrinsicsId,
                    new Vector4(intrinsics.m00, intrinsics.m11, intrinsics.m02, intrinsics.m12));

                // Calculate extrinsics
                var extrinsics =

                    // The inverse view matrix transforms from camera space to world space
                    _camera.cameraToWorldMatrix *

                    // This will rotate the mesh (and the image with it) to match the display orientation
                    Matrix4x4.Rotate(CameraMath.CameraToDisplayRotation(XRDisplayContext.GetScreenOrientation()));

                // Bind camera extrinsics
                Material.SetMatrix(s_extrinsics, extrinsics);
                Material.SetFloat(s_backProjectionDistanceId, BackProjectionDistance);

                // The sampler matrix is assumed to be in image space here (no crop, no rotation, only mirror and warp)
                Material.SetMatrix(s_samplerMatrixId, samplerMatrix);
            }
            else
            {
                Debug.LogWarning($"Failed to fetch data for channel: {_channelNames[_currentChannelIndex]}");
            }
        }

        private bool FetchSemanticsData(string channelName, out Texture2D texture, out Matrix4x4 intrinsicsMatrix, out Matrix4x4 samplerMatrix)
        {
            // Acquire the latest image
            if (_semanticsSubsystem.TryAcquireSemanticChannelCpuImage(
                    channelName: channelName,
                    cameraParams: null, // Viewport should default to image container
                    cpuImage: out var cpuImage,
                    samplerMatrix: out samplerMatrix))
            {
                // Copy the image to a Texture2D
                cpuImage.CreateOrUpdateTexture(ref _tempTexture);
                cpuImage.Dispose();
                texture = _tempTexture;

                var intrinsics = _semanticsSubsystem.LatestIntrinsicsMatrix;
                if (intrinsics.HasValue)
                {
                    intrinsicsMatrix = intrinsics.Value;
                    return true;
                }
            }

            texture = null;
            intrinsicsMatrix = Matrix4x4.identity;
            samplerMatrix = Matrix4x4.identity;
            return false;
        }

        protected override bool ConfigureCommandBuffer(CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();
            commandBuffer.DrawMesh(_mesh, Matrix4x4.identity, Material);
            return true;
        }

        private bool TryAcquireSubsystems()
        {
            if (_semanticsSubsystem != null)
            {
                return true;
            }

            var xrManager = XRGeneralSettings.Instance.Manager;
            if (!xrManager.isInitializationComplete)
            {
                return false;
            }

            _semanticsSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XRSemanticsSubsystem>() as LightshipSemanticsSubsystem;
            if (_semanticsSubsystem == null)
            {
                Debug.LogError
                (
                    "Destroying XRSemanticsDisplay component because " +
                    $"no active {typeof(XRSemanticsSubsystem).FullName} is available. " +
                    "Please ensure that a valid loader configuration exists in the XR project settings."
                );

                Destroy(this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Create a simple quad mesh.
        /// </summary>
        private static Mesh CreateMesh()
        {
            var mesh = new Mesh
            {
                vertices = new Vector3[4],
                uv = new[] {new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)},
                triangles = new[] {2, 1, 0, 3, 2, 0}
            };

            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
