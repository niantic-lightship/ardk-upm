// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// Creates a mesh from available depth data to occlude the view.
    /// </summary>
    [RequireComponent(typeof(AROcclusionManager))]
    public class LightshipOcclusionEffect : LightshipPostBackgroundRenderer
    {
        // Shader property bindings
        private static readonly int s_colorMaskId = Shader.PropertyToID("_ColorMask");
        private static readonly int s_imageWidthId = Shader.PropertyToID("_ImageWidth");
        private static readonly int s_imageHeightId = Shader.PropertyToID("_ImageHeight");
        private static readonly int s_depthTextureId = Shader.PropertyToID("_DepthTexture");
        private static readonly int s_intrinsicsId = Shader.PropertyToID("_Intrinsics");
        private static readonly int s_extrinsicsId = Shader.PropertyToID("_Extrinsics");
        private static readonly int s_cameraForwardScaleId = Shader.PropertyToID("_UnityCameraForwardScale");

        public const string KShaderName = "Lightship/OcclusionEffect";
        protected override string ShaderName
        {
            get => KShaderName;
        }

        protected override string RendererName
        {
            get => "Lightship Occlusion Effect";
        }

        // Error messages
        private const string k_TargetFrameRateNotSupportedMessage =
            "TargetFrameRate is not supported on non-Lightship implementations of the XROcclusionSubsystem.";
        private const string k_TargetFrameRateLessOrEqualToZeroMessage =
            "Target frame rate value must be greater than zero.";

        [SerializeField]
        [Tooltip("Frame rate that depth inference will aim to run at")]
        [Range(1, 90)]
        private uint _targetFrameRate = LightshipOcclusionSubsystem.MaxRecommendedFrameRate;

        [SerializeField]
        private Transform _cameraOffset;

        // Required components
        private XROcclusionSubsystem _occlusionSubsystem;
        private AROcclusionManager _occlusionManager;

        // Resources
        private Mesh _mesh;

        // The depth texture owned by this component, if created
        private Texture2D _tempTexture;

        // Helpers
        private Vector2Int _meshResolution;
        private bool _showedTargetFrameRateNotSupportedMessage;

        private enum ColorMask
        {
            None = 0, // RGBA: 0000
            Depth = 5, // RGBA: 0101
            UV = 11, // RGBA: 1011
            All = 15, // RGBA: 1111
        }
        private ColorMask _colorMask = ColorMask.None;

        /// <summary>
        /// Get or set whether to visualize the depth data.
        /// </summary>
        public bool DebugVisualization
        {
            get => _colorMask != ColorMask.None;
            set
            {
                _colorMask = value ? ColorMask.Depth : ColorMask.None;
            }
        }

        /// <summary>
        /// Get or set the frame rate that depth inference will aim to run at.
        /// </summary>
        public uint TargetFrameRate
        {
            get
            {
                if (_occlusionSubsystem is LightshipOcclusionSubsystem lightshipOcclusionSubsystem)
                {
                    return lightshipOcclusionSubsystem.TargetFrameRate;
                }

                Log.Warning(k_TargetFrameRateNotSupportedMessage);
                return 0;
            }
            set
            {
                if (value <= 0)
                {
                    Log.Error(k_TargetFrameRateLessOrEqualToZeroMessage);
                    return;
                }

                _targetFrameRate = value;
                if (_occlusionSubsystem is LightshipOcclusionSubsystem lightshipOcclusionSubsystem)
                {
                    lightshipOcclusionSubsystem.TargetFrameRate = value;
                }
                else if (!_showedTargetFrameRateNotSupportedMessage)
                {
                    _showedTargetFrameRateNotSupportedMessage = true;
                    Log.Warning(k_TargetFrameRateNotSupportedMessage);
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();

            // Acquire components
            _occlusionManager = GetComponent<AROcclusionManager>();

            // Disable system occlusion
            _occlusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.NoOcclusion;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_mesh != null)
            {
                Destroy(_mesh);
            }

            if (_tempTexture != null)
            {
                Destroy(_tempTexture);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!ValidateOcclusionSubsystem())
            {
                return;
            }

            // Update the depth data
            if (!FetchDepthData(out var depthTexture, out var intrinsics, out var extrinsics))
            {
                return;
            }

            // Update the material properties
            Material.SetFloat(s_colorMaskId, (int)_colorMask);
            Material.SetInt(s_imageWidthId, depthTexture.width);
            Material.SetInt(s_imageHeightId, depthTexture.height);
            Material.SetTexture(s_depthTextureId, depthTexture);
            Material.SetMatrix(s_extrinsicsId, extrinsics);
            Material.SetVector(s_intrinsicsId,
                new Vector4(intrinsics.m00, intrinsics.m11, intrinsics.m02, intrinsics.m12));

            // Set scale: this computes the affect the camera's localToWorld has on the length of the
            // forward vector, i.e. how much farther from the camera are things than with unit scale.
            var forward = transform.localToWorldMatrix.GetColumn(2);
            var scale = forward.magnitude;
            Material.SetFloat(s_cameraForwardScaleId, scale);
        }

        protected override bool ConfigureCommandBuffer(CommandBuffer commandBuffer)
        {
            if (_tempTexture != null)
            {
                commandBuffer.Clear();
                commandBuffer.DrawMesh(
                    GetOrCreateMesh(_tempTexture.width, _tempTexture.height), Matrix4x4.identity, Material);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Grabs the latest depth image and its intrinsics and extrinsics matrices.
        /// </summary>
        /// <returns>Whether depth could be acquired.</returns>
        private bool FetchDepthData(out Texture texture, out Matrix4x4 intrinsics, out Matrix4x4 extrinsics)
        {
            // Using Lightship occlusion subsystem
            if (_occlusionSubsystem is LightshipOcclusionSubsystem lsSubsystem)
            {
                // Try to acquire the full image
                if (lsSubsystem.TryAcquireEnvironmentDepthCpuImage(default, out var image, out _))
                {
                    // Update the depth texture from the cpu image
                    var didUpdateTexture = ImageSamplingUtils.CreateOrUpdateTexture(
                        source: image,
                        destination: ref _tempTexture,
                        destinationFilter: FilterMode.Bilinear,
                        pushToGpu: true
                    );

                    // Release the cpu image
                    image.Dispose();

                    // Assign the texture
                    texture = didUpdateTexture ? _tempTexture : null;

                    // Get the matrices
                    intrinsics = lsSubsystem._LatestIntrinsicsMatrix ?? Matrix4x4.identity;
                    extrinsics = lsSubsystem._LatestExtrinsicsMatrix ?? Matrix4x4.identity;

                    // Apply the camera offset to the extrinsics
                    if (_cameraOffset != null)
                    {
                        extrinsics = _cameraOffset.localToWorldMatrix * extrinsics;
                    }

                    return didUpdateTexture;
                }

                // Using lightship, but failed to acquire the image
                texture = null;
                intrinsics = default;
                extrinsics = default;
                return false;
            }

            // Fall back to the default depth texture
            texture = _occlusionManager.environmentDepthTexture;

            // In case of playback, get the intrinsics matrix from the subsystem
            if (_occlusionSubsystem is LightshipPlaybackOcclusionSubsystem lsPlaybackSubsystem)
            {
                intrinsics = lsPlaybackSubsystem._LatestIntrinsicsMatrix ?? Matrix4x4.identity;
                extrinsics = lsPlaybackSubsystem._LatestExtrinsicsMatrix ?? Matrix4x4.identity;
            }
            else
            {
                // In case of running platform depth, calculate the matrices from the camera
                intrinsics = Matrix4x4.identity;
                if (Camera.subsystem.TryGetIntrinsics(out var intrinsicsData))
                {
                    var res = new Vector2Int(texture.width, texture.height);
                    intrinsics[0, 0] = intrinsicsData.focalLength.x / intrinsicsData.resolution.x * res.x;
                    intrinsics[1, 1] = intrinsicsData.focalLength.y / intrinsicsData.resolution.y * res.y;
                    intrinsics[0, 2] = intrinsicsData.principalPoint.x / intrinsicsData.resolution.x * res.x;
                    intrinsics[1, 2] = intrinsicsData.principalPoint.y / intrinsicsData.resolution.y * res.y;
                }

                var displayRotation = CameraMath.CameraToDisplayRotation(XRDisplayContext.GetScreenOrientation());
                extrinsics = Camera.transform.localToWorldMatrix * Matrix4x4.Rotate(displayRotation);
            }

            // Apply the camera offset to the extrinsics
            if (_cameraOffset != null)
            {
                extrinsics = _cameraOffset.localToWorldMatrix * extrinsics;
            }

            return texture != null;
        }

        /// <summary>
        /// Acquires whatever XR occlusion subsystem is available.
        /// </summary>
        /// <returns>Whether the occlusion subsystem was successfully acquired.</returns>
        private bool ValidateOcclusionSubsystem()
        {
            if (_occlusionSubsystem != null)
            {
                return true;
            }

            if (XRGeneralSettings.Instance == null ||
                XRGeneralSettings.Instance.Manager == null ||
                XRGeneralSettings.Instance.Manager.activeLoader == null)
            {
                return false;
            }

            var xrManager = XRGeneralSettings.Instance.Manager;
            if (!xrManager.isInitializationComplete)
            {
                return false;
            }

            _occlusionSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XROcclusionSubsystem>();
            if (_occlusionSubsystem == null)
            {
                Log.Warning
                (
                    "Destroying LightshipOcclusionEffect component because " +
                    $"no active {typeof(XROcclusionSubsystem).FullName} is available. " +
                    "Please ensure that a valid loader configuration exists in the XR project settings."
                );

                Destroy(this);
                return false;
            }

            if (_occlusionSubsystem is LightshipOcclusionSubsystem lsSubsystem)
            {
                // Set target frame rate
                lsSubsystem.TargetFrameRate = _targetFrameRate;
            }

            return true;
        }

        /// <summary>
        /// Creates or retrieves the mesh with the specified resolution.
        /// </summary>
        /// <param name="width">Number of vertices on the x-axis.</param>
        /// <param name="height">Number of vertices on the y-axis.</param>
        /// <returns></returns>
        private Mesh GetOrCreateMesh(int width, int height)
        {
            var exists = _mesh != null;
            if (!exists|| _meshResolution.x != width || _meshResolution.y != height)
            {
                if (exists)
                {
                    Destroy(_mesh);
                }

                _meshResolution = new Vector2Int(width, height);
                _mesh = CreateGeometry(width, height);
            }

            return _mesh;
        }

        private static Mesh CreateGeometry(int width, int height)
        {
            var numPoints = width * height;
            var vertices = new Vector3[numPoints];
            var numTriangles = 2 * (width - 1) * (height - 1); // just under 2 triangles per point, total

            // Map vertex indices to triangle in triplets
            var triangleIdx = new int[numTriangles * 3]; // 3 vertices per triangle
            var startIndex = 0;

            for (var i = 0; i < width * height; ++i)
            {
                var h = i / width;
                var w = i % width;
                if (h == height - 1 || w == width - 1)
                {
                    continue;
                }

                // Triangle indices are counter-clockwise to face you
                triangleIdx[startIndex] = i;
                triangleIdx[startIndex + 1] = i + width;
                triangleIdx[startIndex + 2] = i + width + 1;
                triangleIdx[startIndex + 3] = i;
                triangleIdx[startIndex + 4] = i + width + 1;
                triangleIdx[startIndex + 5] = i + 1;
                startIndex += 6;
            }

            var mesh = new Mesh
            {
                indexFormat = width * height >= 65534 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices,
                triangles = triangleIdx
            };
            mesh.UploadMeshData(true);

            return mesh;
        }
    }
}
