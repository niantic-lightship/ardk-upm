// Copyright 2022-2025 Niantic.

using System.Collections.Generic;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Subsystems.Semantics;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Occlusion.Features
{
    /// <summary>
    /// A feature to suppress sections of the occlusion map based on semantic segmentation.
    /// </summary>
    internal sealed class Suppression : RenderComponent
    {
        // Error messages
        private const string KSuppressionTextureErrorMessage = "Unable to update the depth suppresion texture.";
        private const string KMissingSubsystemMessage = "Missing Lightship semantics subsystem reference. ";
        private const string KUndeterminedOcclusionTechniqueMessage = "The occlusion technique is undetermined.";

        /// <summary>
        /// Shader keyword for semantic suppression.
        /// </summary>
        protected override string Keyword
        {
            get => "FEATURE_SUPPRESSION";
        }

        // Required components
        private LightshipSemanticsSubsystem _subsystem;

        // Resources
        private Texture2D _gpuSuppression;
        private HashSet<string> _channels;
        private XROrigin _origin;

        // Helpers
        private LightshipOcclusionExtension.OcclusionTechnique _technique;
        private ScreenOrientation? _lastOrientation;
        private XRCameraParams _viewport;
        private bool _subsystemDirty;

        public bool Configure(
            XROrigin origin,
            LightshipSemanticsSubsystem subsystem,
            LightshipOcclusionExtension.OcclusionTechnique technique,
            IEnumerable<string> suppressionChannels
        )
        {
            // The subsystem is required to fetch the suppression mask
            if (subsystem == null)
            {
                Log.Error(KMissingSubsystemMessage);
                return false;
            }

            // The occlusion technique must be decided before adding this feature
            if (technique == LightshipOcclusionExtension.OcclusionTechnique.Automatic)
            {
                Log.Error(KUndeterminedOcclusionTechniqueMessage);
                return false;
            }

            // Cache the components
            _technique = technique;
            _subsystem = subsystem;
            _origin = origin;

            // Initialize the suppression channels
            _channels = new HashSet<string>(suppressionChannels);
            _subsystemDirty = true;
            return true;
        }

        protected override void OnUpdate(Camera camera)
        {
            base.OnUpdate(camera);
            ValidateSubsystem();
        }

        protected override void OnMaterialUpdate(Material mat)
        {
            base.OnMaterialUpdate(mat);

            // Update the suppression mask texture
            if (FetchSuppressionImage(out var cpuSuppression, out var suppressionTransform))
            {
                // Copy to gpu
                if (ImageSamplingUtils.CreateOrUpdateTexture(cpuSuppression, ref _gpuSuppression))
                {
                    // Bind the gpu image
                    mat.SetTexture(ShaderProperties.SuppressionTextureId, _gpuSuppression);
                    mat.SetMatrix(ShaderProperties.SuppressionTransformId, suppressionTransform);
                }
                else
                {
                    Log.Error(KSuppressionTextureErrorMessage);
                }

                // Release the cpu image
                cpuSuppression.Dispose();
            }
        }

        protected override void OnReleaseResources()
        {
            base.OnReleaseResources();
            if (_gpuSuppression != null)
            {
                Object.Destroy(_gpuSuppression);
            }
        }

        /// <summary>
        /// Adds a suppression channel to the list of channels.
        /// </summary>
        /// <param name="channelName">The name of the channel to add.</param>
        /// <returns>True if the channel was successfully added, false otherwise.</returns>
        internal bool AddChannel(string channelName)
        {
            if (_channels.Add(channelName))
            {
                _subsystemDirty = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes a suppression channel from the list of channels.
        /// </summary>
        /// <param name="channelName">The name of the channel to remove.</param>
        /// <returns>True if the channel was successfully removed, false otherwise.</returns>
        internal bool RemoveChannel(string channelName)
        {
            if (_channels.Remove(channelName))
            {
                _subsystemDirty = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Re-sets the suppression channels on the subsystem if necessary.
        /// </summary>
        private void ValidateSubsystem()
        {
            if (!_subsystemDirty)
            {
                return;
            }

            // Set the suppression mask channels if the metadata is available
            if (_subsystem is {IsMetadataAvailable: true})
            {
                _subsystem.SuppressionMaskChannels = _channels;
                _subsystemDirty = false;
            }
        }

        /// <summary>
        /// Fetches the suppression image with the correct transform according to the occulsion technique.
        /// </summary>
        /// <param name="cpuImage">The resulting CPU image. It is the responsibility of the caller to dispose of it.</param>
        /// <param name="transform">The resulting image transform.</param>
        /// <returns>True if the image was successfully fetched, false otherwise.</returns>
        private bool FetchSuppressionImage(out XRCpuImage cpuImage, out Matrix4x4 transform)
        {
            cpuImage = default;
            transform = Matrix4x4.identity;

            if (_subsystem == null)
            {
                Log.Error(KMissingSubsystemMessage);
                return false;
            }

            XRCameraParams cameraParams;
            Matrix4x4? referencePose;

            switch (_technique)
            {
                case LightshipOcclusionExtension.OcclusionTechnique.ZBuffer:
                {
                    // Reconstruct the viewport if the screen orientation has changed
                    var currentOrientation = XRDisplayContext.GetScreenOrientation();
                    if (!_lastOrientation.HasValue || _lastOrientation != currentOrientation)
                    {
                        _lastOrientation = currentOrientation;
                        _viewport = new XRCameraParams
                        {
                            screenWidth = Screen.width,
                            screenHeight = Screen.height,
                            screenOrientation = currentOrientation
                        };
                    }

                    // Using the ZBuffer technique, the viewport is the full
                    // screen and the reference pose is current camera pose
                    referencePose = InputReader.CurrentPose;
                    cameraParams = _viewport;
                } break;

                case LightshipOcclusionExtension.OcclusionTechnique.OcclusionMesh:
                {
                    var depthTexture = GetTexture(ShaderProperties.DepthTextureId);
                    var extrinsics = GetMatrix(ShaderProperties.ExtrinsicsId);
                    if (depthTexture == null || !extrinsics.HasValue)
                    {
                        return false;
                    }

                    // Using the OcclusionMesh technique, the viewport is the depth texture
                    // resolution and the reference pose is the depth texture extrinsics;
                    referencePose = _origin != null && _origin.CameraFloorOffsetObject != null
                        ? _origin.CameraFloorOffsetObject.transform.worldToLocalMatrix * extrinsics
                        : extrinsics;

                    cameraParams = new XRCameraParams
                    {
                        screenWidth = depthTexture.width,
                        screenHeight = depthTexture.height,
                        screenOrientation = ScreenOrientation.LandscapeLeft
                    };
                } break;

                default:
                    return false;
            }

            return _subsystem.TryAcquireSuppressionMaskCpuImage(out cpuImage, out transform, cameraParams,
                referencePose);
        }
    }
}
