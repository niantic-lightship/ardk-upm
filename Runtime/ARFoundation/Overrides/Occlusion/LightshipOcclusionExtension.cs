// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Semantics;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using Niantic.Lightship.AR.Subsystems.Playback;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// This component allows configuration of the additional functionality available in
    /// Lightship's implementation of <see cref="XROcclusionSubsystem"/>.
    /// </summary>
    [PublicAPI]
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(ARCameraManager))]
    [RequireComponent(typeof(AROcclusionManager))]
    [DefaultExecutionOrder(ARUpdateOrder.k_OcclusionManager - 1)]
    public class LightshipOcclusionExtension : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// The sampling mode for determining the distance to the occluder.
        /// This distance is used to transform the depth buffer to provide
        /// accurate occlusions.
        /// </summary>
        public enum OptimalOcclusionDistanceMode
        {
            /// Take a few samples of the full depth buffer to
            /// determine the closest occluder on the screen.
            /// This will provide the best available occlusions
            /// if there are many occluded virtual objects of similar
            /// size and importance.
            ClosestOccluder,

            /// Sample the sub-region of the buffer that is directly over
            /// the main CG object, to determine the distance of its occluder
            /// in the world. This will provide the best quality occlusions
            /// if there is only one occluded virtual object, or if one is more
            /// visually prominent than the others
            SpecifiedGameObject,

            /// Stabilize the depth buffer relative to a pre-determined,
            /// unchanging depth. Not recommended if there are occluded virtual objects
            /// in the scene, but is more performant and thus optimal when
            /// occlusions are not needed.
            Static,
        }

        #endregion

        #region Constants

        /// <summary>
        /// Name of the default Lightship Occlusion Extension shader.
        /// </summary>
        public const string DefaultShaderName = "Lightship/OcclusionExtension";

        /// <summary>
        /// Name for the custom rendering command buffer.
        /// </summary>
        private const string k_CustomRenderPassName = "LightshipOcclusionExtension Pass (LegacyRP)";

        /// <summary>
        /// Message logged when the extension tries to set a frame rate that is not supported by the subsystem.
        /// </summary>
        private const string k_TargetFrameRateNotSupportedMessage =
            "TargetFrameRate is not supported on non-Lightship implementations of the XROcclusionSubsystem.";

        /// <summary>
        /// Message logged when the LatestInrinsicsMatrix API is not supported by the subsystem.
        /// </summary>
        private const string k_LatestIntrinsicsMatrixNotSupportedMessage =
            "LatestInrinsicsMatrix is not supported on non-Lightship implementations of the XROcclusionSubsystem.";

        /// <summary>
        /// Message logged when the extension tries to use the cpu image it failed to acquire.
        /// </summary>
        private const string k_MissingCpuImageMessage = "Could not acquire the cpu depth image.";

        /// <summary>
        /// Message logged when there has been an error updating the suppression texture.
        /// </summary>
        private const string k_SuppressionTextureErrorMessage = "Unable to update the depth suppresion texture.";

        /// <summary>
        /// Message logged when the OptimalOcclusionDistanceMode is invalidly set to SpecifiedGameObject.
        /// </summary>
        private const string k_MissingOccludeeMessage =
            "Active OptimalOcclusionDistanceMode is SpecifiedGameObject but the Principal Occludee " +
            "object is null. Falling back to the ClosestOccluder mode.";

        /// <summary>
        /// Message logged when the semantic depth suppression is enabled without the required components in the scene.
        /// </summary>
        private const string k_MissingSemanticManagerMessage =
            "Missing ARSemanticSegmentationManager component reference. " +
            "One in the scene is required to enable semantic depth suppression.";

        /// <summary>
        /// Message logged when the occlusion stabilization feature is enabled without the required components in the scene.
        /// </summary>
        private const string k_MissingMeshManagerMessage =
            "Missing ARMeshManager component reference. " +
            "One in the scene is required to enable occlusion stabilization.";

        /// <summary>
        /// Message logged when the occlusion extension shader is missing from the project.
        /// </summary>
        private const string k_MissingShaderResourceMessage =
            "Missing " + DefaultShaderName + " shader resource.";

        /// <summary>
        /// Message logged when the component is set to use a custom material, but the the material resource is null.
        /// </summary>
        private const string k_MissingCustomBackgroundMaterialMessage =
            "Set to use a custom background material without a valid reference.";

        /// <summary>
        /// Shader keyword for occlusion stabilization.
        /// </summary>
        private const string k_OcclusionStabilizationFeature = "FEATURE_STABILIZATION";

        /// <summary>
        /// Shader keyword for semantic suppression.
        /// </summary>
        private const string k_OcclusionSuppressionFeature = "FEATURE_SUPPRESSION";

        /// <summary>
        /// Shader keyword for depth edge smoothing.
        /// </summary>
        private const string k_DepthEdgeSmoothingFeature = "FEATURE_EDGE_SMOOTHING";

        /// <summary>
        /// Shader keyword for debug view.
        /// </summary>
        private const string k_DebugViewFeature = "FEATURE_DEBUG";

        /// <summary>
        /// Minimum possible value for the optimal occlusion depth.
        /// </summary>
        private const float k_MinimumDepthSample = 0.2f;

        /// <summary>
        /// Maximum possible value for the optimal occlusion depth.
        /// </summary>
        private const float k_MaximumDepthSample = 100.0f;

        /// <summary>
        /// Value used to exclude the edges of the image from 'ClosestOccluder' occlusion mode, since objects at
        /// the edges should be ignored for the calculation.
        /// </summary>
        private const float k_FullScreenSampleBorder = 0.2f;

        /// <summary>
        /// Rectangle used to sample the depth buffer for the optimal occlusion depth.
        /// </summary>
        private static readonly Rect s_fullScreenRect = new
        (
            k_FullScreenSampleBorder,
            k_FullScreenSampleBorder,
            1 - k_FullScreenSampleBorder,
            1 - k_FullScreenSampleBorder
        );

        /// <summary>
        /// ARFoundation's renderer gets added to the camera on the first AR frame, so this component needs
        ///  to wait this number of AR frames before attaching its command buffers.
        /// </summary>
        private const int k_attachDelay = 2;

        #endregion

        #region Serialized Fields

        [SerializeField]
        [Tooltip("Frame rate that depth inference will aim to run at")]
        [Range(1, 90)]
        private uint _targetFrameRate = LightshipOcclusionSubsystem.MaxRecommendedFrameRate;

        [SerializeField]
        [Tooltip("The current method used for determining the distance at which occlusions "
            + "will have the best visual quality.\n\n"
            + "ClosestOccluder -- average depth from the whole field of view.\n"
            + "SpecifiedGameObject -- depth of the specified object.")]
        private OptimalOcclusionDistanceMode _optimalOcclusionDistanceMode =
            OptimalOcclusionDistanceMode.ClosestOccluder;

        [SerializeField]
        [Tooltip("When enabled, will employ bilinear sampling on the depth texture for smoother occlusion edges.")]
        private bool _preferSmoothEdges;

        [SerializeField]
        [Tooltip("The principal virtual object being occluded in SpecifiedGameObject mode. "
            + "Occlusions will look most effective for this object, so it should be the "
            + "visual focal point of the experience.")]
        private Renderer _principalOccludee;

        [SerializeField]
        [Tooltip("When enabled, the occlusions use the combination of the fused mesh and depth image to "
            + "achieve more stable results while still accounting for moving objects.")]
        private bool _isOcclusionStabilizationEnabled;

        [SerializeField]
        [Tooltip("When enabled, rendering will be altered so that it virtual objects will not be occluded in "
            + "pixels that contain the specified semantic suppression channels. It is recommended to use this "
            + " feature to reduce visual artifacts such as objects being clipped by the floor or the sky.")]
        private bool _isOcclusionSuppressionEnabled;

        [SerializeField]
        [Tooltip("The list of semantic channels to be suppressed in the depth buffer. "
            + "Pixels classified as any of these channels will not occlude virtual objects.")]
        private List<string> _suppressionChannels = new();

        [SerializeField]
        [Tooltip("Whether to use the custom material for processing the AR background depth buffer.")]
        private bool _useCustomBackgroundMaterial;

        [SerializeField]
        private Material _customBackgroundMaterial;

        [SerializeField]
        private bool _bypassOcclusionManagerUpdates;

        [SerializeField]
        private ARSemanticSegmentationManager _semanticSegmentationManager;

        [SerializeField]
        private ARMeshManager _meshManager;

        #endregion

        #region Private Fields

        /// <summary>
        /// Whether the application is using the default render pipeline.
        /// </summary>
        private static bool IsUsingLegacyRenderPipeline => GraphicsSettings.currentRenderPipeline == null;

        // Required components
        private Camera _camera;
        private ARCameraManager _cameraManager;
        private XROcclusionSubsystem _occlusionSubsystem;
        private AROcclusionManager _occlusionManager;

        // Resources
        private XRCpuImage _cpuDepth;
        private Texture2D _gpuDepth;
        private Texture2D _gpuSuppression;
        private Texture2D _defaultDepthTexture;
        private Texture2D _defaultNonLinearDepthTexture;
        private Matrix4x4 _depthTransform;
        private CommandBuffer _backgroundCommandBuffer;
        private Material _backgroundMaterial;
        private LightshipFusedDepthCamera _fusedDepthCamera;

        // The number of AR frames received while waiting to attach command buffers.
        private int _numberOfARFramesReceived;

        // Additional helpers
        private bool _isValidated;
        private bool _isVisualizationEnabled;
        private bool _areCommandBuffersAttached;
        private bool _showedTargetFrameRateNotSupportedMessage;

        #endregion

        #region Shader Properties

        /// <summary>
        /// Property ID for the shader parameter for the depth texture.
        /// </summary>
        private static readonly int s_depthTextureId = Shader.PropertyToID("_Depth");

        /// <summary>
        /// Property ID for the shader parameter for the semantics texture.
        /// </summary>
        private static readonly int s_suppressionTextureId = Shader.PropertyToID("_Suppression");

        /// <summary>
        /// Property ID for the shader parameter for the fused depth texture (generated from the mesh).
        /// </summary>
        private static readonly int s_fusedDepthTextureId = Shader.PropertyToID("_FusedDepth");

        /// <summary>
        /// Property ID for the shader parameter for the depth display matrix.
        /// </summary>
        private static readonly int s_depthTransformId = Shader.PropertyToID("_DepthTransform");

        /// <summary>
        /// Property ID for the shader parameter for the semantics display matrix.
        /// </summary>
        private static readonly int s_suppressionTransformId = Shader.PropertyToID("_SuppressionTransform");

        /// <summary>
        /// Property ID for the shader parameter used to linearize (compressed) depth.
        /// </summary>
        private static readonly int s_depthTextureParams = Shader.PropertyToID("_DepthTextureParams");

        /// <summary>
        /// Property ID for the shader parameter that controls the blending between instant depth and fused depth.
        /// </summary>
        private static readonly int s_stabilizationThreshold = Shader.PropertyToID("_StabilizationThreshold");

        /// <summary>
        /// Property ID for the shader parameter for the Unity camera forward scale.
        /// </summary>
        private static readonly int s_cameraForwardScaleId = Shader.PropertyToID("_UnityCameraForwardScale");

        #endregion

        #region Properties

        /// <summary>
        /// Get or set the frame rate that depth inference will aim to run at
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
                    Log.Error("Target frame rate value must be greater than zero.");
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

        /// <summary>
        /// When enabled, the component displays the depth image used for occlusions.
        /// Note that visualization can only be used if a custom occlusion feature is
        /// active, e.g. suppression or stabilization.
        /// </summary>
        public bool Visualization
        {
            get { return _isVisualizationEnabled; }
            set
            {
                if (_isVisualizationEnabled != value)
                {
                    if (value)
                    {
                        BackgroundMaterial.EnableKeyword(k_DebugViewFeature);
                    }
                    else
                    {
                        BackgroundMaterial.DisableKeyword(k_DebugViewFeature);
                    }

                    _isVisualizationEnabled = value;
                }
            }
        }

        /// <summary>
        /// When enabled, the depth image will be sampled bilinearly during rendering.
        /// </summary>
        public bool PreferSmoothEdges
        {
            get { return _preferSmoothEdges; }
            set
            {
                var enableFeature = value && EligibleForEdgeSmoothing;

                if (_preferSmoothEdges != enableFeature)
                {
                    if (enableFeature)
                    {
                        BackgroundMaterial.EnableKeyword(k_DepthEdgeSmoothingFeature);
                    }
                    else
                    {
                        BackgroundMaterial.DisableKeyword(k_DepthEdgeSmoothingFeature);
                    }

                    _preferSmoothEdges = enableFeature;
                }
            }
        }

        /// <summary>
        /// The occlusion edge smoothing feature should only be used with low resolution depth images.
        /// This property returns whether the current settings qualify to enable edge smoothing.
        /// </summary>
        private bool EligibleForEdgeSmoothing
        {
            // Low resolution images are produced using lightship or lidar with fastest setting
            get => _occlusionSubsystem is LightshipOcclusionSubsystem ||
                _occlusionSubsystem.currentEnvironmentDepthMode == EnvironmentDepthMode.Fastest;
        }

        /// <summary>
        /// Returns the intrinsics matrix of the most recent semantic segmentation prediction. Contains values
        /// for the camera's focal length and principal point. Converts between 2D image pixel coordinates and
        /// 3D world coordinates relative to the camera.
        /// </summary>
        /// <value>
        /// The intrinsics matrix.
        /// </value>
        /// <exception cref="System.NotSupportedException">Thrown if getting intrinsics matrix is not supported.
        /// </exception>
        public Matrix4x4? LatestIntrinsicsMatrix
        {
            get
            {
                switch (_occlusionSubsystem)
                {
                    case LightshipOcclusionSubsystem lightshipOcclusionSubsystem:
                        return lightshipOcclusionSubsystem.LatestIntrinsicsMatrix;
                    case LightshipPlaybackOcclusionSubsystem lightshipPlaybackOcclusionSubsystem:
                        return lightshipPlaybackOcclusionSubsystem.LatestIntrinsicsMatrix;
                    default:
                        Log.Warning(k_LatestIntrinsicsMatrixNotSupportedMessage);
                        return default;
                }
            }
        }

        /// <summary>
        /// Get or set the current mode in use for determining the distance at which occlusions
        /// will have the best visual quality.
        /// </summary>
        public OptimalOcclusionDistanceMode Mode
        {
            get => _optimalOcclusionDistanceMode;
            set
            {
                if (_optimalOcclusionDistanceMode == value)
                {
                    return;
                }

                if (value == OptimalOcclusionDistanceMode.SpecifiedGameObject && _principalOccludee == null)
                {
                    Log.Error
                    (
                        "Active OptimalOcclusionDistanceMode is SpecifiedGameObject but the Principal Occludee " +
                        "object is null. Use the TrackOccludee method to set a Principal Occludee object."
                    );

                    return;
                }

                _optimalOcclusionDistanceMode = value;
            }
        }

        /// <summary>
        /// Get or set whether semantic segmentation based occlusion suppression is enabled.
        /// </summary>
        public bool IsOcclusionSuppressionEnabled
        {
            get { return _isOcclusionSuppressionEnabled; }
            set
            {
                if (_isOcclusionSuppressionEnabled != value)
                {
                    if (value)
                    {
                        if (_semanticSegmentationManager == null)
                        {
                            _semanticSegmentationManager = FindObjectOfType<ARSemanticSegmentationManager>();

                            if (_semanticSegmentationManager == null)
                            {
                                Log.Error(k_MissingSemanticManagerMessage);
                                return;
                            }
                        }

                        BackgroundMaterial.EnableKeyword(k_OcclusionSuppressionFeature);
                        _semanticSegmentationManager.SuppressionMaskChannels = _suppressionChannels;
                    }
                    else
                    {
                        BackgroundMaterial.DisableKeyword(k_OcclusionSuppressionFeature);
                        _semanticSegmentationManager.SuppressionMaskChannels = new List<string>();
                    }

                    _isOcclusionSuppressionEnabled = value;
                }
            }
        }

        /// <summary>
        /// Get or set whether meshing based occlusion stabilization is enabled.
        /// </summary>
        public bool IsOcclusionStabilizationEnabled
        {
            get { return _isOcclusionStabilizationEnabled; }
            set
            {
                if (_isOcclusionStabilizationEnabled != value)
                {
                    if (value)
                    {
                        if (_meshManager == null)
                        {
                            _meshManager = FindObjectOfType<ARMeshManager>();

                            if (_meshManager == null)
                            {
                                Log.Error(k_MissingMeshManagerMessage);
                                return;
                            }
                        }

                        BackgroundMaterial.EnableKeyword(k_OcclusionStabilizationFeature);
                        ToggleFusedDepthCamera(true);
                    }
                    else
                    {
                        BackgroundMaterial.DisableKeyword(k_OcclusionStabilizationFeature);
                        ToggleFusedDepthCamera(false);
                    }

                    _isOcclusionStabilizationEnabled = value;
                }
            }
        }

        /// <summary>
        /// Whether to prefer per-frame (0) or fused depth (1) during stabilization.
        /// </summary>
        public float StabilizationThreshold
        {
            get { return BackgroundMaterial.GetFloat(s_stabilizationThreshold); }
            set { BackgroundMaterial.SetFloat(s_stabilizationThreshold, Mathf.Clamp(value, 0.0f, 1.0f)); }
        }

        /// <summary>
        /// Whether the second pass of background rendering is active to satisfy custom occlusion features.
        /// </summary>
        public bool IsRenderingActive
        {
            get => (_areCommandBuffersAttached || !IsUsingLegacyRenderPipeline) && IsAnyFeatureEnabled;
        }

        /// <summary>
        /// Whether any feature provided by the occlusion extension is enabled.
        /// </summary>
        private bool IsAnyFeatureEnabled
        {
            get { return _isOcclusionSuppressionEnabled || _isOcclusionStabilizationEnabled || _preferSmoothEdges; }
        }

        /// <summary>
        /// Whether to disable automatically updating the depth texture of
        /// the occlusion manager. This feature can be used to avoid
        /// redundant texture operations since depth is ultimately going
        /// to be overriden by the Lightship Occlusion Extension anyway.
        /// Not using this setting may result in undesired synchronization
        /// with the rendering thread that impacts performance.
        /// </summary>
        public bool BypassOcclusionManagerUpdates
        {
            get => _bypassOcclusionManagerUpdates;
            set
            {
                _bypassOcclusionManagerUpdates = value;
                ReconfigureSubsystem();
            }
        }

        /// <summary>
        /// Returns the depth texture used to render occlusions.
        /// </summary>
        public Texture2D DepthTexture
        {
            get
            {
                return _occlusionSubsystem is not LightshipOcclusionSubsystem
                    ? _occlusionManager.environmentDepthTexture
                    : _gpuDepth;
            }
        }

        /// <summary>
        /// Returns a transform for converting between normalized image
        /// coordinates and a coordinate space appropriate for rendering
        /// the depth texture onscreen.
        /// </summary>
        public Matrix4x4 DepthTransform
        {
            get { return _depthTransform; }
        }

        /// <summary>
        /// The default texture bound to the depth property on the material.
        /// It let's every pixel pass through until the real depth texture is ready to use.
        /// This resource only gets allocated if custom occlusion features are used during
        /// the lifetime of the occlusion extension component.
        /// </summary>
        private Texture2D DefaultDepthTexture
        {
            get
            {
                // Lazy
                if (_defaultDepthTexture == null)
                {
                    var maxDistance = _camera.farClipPlane;
                    _defaultDepthTexture = new Texture2D(2, 2, TextureFormat.RFloat, mipChain: false);
                    _defaultDepthTexture.SetPixelData(new[] {maxDistance, maxDistance, maxDistance, maxDistance}, 0);
                    _defaultDepthTexture.Apply(false);
                }

                return _defaultDepthTexture;
            }
        }

        /// <summary>
        /// The default texture bound to the fused depth property on the material.
        /// It let's every pixel pass through until the real depth texture is ready to use.
        /// This resource only gets allocated if custom occlusion features are used during
        /// the lifetime of the occlusion extension component.
        /// </summary>
        private Texture2D DefaultNonLinearDepthTexture
        {
            get
            {
                // Lazy
                if (_defaultNonLinearDepthTexture == null)
                {
                    var val = LightshipOcclusionExtensionUtils.IsDepthReversed() ? 0.0f : 1.0f;
                    _defaultNonLinearDepthTexture = new Texture2D(2, 2, TextureFormat.RFloat, mipChain: false);
                    _defaultNonLinearDepthTexture.SetPixelData(new[] {val, val, val, val}, 0);
                    _defaultNonLinearDepthTexture.Apply(false);
                }

                return _defaultNonLinearDepthTexture;
            }
        }

        /// <summary>
        /// Get or set whether to use the custom material for processing the AR background depth buffer.
        /// </summary>
        public bool UseCustomBackgroundMaterial
        {
            get => _useCustomBackgroundMaterial;
            set
            {
                _useCustomBackgroundMaterial = value;
                if (value)
                {
                    // Release the unused material
                    if (_backgroundMaterial != null)
                    {
                        Destroy(_backgroundMaterial);
                    }
                }
            }
        }

        /// <summary>
        /// Get or set the custom material used for processing the AR background depth buffer.
        /// </summary>
        public Material CustomBackgroundMaterial
        {
            get => _customBackgroundMaterial;
            set => _customBackgroundMaterial = value;
        }

        /// <summary>
        /// The background rendering material in use.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the the background shader resource cannot be found.</exception>
        public Material BackgroundMaterial
        {
            get
            {
                var result = _useCustomBackgroundMaterial ? _customBackgroundMaterial : _backgroundMaterial;
                if (result == null)
                {
                    if (!_useCustomBackgroundMaterial)
                    {
                        // Load the background material for the first time
                        var shader = Shader.Find(DefaultShaderName);
                        if (shader == null)
                        {
                            throw new InvalidOperationException
                            (
                                $"Could not find shader named '{DefaultShaderName}' required " +
                                "for depth processing."
                            );
                        }

                        _backgroundMaterial = new Material(shader);
                        result = _backgroundMaterial;
                    }
                    else
                    {
                        Log.Error(k_MissingCustomBackgroundMaterialMessage);
                    }
                }

                return result;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Adds a semantic segmentation channel to the collection of channels that are suppressed in the depth
        /// buffer.
        /// </summary>
        /// <param name="channelName">Semantic segmentation channel to add</param>
        /// <returns>True if the channel was successfully added.</returns>
        public bool AddSemanticSuppressionChannel(string channelName)
        {
            if (_suppressionChannels.Contains(channelName))
            {
                return false;
            }

            _suppressionChannels.Add(channelName);
            if (IsOcclusionSuppressionEnabled)
            {
                _semanticSegmentationManager.SuppressionMaskChannels = _suppressionChannels;
            }

            return true;
        }

        /// <summary>
        /// Removes a semantic segmentation channel, if it exists, from the collection of channels
        /// that are suppressed in the depth buffer.
        /// </summary>
        /// <param name="channelName">Semantic segmentation channel to remove</param>
        public void RemoveSemanticSuppressionChannel(string channelName)
        {
            _suppressionChannels.Remove(channelName);
            if (IsOcclusionSuppressionEnabled)
            {
                _semanticSegmentationManager.SuppressionMaskChannels = _suppressionChannels;
            }
        }

        /// <summary>
        /// Returns the metric eye depth at the specified pixel coordinates.
        /// </summary>
        /// <param name="screenX">The x position on the screen.</param>
        /// <param name="screenY">The y position on the screen.</param>
        /// <param name="depth">The resulting depth value.</param>
        /// <returns>Whether retrieving the depth value was successful.</returns>
        public bool TryGetDepth(int screenX, int screenY, out float depth)
        {
            // Check if we have a valid cpu image
            if (!_cpuDepth.valid)
            {
                Log.Warning(k_MissingCpuImageMessage);
                depth = float.NaN;
                return false;
            }

            // Acquire the image data
            var data = ImageSamplingUtils.AcquireDataBuffer<float>(_cpuDepth);
            if (!data.HasValue)
            {
                depth = float.NaN;
                return false;
            }

            // Sample the image
            var uv = new Vector2(screenX / (float)Screen.width, screenY / (float)Screen.height);
            depth = data.Value.Sample(_cpuDepth.width, _cpuDepth.height, uv, _depthTransform);

            // Success
            return true;
        }

        /// <summary>
        /// Sets the principal virtual object being occluded in the SpecifiedGameObject occlusion mode
        /// </summary>
        /// @note This method changes the optimal occlusion distance mode setting.
        public void TrackOccludee(Renderer occludee)
        {
            if (occludee == null)
            {
                throw new ArgumentNullException(nameof(occludee));
            }

            _principalOccludee = occludee;
            Mode = OptimalOcclusionDistanceMode.SpecifiedGameObject;
        }

        #endregion

        #region Unity Engine Callbacks

        private void Awake()
        {
            // Acquire components
            _camera = GetComponent<Camera>();
            _cameraManager = GetComponent<ARCameraManager>();
            _occlusionManager = GetComponent<AROcclusionManager>();

            // Acquire the subsystem
            if (ValidateSubsystem() == (true, true))
            {
                ReconfigureSubsystem();
            }

            // Validate settings
            ValidateOcclusionDistanceMode();
            ValidateFeatures();
        }

        private void OnDestroy()
        {
            _backgroundCommandBuffer?.Dispose();

            if (_backgroundMaterial)
            {
                Destroy(_backgroundMaterial);
            }

            if (_fusedDepthCamera != null)
            {
                Destroy(_fusedDepthCamera.gameObject);
            }

            if (_defaultDepthTexture != null)
            {
                Destroy(_defaultDepthTexture);
            }

            if (_defaultNonLinearDepthTexture != null)
            {
                Destroy(_defaultNonLinearDepthTexture);
            }

            if (_gpuDepth != null)
            {
                Destroy(_gpuDepth);
            }

            if (_cpuDepth.valid)
            {
                _cpuDepth.Dispose();
            }

            if (_gpuSuppression != null)
            {
                Destroy(_gpuSuppression);
            }
        }

        private void OnEnable()
        {
            _cameraManager.frameReceived += CameraManager_OnFrameReceived;
            HandleCommandBufferBehaviour();
        }

        private void OnDisable()
        {
            if (_cameraManager != null)
            {
                _cameraManager.frameReceived -= CameraManager_OnFrameReceived;
            }

            HandleCommandBufferBehaviour();
        }

        private void Update()
        {
            // If automatic XR loading is enabled, then subsystems will be available before the Awake call.
            // However, if XR loading is done manually, then this component needs to check every Update call
            // if a compatible XROcclusionSubsystem has been loaded.
            if (!_isValidated)
            {
                var (isLoaderInitialized, isValidSubsystemLoaded) = ValidateSubsystem();
                if (!isLoaderInitialized || !isValidSubsystemLoaded)
                {
                    return;
                }

                // Reconfigure after validation
                ReconfigureSubsystem();
            }

            if (!_occlusionSubsystem.running ||
                _occlusionManager.currentOcclusionPreferenceMode == OcclusionPreferenceMode.NoOcclusion)
            {
                return;
            }

            if (FetchDepthData(out Texture2D depthTexture))
            {
                HandleOcclusionDistance();
                HandleOcclusionRendering(depthTexture);
            }
        }

        private void CameraManager_OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (!_areCommandBuffersAttached)
            {
                // Accumulate the number of frames
                _numberOfARFramesReceived = (_numberOfARFramesReceived + 1) % int.MaxValue;
            }

            // Handle automatic command buffer attachment
            HandleCommandBufferBehaviour();
        }

        #endregion

        /// <summary>
        /// Updates the XRCpuImage reference by fetching the latest depth image on cpu memory.
        /// If the XRCpuImage already exists, it will be disposed before updating. Finally,
        /// the function will output a reference to the depth image on gpu that is most
        /// appropriate for the current configuration.
        /// </summary>
        /// <param name="outDepth">A reference to the latest depth texture.</param>
        /// <returns>Whether an appropriate depth texture could be acquired.</returns>>
        private bool FetchDepthData(out Texture2D outDepth)
        {
            // Defaults
            outDepth = null;

            // Specify the screen as the viewport
            var viewport = new XRCameraParams
            {
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                screenOrientation = XRDisplayContext.GetScreenOrientation()
            };

            // Using Lightship occlusion subsystem
            if (_occlusionSubsystem is LightshipOcclusionSubsystem lsSubsystem)
            {
                // When using lightship depth, we acquire the image in its native container
                // and use a custom matrix to fit it to the viewport. This matrix may contain
                // re-projection (warping) as well.
                if (lsSubsystem.TryAcquireEnvironmentDepthCpuImage(viewport, out var image, out _depthTransform))
                {
                    // Release the previous image
                    if (_cpuDepth.valid)
                    {
                        _cpuDepth.Dispose();
                    }

                    // Cache the reference to the new image
                    _cpuDepth = image;

                    // Update the depth texture from the cpu image
                    var gotGpuImage = ImageSamplingUtils.CreateOrUpdateTexture(
                        source: _cpuDepth,
                        destination: ref _gpuDepth,
                        linearColorSpace: true, // avoid gamma correction on depth values
                        destinationFilter: FilterMode.Point,
                        pushToGpu: true
                    );

                    outDepth = gotGpuImage ? _gpuDepth : null;
                    return gotGpuImage;
                }
            }
            // Using foreign occlusion subsystem
            else if (_occlusionSubsystem != null)
            {
                if (_occlusionSubsystem.TryAcquireEnvironmentDepthCpuImage(out var image))
                {
                    // Release the previous image
                    if (_cpuDepth.valid)
                    {
                        _cpuDepth.Dispose();
                    }

                    // Cache the reference to the new image
                    _cpuDepth = image;

                    // When using lidar, the image container will have the same aspect ratio
                    // as the camera image, according to ARFoundation standards. For the matrix
                    // here, we do not warp, just use an affine display matrix which is the same
                    // that is used to display the camera image.
                    _depthTransform = CameraMath.CalculateDisplayMatrix
                    (
                        image.width,
                        image.height,
                        (int)viewport.screenWidth,
                        (int)viewport.screenHeight,
                        viewport.screenOrientation
                    );

                    // We make a special case for Lidar on fastest setting, because there is
                    // a bug in ARFoundation that makes the manager owned texture flicker.
                    // By creating a texture from the cpu image and retaining ourself, the
                    // image becomes stable. This issue is probably due to the changes introduced
                    // in iOS 16 where the metal command buffer do not implicitly retain textures.
                    if (_occlusionSubsystem.currentEnvironmentDepthMode == EnvironmentDepthMode.Fastest)
                    {
                        // Update the depth texture from the cpu image
                        var gotGpuImage = ImageSamplingUtils.CreateOrUpdateTexture(
                            source: _cpuDepth,
                            destination: ref _gpuDepth,
                            linearColorSpace: true, // avoid gamma correction on depth values
                            destinationFilter: FilterMode.Point,
                            pushToGpu: true
                        );

                        outDepth = gotGpuImage ? _gpuDepth : null;
                        return gotGpuImage;
                    }

                    // We rely on the foreign occlusion subsystem to manage the gpu image
                    outDepth = _occlusionManager.environmentDepthTexture;
                    return outDepth is not null;
                }
            }

            // Could not acquire the latest cpu image
            return false;
        }

        private void HandleOcclusionDistance()
        {
            if (Mode == OptimalOcclusionDistanceMode.Static || _occlusionSubsystem is not LightshipOcclusionSubsystem)
            {
                return;
            }

            if (!_cpuDepth.valid)
            {
                Log.Warning(k_MissingCpuImageMessage);
                return;
            }

            // Acquire the image subregion to sample
            Rect region;
            if (Mode != OptimalOcclusionDistanceMode.SpecifiedGameObject)
            {
                // Use full image
                region = s_fullScreenRect;
            }
            else
            {
                if (_principalOccludee != null)
                {
                    // Calculate subregion
                    region = LightshipOcclusionExtensionUtils.CalculateScreenRect(_principalOccludee, _camera);
                }
                else
                {
                    // Use full image
                    region = s_fullScreenRect;

                    // Set fallback
                    Mode = OptimalOcclusionDistanceMode.ClosestOccluder;
                    Log.Warning(k_MissingOccludeeMessage);
                }
            }

            // Sparsely sample depth within bounds
            var depth = LightshipOcclusionExtensionUtils.SampleImageSubregion(_cpuDepth, _depthTransform, region);

            // Cache result
            XRDisplayContext.OccludeeEyeDepth = Mathf.Clamp(depth, k_MinimumDepthSample, k_MaximumDepthSample);
        }

        private void HandleOcclusionRendering(Texture2D depthTexture)
        {
            // Handle custom rendering
            if (_areCommandBuffersAttached || !IsUsingLegacyRenderPipeline)
            {
                // Acquire the material in use
                var material = BackgroundMaterial;

                // Bind depth
                material.SetTexture(s_depthTextureId, depthTexture);
                material.SetMatrix(s_depthTransformId, _depthTransform);

                // Set scale: this computes the affect the camera's localToWorld has on the the length of the
                // forward vector, i.e., how much farther from the camera are things than with unit scale.
                var forward = transform.localToWorldMatrix.GetColumn(2);
                var scale = forward.magnitude;
                material.SetFloat(s_cameraForwardScaleId, scale);

                // Semantic depth suppression
                if (_isOcclusionSuppressionEnabled)
                {
                    // Let the manager know which channels do we want to suppress with
                    _semanticSegmentationManager.SuppressionMaskChannels = _suppressionChannels;

                    // Update the suppression mask texture
                    if (_semanticSegmentationManager.TryAcquireSuppressionMaskCpuImage(out var cpuSuppression,
                            out var suppressionTransform))
                    {
                        // Copy to gpu
                        if (ImageSamplingUtils.CreateOrUpdateTexture(cpuSuppression, ref _gpuSuppression))
                        {
                            // Bind the gpu image
                            material.SetTexture(s_suppressionTextureId, _gpuSuppression);
                            material.SetMatrix(s_suppressionTransformId, suppressionTransform);
                        }
                        else
                        {
                            Log.Error(k_SuppressionTextureErrorMessage);
                        }

                        // Release the cpu image
                        cpuSuppression.Dispose();
                    }
                }

                // Occlusion stabilization with mesh
                if (_isOcclusionStabilizationEnabled)
                {
                    // Sync pose and bind mesh depth
                    _fusedDepthCamera.SetViewProjection(_camera.worldToCameraMatrix, _camera.projectionMatrix);
                    material.SetTexture(s_fusedDepthTextureId, _fusedDepthCamera.GpuTexture);
                }

                if (_preferSmoothEdges)
                {
                    var width = depthTexture.width;
                    var height = depthTexture.height;
                    _backgroundMaterial.SetVector(s_depthTextureParams,
                        new Vector4(1.0f / width, 1.0f / height, width, height));
                }
            }
        }

        /// <summary>
        /// Ensures that the occlusion distance mode if correctly set.
        /// Falls back to the default technique if it isn't.
        /// </summary>
        private void ValidateOcclusionDistanceMode()
        {
            if (Mode == OptimalOcclusionDistanceMode.SpecifiedGameObject && _principalOccludee == null)
            {
                Log.Warning(k_MissingOccludeeMessage);
                Mode = OptimalOcclusionDistanceMode.ClosestOccluder;
            }
        }

        /// <summary>
        /// Returns (isLoaderInitialized, isValidSubsystemLoaded)
        /// </summary>
        private (bool, bool) ValidateSubsystem()
        {
            if (_isValidated)
            {
                return (true, true);
            }

            var xrManager = XRGeneralSettings.Instance.Manager;
            if (!xrManager.isInitializationComplete)
            {
                return (false, false);
            }

            _occlusionSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XROcclusionSubsystem>();
            if (_occlusionSubsystem == null)
            {
                Log.Warning
                (
                    "Destroying LightshipOcclusionExtension component because " +
                    $"no active {typeof(XROcclusionSubsystem).FullName} is available. " +
                    "Please ensure that a valid loader configuration exists in the XR project settings."
                );

                Destroy(this);
                return (true, false);
            }

            _isValidated = true;
            return (true, true);
        }

        /// <summary>
        /// Ensures that the occlusion features have access to the appropriate resources.
        /// </summary>
        private void ValidateFeatures()
        {
            if (_isOcclusionSuppressionEnabled && _semanticSegmentationManager == null)
            {
                Log.Error(k_MissingSemanticManagerMessage);
                return;
            }

            if (_isOcclusionStabilizationEnabled && _meshManager == null)
            {
                Log.Error(k_MissingMeshManagerMessage);
                return;
            }

            if (BackgroundMaterial == null)
            {
                Log.Error(k_MissingShaderResourceMessage);
                return;
            }

            // Recompile the shader for suppression
            if (_isOcclusionSuppressionEnabled)
            {
                BackgroundMaterial.EnableKeyword(k_OcclusionSuppressionFeature);
            }
            else
            {
                BackgroundMaterial.DisableKeyword(k_OcclusionSuppressionFeature);
            }

            // Recompile the shader for stabilization
            if (_isOcclusionStabilizationEnabled)
            {
                BackgroundMaterial.EnableKeyword(k_OcclusionStabilizationFeature);
                ToggleFusedDepthCamera(true);
            }
            else
            {
                BackgroundMaterial.DisableKeyword(k_OcclusionStabilizationFeature);
                ToggleFusedDepthCamera(false);
            }

            // Recompile the shader for depth compression
            if (_preferSmoothEdges && _occlusionSubsystem is LightshipOcclusionSubsystem)
            {
                BackgroundMaterial.EnableKeyword(k_DepthEdgeSmoothingFeature);
            }
            else
            {
                BackgroundMaterial.DisableKeyword(k_DepthEdgeSmoothingFeature);

                // Bypass this feature on foreign occlusion subsystems
                _preferSmoothEdges = false;
            }
        }

        /// <summary>
        /// Enables or disables the secondary camera that intended to capture mesh depth for occlusion stabilization.
        /// </summary>
        /// <param name="isEnabled">Whether the secondary camera should be enabled.</param>
        private void ToggleFusedDepthCamera(bool isEnabled)
        {
            if (isEnabled)
            {
                // Create the camera if it doesn't exist
                if (_fusedDepthCamera == null)
                {
                    // Add as a child
                    var go = new GameObject("FusedDepthCamera");
                    go.transform.SetParent(transform);

                    // Cache the component
                    _fusedDepthCamera = go.AddComponent<LightshipFusedDepthCamera>();
                }

                // Configure the camera to capture mesh depth
                _fusedDepthCamera.Configure(meshLayer: _meshManager.meshPrefab.gameObject.layer);
            }

            // Enable or disable the camera
            if (_fusedDepthCamera != null)
            {
                _fusedDepthCamera.gameObject.SetActive(isEnabled);
            }
        }

        /// <summary>
        /// Refreshes the configuration of the occlusion subsystem.
        /// </summary>
        private void ReconfigureSubsystem()
        {
            // Custom settings for the lightship occlusion subsystem
            if (_occlusionSubsystem is LightshipOcclusionSubsystem lsSubsystem)
            {
                // Set target frame rate
                lsSubsystem.TargetFrameRate = _targetFrameRate;

                // Bypass occlusion manager updates
                lsSubsystem.RequestDisableFetchTextureDescriptors =
                    _areCommandBuffersAttached && _bypassOcclusionManagerUpdates;
            }
        }

        /// <summary>
        /// Verifies the state of the component and enables or disables custom rendering accordingly.
        /// </summary>
        private void HandleCommandBufferBehaviour()
        {
            // Evaluate state
            var canAttachCommandBuffer = _numberOfARFramesReceived > k_attachDelay;
            var shouldAttachCommandBuffer = gameObject.activeInHierarchy && enabled && IsAnyFeatureEnabled;

            if (!_areCommandBuffersAttached)
            {
                // Attach?
                if (canAttachCommandBuffer && shouldAttachCommandBuffer)
                {
                    ConfigureCameraCommandBuffers(addBuffers: true);
                }
            }
            else
            {
                // Detach?
                if (!shouldAttachCommandBuffer)
                {
                    _numberOfARFramesReceived = 0;
                    ConfigureCameraCommandBuffers(addBuffers: false);
                }
            }
        }

        /// <summary>
        /// Attaches or detaches the background rendering command buffer to the main camera.
        /// </summary>
        /// <param name="addBuffers">Whether background rendering should be enabled.</param>
        private void ConfigureCameraCommandBuffers(bool addBuffers)
        {
            // Omit this step if the application is using URP
            if (!IsUsingLegacyRenderPipeline)
            {
                return;
            }

            Log.Info("ConfigureCameraCommandBuffers: " + addBuffers);
            if (addBuffers == _areCommandBuffersAttached)
            {
                return;
            }

            // Acquire the command buffer
            if (addBuffers)
            {
                var commandBuffer = GetOrConstructCommandBuffer();

                // Attach to the camera
                _camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
                _camera.AddCommandBuffer(CameraEvent.BeforeGBuffer, commandBuffer);

                // Bind pass-through texture by default
                BackgroundMaterial.SetTexture(s_depthTextureId, DefaultDepthTexture);
                BackgroundMaterial.SetTexture(s_fusedDepthTextureId, DefaultNonLinearDepthTexture);
            }
            else if (_backgroundCommandBuffer != null)
            {
                // Detach from the camera
                _camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, _backgroundCommandBuffer);
                _camera.RemoveCommandBuffer(CameraEvent.BeforeGBuffer, _backgroundCommandBuffer);

                // Release the command buffer
                _backgroundCommandBuffer?.Dispose();
                _backgroundCommandBuffer = null;
            }

            _areCommandBuffersAttached = addBuffers;
        }

        /// <summary>
        /// Retrieves the background rendering command buffer. If it doesn't exist, it will be constructed.
        /// </summary>
        private CommandBuffer GetOrConstructCommandBuffer()
        {
            if (_backgroundCommandBuffer == null)
            {
                _backgroundCommandBuffer = new CommandBuffer();
                _backgroundCommandBuffer.name = k_CustomRenderPassName;
                _backgroundCommandBuffer.Blit(null, BuiltinRenderTextureType.CameraTarget, BackgroundMaterial);
            }

            return _backgroundCommandBuffer;
        }
    }
}
