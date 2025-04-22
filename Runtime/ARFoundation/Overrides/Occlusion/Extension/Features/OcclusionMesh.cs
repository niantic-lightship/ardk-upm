// Copyright 2022-2025 Niantic.

using System;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Occlusion.Features
{
    /// <summary>
    /// Implements an occlusion technique that renders an invisible occluder geometry.
    /// </summary>
    internal sealed class OcclusionMesh : OcclusionComponent
    {
        /// <summary>
        /// The shader required for the OcclusionMesh feature.
        /// </summary>
        public const string RequiredShaderName = "Lightship/OcclusionMesh";

        // Components
        private XRCameraSubsystem _cameraSubsystem;
        private LightshipOcclusionSubsystem _lightshipOcclusionSubsystem;
        private LightshipPlaybackOcclusionSubsystem _playbackOcclusionSubsystem;

        // Resources
        private Mesh _mesh;

        // Image matrices
        private Matrix4x4 _intrinsics;
        private Matrix4x4 _extrinsics;

        // Helpers
        private float _cameraForwardScale;
        private Vector2Int _meshResolution;
        private Transform _cameraOffset;
        private bool _silenceDepthTextureWarning;

        /// <summary>
        /// The mesh that represents the occlusion geometry.
        /// </summary>
        public Mesh Mesh => GPUDepth == null ? null : GetOrCreateMesh(GPUDepth.width, GPUDepth.height);

        public new void Configure(XROcclusionSubsystem occlusionSubsystem) =>
            throw new InvalidOperationException(
                "Use the Configure(XROcclusionSubsystem, XRCameraSubsystem) method instead.");

        public void Configure(XROcclusionSubsystem occlusionSubsystem, XRCameraSubsystem cameraSubsystem)
        {
            base.Configure(occlusionSubsystem);

            // Cache subsytems
            _cameraSubsystem = cameraSubsystem;
            _lightshipOcclusionSubsystem = occlusionSubsystem as LightshipOcclusionSubsystem;
            _playbackOcclusionSubsystem = occlusionSubsystem as LightshipPlaybackOcclusionSubsystem;

            // Find the camera offset
            var xrOrigin = UnityEngine.Object.FindObjectOfType<XROrigin>();
            if (xrOrigin != null)
            {
                _cameraOffset = xrOrigin.CameraFloorOffsetObject.transform;
            }
        }

        protected override void OnReleaseResources()
        {
            base.OnReleaseResources();

            if (_mesh != null)
            {
                UnityEngine.Object.Destroy(_mesh);
                _mesh = null;
            }
        }

        protected override void OnUpdate(Camera camera)
        {
            base.OnUpdate(camera);

            // Set scale: this computes the affect the camera's localToWorld has on the length of the
            // forward vector, i.e., how much farther from the camera are things than with unit scale.
            var forward = camera.transform.localToWorldMatrix.GetColumn(2);
            _cameraForwardScale = forward.magnitude;

            // Get the depth texture
            var texture = GPUDepth;

            // Do nothing if the depth texture is not available
            if (texture == null)
            {
                if (!_silenceDepthTextureWarning)
                {
                    Log.Info("OcclusionMesh: No depth texture available.");
                    _silenceDepthTextureWarning = true;
                }
                return;
            }
            _silenceDepthTextureWarning = false;

            if (_lightshipOcclusionSubsystem != null)
            {
                _intrinsics = _lightshipOcclusionSubsystem.LatestIntrinsicsMatrix ?? Matrix4x4.identity;
                _extrinsics = _lightshipOcclusionSubsystem.LatestExtrinsicsMatrix ?? Matrix4x4.identity;
            }
            else if (_playbackOcclusionSubsystem != null)
            {
                _intrinsics = _playbackOcclusionSubsystem.LatestIntrinsicsMatrix ?? Matrix4x4.identity;
                _extrinsics = _playbackOcclusionSubsystem.LatestExtrinsicsMatrix ?? Matrix4x4.identity;
            }
            else if (OcclusionSubsystem != null && _cameraSubsystem != null)
            {
                // In case of running platform depth, calculate the matrices from the camera
                _intrinsics = Matrix4x4.identity;
                if (_cameraSubsystem.TryGetIntrinsics(out var intrinsicsData))
                {
                    var res = new Vector2Int(texture.width, texture.height);
                    _intrinsics[0, 0] = intrinsicsData.focalLength.x / intrinsicsData.resolution.x * res.x;
                    _intrinsics[1, 1] = intrinsicsData.focalLength.y / intrinsicsData.resolution.y * res.y;
                    _intrinsics[0, 2] = intrinsicsData.principalPoint.x / intrinsicsData.resolution.x * res.x;
                    _intrinsics[1, 2] = intrinsicsData.principalPoint.y / intrinsicsData.resolution.y * res.y;
                }

                var displayRotation = CameraMath.CameraToDisplayRotation(XRDisplayContext.GetScreenOrientation());
                _extrinsics = camera.transform.localToWorldMatrix * Matrix4x4.Rotate(displayRotation);
            }
            else
            {
                Log.Warning("OcclusionMesh: Could not acquire image intrinsics. " +
                    "No occlusion subsystem or camera subsystem found.");
            }

            // Apply the camera offset to the extrinsics
            if (_cameraOffset != null)
            {
                _extrinsics = _cameraOffset.localToWorldMatrix * _extrinsics;
            }
        }

        protected override void OnMaterialUpdate(Material mat)
        {
            base.OnMaterialUpdate(mat);

            var texture = GPUDepth;
            if (texture == null)
            {
                return;
            }

            var width = texture.width;
            var height = texture.height;

            // Update the material properties
            mat.SetInt(ShaderProperties.ImageWidthId, width);
            mat.SetInt(ShaderProperties.ImageHeightId, height);
            mat.SetTexture(ShaderProperties.DepthTextureId, texture);
            mat.SetMatrix(ShaderProperties.ExtrinsicsId, _extrinsics);
            mat.SetVector(ShaderProperties.IntrinsicsId,
                new Vector4(_intrinsics.m00, _intrinsics.m11, _intrinsics.m02, _intrinsics.m12));
            mat.SetFloat(ShaderProperties.CameraForwardScaleId, _cameraForwardScale);
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
                    UnityEngine.Object.Destroy(_mesh);
                }

                _meshResolution = new Vector2Int(width, height);
                _mesh = OcclusionExtensionUtils.CreateGeometry(width, height);
            }

            return _mesh;
        }
    }
}
