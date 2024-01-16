// Copyright 2022-2024 Niantic.
using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Log;
using Niantic.Lightship.AR.Semantics;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using Niantic.Lightship.AR.Subsystems.Playback;
using Unity.Collections.LowLevel.Unsafe;
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

        [SerializeField]
        [Tooltip("Frame rate that depth inference will aim to run at")]
        [Range(1, 90)]
        private uint _targetFrameRate = LightshipOcclusionSubsystem.MaxRecommendedFrameRate;

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

        private bool _showedTargetFrameRateNotSupportedMessage;
        private const string k_TargetFrameRateNotSupportedMessage =
            "TargetFrameRate is not supported on non-Lightship implementations of the XROcclusionSubsystem.";

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
                if (_occlusionSubsystem is LightshipOcclusionSubsystem lightshipOcclusionSubsystem)
                {
                    return lightshipOcclusionSubsystem.LatestIntrinsicsMatrix;
                }
                if (_occlusionSubsystem is LightshipPlaybackOcclusionSubsystem lightshipPlaybackOcclusionSubsystem)
                {
                    return lightshipPlaybackOcclusionSubsystem.LatestIntrinsicsMatrix;
                }

                Log.Warning(k_LatestIntrinsicsMatrixNotSupportedMessage);
                return default;
            }
        }

        private const string k_LatestIntrinsicsMatrixNotSupportedMessage =
            "LatestInrinsicsMatrix is not supported on non-Lightship implementations of the XROcclusionSubsystem.";

        [SerializeField]
        [Tooltip("The current method used for determining the distance at which occlusions "
                  + "will have the best visual quality.\n\n"
                  + "ClosestOccluder -- average depth from the whole field of view.\n"
                  + "SpecifiedGameObject -- depth of the specified object.")]
        private OptimalOcclusionDistanceMode _optimalOcclusionDistanceMode = OptimalOcclusionDistanceMode.ClosestOccluder;

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
                    return;

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

        [SerializeField]
        [Tooltip("The principal virtual object being occluded in SpecifiedGameObject mode. "
                 + "Occlusions will look most effective for this object, so it should be the "
                 + "visual focal point of the experience.")]
        private Renderer _principalOccludee;

        /// <summary>
        /// Message logged when the OptimalOcclusionDistanceMode is invalidly set to SpecifiedGameObject.
        /// </summary>
        private const string k_MissingOccludeeMessage =
            "Active OptimalOcclusionDistanceMode is SpecifiedGameObject but the Principal Occludee " +
            "object is null. Falling back to the ClosestOccluder mode.";

        /// <summary>
        /// Shader keyword for occlusion stabilization.
        /// </summary>
        private const string k_OcclusionStabilizationFeature = "FEATURE_STABILIZATION";

        /// <summary>
        /// Shader keyword for semantic suppression.
        /// </summary>
        private const string k_OcclusionSuppressionFeature = "FEATURE_SUPPRESSION";

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
        private static readonly Rect s_fullScreenRect =
            new
            (
                k_FullScreenSampleBorder,
                k_FullScreenSampleBorder,
                1 - k_FullScreenSampleBorder,
                1 - k_FullScreenSampleBorder
            );

        [SerializeField]
        [Tooltip("When enabled, rendering will be altered so that it virtual objects will not be occluded in "
                  + "pixels that contain the specified semantic suppression channels. It is recommended to use this "
                  + " feature to reduce visual artifacts such as objects being clipped by the floor or the sky.")]
        private bool _isOcclusionSuppressionEnabled;

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
                        UpdateSemanticsMask();
                        BackgroundMaterial.EnableKeyword(k_OcclusionSuppressionFeature);
                    }
                    else
                    {
                        BackgroundMaterial.DisableKeyword(k_OcclusionSuppressionFeature);
                    }

                    _isOcclusionSuppressionEnabled = value;
                    ConfigureRendering();
                }
            }
        }

        /// <summary>
        /// This component must be active in the scene for semantic depth suppression to function.
        /// </summary>
        [SerializeField]
        private ARSemanticSegmentationManager _semanticSegmentationManager;

        [SerializeField]
        [Tooltip("The list of semantic channels to be suppressed in the depth buffer. "
                  + "Pixels classified as any of these channels will not occlude virtual objects.")]
        private List<string> _suppressionChannels = new();

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
            _suppressionChannelsMaskOutdated = true;
            return true;
        }

        /// <summary>
        /// Removes a semantic segmentation channel, if it exists, from the collection of channels
        /// that are suppressed in the depth buffer.
        /// </summary>
        /// <param name="channelName">Semantic segmentation channel to remove</param>
        /// <returns></returns>
        public void RemoveSemanticSuppressionChannel(string channelName)
        {
            if (_suppressionChannels.Remove(channelName))
            {
                _suppressionChannelsMaskOutdated = true;
            }
        }

        /// <summary>
        /// A bitmask covering which semantic channels are suppressed.
        /// </summary>
        private int _suppressionChannelsMask;

        /// <summary>
        /// Whether the suppression channels mask needs to be updated. The mask is updated in the Update method
        /// rather than directly by when it becomes outdated in because semantic channel data may not be available
        /// at that time.
        /// </summary>
        private bool _suppressionChannelsMaskOutdated;

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
            "Missing " + k_LightshipOcclusionExtensionShaderName + " shader resource.";

        [SerializeField]
        [Tooltip("When enabled, the occlusions use the combination of the fused mesh and depth image to "
                  + "achieve more stable results while still accounting for moving objects.")]
        private bool _isOcclusionStabilizationEnabled;

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
                    ConfigureRendering();
                }
            }
        }

        /// <summary>
        /// Whether to prefer per-frame (0) or fused depth (1) during stabilization.
        /// </summary>
        public float StabilizationThreshold
        {
            get
            {
                return BackgroundMaterial.GetFloat(s_stabilizationThreshold);
            }

            set
            {
                BackgroundMaterial.SetFloat(s_stabilizationThreshold, Mathf.Clamp(value, 0.0f, 1.0f));
            }
        }

        /// <summary>
        /// This component must be active in the scene for occlusion stabilization to function.
        /// </summary>
        [SerializeField]
        private ARMeshManager _meshManager;

        /// <summary>
        /// The camera that captures the depth of the fused mesh for occlusion stabilization.
        /// </summary>
        private LightshipFusedDepthCamera _fusedDepthCamera;

        [SerializeField]
        [Tooltip("Whether to use the custom material for processing the AR background depth buffer.")]
        private bool _useCustomBackgroundMaterial;

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
                    if (_backgroundMaterial != null)
                    {
                        Destroy(_backgroundMaterial);
                    }

                    _backgroundMaterial = CustomBackgroundMaterial;
                }
            }
        }

        /// <summary>
        /// The custom material used for processing the AR background depth buffer.
        /// </summary>
        [SerializeField]
        private Material _customBackgroundMaterial;

        /// <summary>
        /// Get or set the custom material used for processing the AR background depth buffer.
        /// </summary>
        public Material CustomBackgroundMaterial
        {
            get => _customBackgroundMaterial;
            set
            {
                _customBackgroundMaterial = value;
                if (_useCustomBackgroundMaterial)
                {
                    if (_backgroundMaterial != null)
                    {
                        Destroy(_backgroundMaterial);
                    }

                    _backgroundMaterial = _customBackgroundMaterial;
                }
            }
        }

        /// <summary>
        /// Whether the second pass of background rendering is active to satisfy custom occlusion features.
        /// </summary>
        public bool IsRenderingActive
        {
            get
            {
                return (_areCommandBuffersAdded || !IsUsingLegacyRenderPipeline) &&
                    (_isOcclusionSuppressionEnabled || _isOcclusionStabilizationEnabled);
            }
        }

        /// <summary>
        /// Name of the default Lightship Occlusion Extension shader.
        /// </summary>
        private const string k_LightshipOcclusionExtensionShaderName = "Lightship/OcclusionExtension";

        /// <summary>
        /// Name of the default Lightship Occlusion Extension shader.
        /// </summary>
        public const string occlusionExtensionShaderName = k_LightshipOcclusionExtensionShaderName;

        /// <summary>
        /// Material used for processing the AR background depth buffer.
        /// </summary>
        private Material _backgroundMaterial;

        /// <summary>
        /// Property ID for the shader parameter for the depth texture.
        /// </summary>
        private static readonly int s_depthTextureId = Shader.PropertyToID("_Depth");

        /// <summary>
        /// Property ID for the shader parameter for the fused depth texture (generated from the mesh).
        /// </summary>
        private static readonly int s_fusedDepthTextureId = Shader.PropertyToID("_FusedDepth");

        /// <summary>
        /// Property ID for the shader parameter for the semantics texture.
        /// </summary>
        private static readonly int s_semanticsTextureId = Shader.PropertyToID("_Semantics");

        /// <summary>
        /// Property ID for the shader parameter for the semantics display matrix.
        /// </summary>
        private static readonly int s_semanticsTransformId = Shader.PropertyToID("_SemanticsTransform");

        /// <summary>
        /// Property ID for the shader parameter for the depth display matrix.
        /// </summary>
        private static readonly int s_displayMatrixId = Shader.PropertyToID("_DisplayMatrix");

        /// <summary>
        /// Property ID for the shader parameter for the semantic channel bit mark.
        /// </summary>
        private static readonly int s_bitMaskId = Shader.PropertyToID("_BitMask");

        /// <summary>
        ///
        /// </summary>
        private static readonly int s_stabilizationThreshold = Shader.PropertyToID("_StabilizationThreshold");

        /// <summary>
        /// Property ID for the shader parameter for the Unity camera forward scale.
        /// </summary>
        private static readonly int s_cameraForwardScaleId = Shader.PropertyToID("_UnityCameraForwardScale");

        /// <summary>
        /// Name for the custom rendering command buffer.
        /// </summary>
        private const string k_CustomRenderPassName = "LightshipOcclusionExtension Pass (LegacyRP)";

        /// <summary>
        /// The command buffer attached to the AR camera in order to suppress semantic channels.
        /// </summary>
        private CommandBuffer _backgroundCommandBuffer;

        /// <summary>
        /// Whether the command buffer is attached to the main camera.
        /// </summary>
        private bool _areCommandBuffersAdded;

        /// <summary>
        /// Whether we are currently trying to attach the command buffer.
        /// </summary>
        private bool _isAttachingCommandBuffer;

        /// <summary>
        /// Whether the application is using the default render pipeline.
        /// </summary>
        private static bool IsUsingLegacyRenderPipeline => GraphicsSettings.currentRenderPipeline == null;

        /// <summary>
        /// ARFoundation's renderer gets added to the camera on the first AR frame, so this component needs
        ///  to wait this number of AR frames before attaching its command buffers.
        /// </summary>
        private const int k_attachDelay = 2;

        /// <summary>
        /// The number of AR frames received while waiting to attach command buffers.
        /// </summary>
        private int _numberOfARFramesReceived;

        // Required components
        private Camera _camera;
        private ARCameraManager _cameraManager;
        private XROcclusionSubsystem _occlusionSubsystem;
        private AROcclusionManager _occlusionManager;

        // Helpers
        private bool _isValidated;
        private Matrix4x4? _displayMatrix;
        private ScreenOrientation _screenOrientation;

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
        private Texture2D _defaultDepthTexture;

        /// <summary>
        /// Verifies the state of the component and enables or disables custom rendering accordingly.
        /// </summary>
        private void ConfigureRendering()
        {
            if (gameObject.activeInHierarchy && enabled &&
                (_isOcclusionSuppressionEnabled || _isOcclusionStabilizationEnabled))
            {
                // Enable rendering
                if(!_isAttachingCommandBuffer) { // Only try and attach if not already attaching
                  ScheduleCommandBufferAdditions();
                }
            }
            else
            {
                // Disable rendering
                ConfigureCameraCommandBuffers(false);
            }
        }

        /// <summary>
        /// Sets the principal virtual object being occluded in the SpecifiedGameObject occlusion mode
        /// </summary>
        /// @note This method changes the optimal occlusion distance mode setting.
        public void TrackOccludee(Renderer occludee)
        {
            if (occludee == null)
                throw new ArgumentNullException(nameof(occludee));

            _principalOccludee = occludee;
            Mode = OptimalOcclusionDistanceMode.SpecifiedGameObject;
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
        private bool _isVisualizationEnabled;

        private void Awake()
        {
            var (isLoaderInitialized, isValidSubsystemLoaded) = ValidateSubsystem();
            if (isLoaderInitialized && !isValidSubsystemLoaded)
                return;

            // Acquire components
            _camera = GetComponent<Camera>();
            _cameraManager = GetComponent<ARCameraManager>();
            _occlusionManager = GetComponent<AROcclusionManager>();
            _screenOrientation = Screen.orientation;

            ValidateOcclusionDistanceMode();
            ValidateFeatures();

            // Update the subsystem's value before it's started in the Enable tick
            TargetFrameRate = _targetFrameRate;
        }

        private void OnEnable()
        {
            // Check whether it's needed to enable the second render pass for occlusion
            if (_isOcclusionSuppressionEnabled || _isOcclusionStabilizationEnabled)
            {
                _suppressionChannelsMaskOutdated = _isOcclusionSuppressionEnabled;
                ScheduleCommandBufferAdditions();
            }
        }

        private void OnDisable()
        {
            if (_cameraManager != null && _isAttachingCommandBuffer)
              _cameraManager.frameReceived -= TryAttachCommandBuffer;

            // Disable custom background rendering
            ConfigureCameraCommandBuffers(false);
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
            }

            if (!_occlusionSubsystem.running ||
                _occlusionManager.currentOcclusionPreferenceMode == OcclusionPreferenceMode.NoOcclusion)
            {
                return;
            }

            // Acquire the depth texture
            var gpuDepth = _occlusionManager.environmentDepthTexture;
            if (gpuDepth == null)
                return;

            // Update the display transform if necessary
            if (!_displayMatrix.HasValue || _screenOrientation != Screen.orientation)
            {
                _screenOrientation = Screen.orientation;
                _displayMatrix = CameraMath.CalculateDisplayMatrix
                (
                    gpuDepth.width,
                    gpuDepth.height,
                    Screen.width,
                    Screen.height,
                    _screenOrientation
                );
            }

            UpdateOcclusionDistance();

            // Handle custom rendering
            if (_areCommandBuffersAdded || !IsUsingLegacyRenderPipeline)
            {
                // Bind depth
                _backgroundMaterial.SetTexture(s_depthTextureId, gpuDepth);
                _backgroundMaterial.SetMatrix(s_displayMatrixId, _displayMatrix.Value);

                // Set scale: this computes the affect the camera's localToWorld has on the the length of the
                // forward vector, i.e., how much farther from the camera are things than with unit scale.
                var forward = transform.localToWorldMatrix.GetColumn(2);
                var scale = forward.magnitude;
                _backgroundMaterial.SetFloat(s_cameraForwardScaleId, scale);

                // Semantic depth suppression
                if (_isOcclusionSuppressionEnabled)
                {
                    var semantics = _semanticSegmentationManager.GetPackedSemanticsChannelsTexture(out var samplerMatrix);
                    if (semantics != null)
                    {
                        if (_suppressionChannelsMaskOutdated)
                            UpdateSemanticsMask();

                        // Bind semantics
                        _backgroundMaterial.SetTexture(s_semanticsTextureId, semantics);
                        _backgroundMaterial.SetMatrix(s_semanticsTransformId, samplerMatrix);
                        _backgroundMaterial.SetInteger(s_bitMaskId, _suppressionChannelsMask);
                    }
                }

                if (_isOcclusionStabilizationEnabled)
                {
                    // Sync pose
                    _fusedDepthCamera.SetViewProjection(_camera.worldToCameraMatrix, _camera.projectionMatrix);

                    // Bind mesh depth
                    _backgroundMaterial.SetTexture(s_fusedDepthTextureId, _fusedDepthCamera.GpuTexture);
                }
            }
        }

        private void UpdateOcclusionDistance()
        {
            if (Mode != OptimalOcclusionDistanceMode.Static && _occlusionSubsystem is LightshipOcclusionSubsystem)
            {
                if (_occlusionSubsystem.TryAcquireEnvironmentDepthCpuImage(out var cpuDepth))
                {
                    ValidateOcclusionDistanceMode();

                    // Acquire sample bounds
                    var region =
                        (Mode == OptimalOcclusionDistanceMode.SpecifiedGameObject && _principalOccludee != null)
                            ? CalculateScreenRect(_principalOccludee, _camera)
                            : s_fullScreenRect;

                    // Sparsely sample depth within bounds
                    var depth = SampleSubregion(cpuDepth, _displayMatrix.Value, region);

                    // Cache result
                    OcclusionContext.Shared.OccludeeEyeDepth =
                        Mathf.Clamp(depth, k_MinimumDepthSample, k_MaximumDepthSample);

                    cpuDepth.Dispose();
                }
            }
        }

        private void OnDestroy()
        {
            // Release the command buffer
            _backgroundCommandBuffer?.Dispose();

            // Destroy the material
            if (_backgroundMaterial)
            {
                Destroy(_backgroundMaterial);
            }

            // Destroy the secondary camera
            if (_fusedDepthCamera != null)
            {
                Destroy(_fusedDepthCamera.gameObject);
            }

            // Destroy additional resources
            if (_defaultDepthTexture != null)
            {
                Destroy(_defaultDepthTexture);
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
        }

        /// <summary>
        /// Returns (isLoaderInitialized, isValidSubsystemLoaded)
        /// </summary>
        private (bool, bool) ValidateSubsystem()
        {
            if (_isValidated)
                return (true, true);

            var xrManager = XRGeneralSettings.Instance.Manager;
            if (!xrManager.isInitializationComplete)
                return (false, false);

            _occlusionSubsystem =
                xrManager.activeLoader.GetLoadedSubsystem<XROcclusionSubsystem>();

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

        private void ScheduleCommandBufferAdditions()
        {
          _numberOfARFramesReceived = 0;
          _cameraManager.frameReceived += TryAttachCommandBuffer;
        }

        private void TryAttachCommandBuffer(ARCameraFrameEventArgs args)
        {
            _isAttachingCommandBuffer = true;
            // Accumulate the number of frames
            _numberOfARFramesReceived++;
            if (_numberOfARFramesReceived > k_attachDelay)
            {
                // Enable custom background rendering
                ConfigureCameraCommandBuffers(true);

                // Stop tracking the number of frames received
                _cameraManager.frameReceived -= TryAttachCommandBuffer;
                _isAttachingCommandBuffer = false;
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
            if (addBuffers == _areCommandBuffersAdded)
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

            _areCommandBuffersAdded = addBuffers;
        }

        /// <summary>
        /// The background rendering material in use.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public Material BackgroundMaterial
        {
            get
            {
                if (_backgroundMaterial == null)
                {
                    if (UseCustomBackgroundMaterial)
                    {
                        _backgroundMaterial = CustomBackgroundMaterial;
                    }
                    else
                    {
                        var shader = Shader.Find(k_LightshipOcclusionExtensionShaderName);
                        if (shader == null)
                        {
                            throw new InvalidOperationException
                            (
                                $"Could not find shader named '{k_LightshipOcclusionExtensionShaderName}' required " +
                                "for depth processing."
                            );
                        }

                        _backgroundMaterial = new Material(shader);
                    }
                }

                return _backgroundMaterial;
            }
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

                // Bind passthrough texture by default
                BackgroundMaterial.SetTexture(s_depthTextureId, DefaultDepthTexture);
            }

            return _backgroundCommandBuffer;
        }

        private void UpdateSemanticsMask()
        {
            uint mask = 0u;
            const int bitsPerPixel = sizeof(UInt32) * 8;

            UInt32 GetChannelTextureMask(int channelIndex)
            {
                if (channelIndex is < 0 or >= bitsPerPixel)
                    return 0u;

                return 1u << (bitsPerPixel - 1 - channelIndex);
            }

            for (var i = 0; i < _suppressionChannels.Count; i++)
            {
                var cIdx = _semanticSegmentationManager.GetChannelIndex(_suppressionChannels[i]);
                mask |= GetChannelTextureMask(cIdx);
            }

            _suppressionChannelsMask = (int)mask;
        }

        #region Utils

        /// <summary>
        /// Sparsely samples the specified subregion for the closest depth value.
        /// </summary>
        private static float SampleSubregion
        (
            XRCpuImage depthImage,
            Matrix4x4 imageTransform,
            Rect region
        )
        {
            using (var data = depthImage.GetPlane(0)
                       .data.Reinterpret<float>(UnsafeUtility.SizeOf<byte>()))
            {
                // Inspect the image
                var width = depthImage.width;
                var height = depthImage.height;
                var depth = 100.0f;

                // Helpers
                const int numSamples = 5;
                var position = region.position;
                var center = region.center;
                var stepX = region.width * (1.0f / numSamples);
                var stepY = region.height * (1.0f / numSamples);

                // Sample
                var uv = new Vector2();
                for (int i = 0; i <= numSamples; i++)
                {
                    // Sample horizontally
                    uv.x = position.x + i * stepX;
                    uv.y = center.y;

                    var horizontal = data.Sample(width, height, uv, imageTransform);
                    if (horizontal < depth) depth = horizontal;

                    // Sample vertically
                    uv.x = center.x;
                    uv.y = position.y + i * stepY;

                    var vertical = data.Sample(width, height, uv, imageTransform);
                    if (vertical < depth) depth = vertical;
                }

                return depth;
            }
        }

        private static Rect CalculateScreenRect
        (
            Renderer forRenderer,
            Camera usingCamera
        )
        {
            var bounds = forRenderer.bounds;
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            Vector3[] points = {
                usingCamera.WorldToViewportPoint(new Vector3( c.x + e.x, c.y + e.y, c.z + e.z )),
                usingCamera.WorldToViewportPoint(new Vector3( c.x + e.x, c.y + e.y, c.z - e.z )),
                usingCamera.WorldToViewportPoint(new Vector3( c.x + e.x, c.y - e.y, c.z + e.z )),
                usingCamera.WorldToViewportPoint(new Vector3( c.x + e.x, c.y - e.y, c.z - e.z )),
                usingCamera.WorldToViewportPoint(new Vector3( c.x - e.x, c.y + e.y, c.z + e.z )),
                usingCamera.WorldToViewportPoint(new Vector3( c.x - e.x, c.y + e.y, c.z - e.z )),
                usingCamera.WorldToViewportPoint(new Vector3( c.x - e.x, c.y - e.y, c.z + e.z )),
                usingCamera.WorldToViewportPoint(new Vector3( c.x - e.x, c.y - e.y, c.z - e.z ))
            };

            float maxX = 0.0f;
            float maxY = 0.0f;
            float minX = 1.0f;
            float minY = 1.0f;
            for (var i = 0; i < points.Length; i++)
            {
                Vector3 entry = points[i];
                maxX = Mathf.Max(entry.x, maxX);
                maxY = Mathf.Max(entry.y, maxY);
                minX = Mathf.Min(entry.x, minX);
                minY = Mathf.Min(entry.y, minY);
            }

            maxX = Mathf.Clamp(maxX, 0.0f, 1.0f);
            minX = Mathf.Clamp(minX, 0.0f, 1.0f);
            maxY = Mathf.Clamp(maxY, 0.0f, 1.0f);
            minY = Mathf.Clamp(minY, 0.0f, 1.0f);

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        #endregion
    }
}
