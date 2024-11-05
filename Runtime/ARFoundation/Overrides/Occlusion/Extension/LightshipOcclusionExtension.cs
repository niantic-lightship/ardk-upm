// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Common;
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
using UnityEngine.Serialization;

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// This component allows configuration of the additional functionality available in
    /// Lightship's implementation of <see cref="XROcclusionSubsystem"/>.
    /// </summary>
    [PublicAPI("apiref/Niantic/Lightship/AR/Occlusion/LightshipOcclusionExtension/")]
    [RequireComponent(typeof(ARCameraBackground))]
    [RequireComponent(typeof(AROcclusionManager))]
    [DefaultExecutionOrder(ARUpdateOrder.k_OcclusionManager - 1)]
    public partial class LightshipOcclusionExtension : ConditionalRenderer
    {
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
        [HideInInspector]
        private bool _useCustomMaterial;

        [FormerlySerializedAs("_customBackgroundMaterial")]
        [SerializeField]
        private Material _customMaterial;

        [SerializeField]
        private bool _bypassOcclusionManagerUpdates;

        [SerializeField]
        [Tooltip("Allow the occlusion extension to override the occlusion manager's settings to set "
            + "the most optimal configuration (see documentation for more details).")]
        private bool _overrideOcclusionManagerSettings = true;

        [SerializeField]
        private ARSemanticSegmentationManager _semanticSegmentationManager;

        [SerializeField]
        private ARMeshManager _meshManager;

        #endregion

        #region Private Fields

        // Required components
        private XROcclusionSubsystem _occlusionSubsystem;
        private AROcclusionManager _occlusionManager;

        // Resources
        private XRCpuImage _cpuDepth;
        private Texture2D _gpuDepth;
        private Texture2D _gpuSuppression;
        private Texture2D _defaultDepthTexture;
        private Texture2D _defaultNonLinearDepthTexture;
        private Matrix4x4 _depthTransform;
        private LightshipFusedDepthCamera _fusedDepthCamera;

        // The number of AR frames received while waiting to attach command buffers.
        private int _numberOfARFramesReceived;

        // Additional helpers
        private bool _isValidated;
        private bool _isVisualizationEnabled;
        private bool _showedTargetFrameRateNotSupportedMessage;

        #endregion

        #region Properties

        protected override string ShaderName
        {
            get => DefaultShaderName;
        }

        protected override string RendererName
        {
            get => k_CustomRenderPassName;
        }

        /// <summary>
        /// The occlusion extension pass needs to run after the background is drawn.
        /// </summary>
        private readonly string[] _externalPassDependencies = {"AR Background"};

        /// <summary>
        /// Invoked to query the external command buffers that need to run before our own.
        /// </summary>
        /// <param name="evt">The camera event to search command buffers for.</param>
        /// <returns>Names or partial names of the command buffers.</returns>
        protected override string[] OnRequestExternalPassDependencies(CameraEvent evt)
        {
            return _externalPassDependencies;
        }

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
                        Material.EnableKeyword(k_DebugViewFeature);
                    }
                    else
                    {
                        Material.DisableKeyword(k_DebugViewFeature);
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
                        Material.EnableKeyword(k_DepthEdgeSmoothingFeature);
                    }
                    else
                    {
                        Material.DisableKeyword(k_DepthEdgeSmoothingFeature);
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
            // Low resolution images are produced using lightship or lidar with the fastest setting
            get => IsUsingLightshipOcclusionSubsystem ||
                _occlusionSubsystem.currentEnvironmentDepthMode == EnvironmentDepthMode.Fastest;
        }

        /// <summary>
        /// Returns the intrinsics matrix of the most recent depth prediction. Contains values
        /// for the camera's focal length and principal point. Converts between 2D image pixel
        /// coordinates and 3D world coordinates relative to the camera.
        /// </summary>
        /// <exception cref="System.NotSupportedException">Thrown if getting intrinsics matrix is not supported.
        /// </exception>
        public Matrix4x4? LatestIntrinsicsMatrix
        {
            get
            {
                switch (_occlusionSubsystem)
                {
                    case LightshipOcclusionSubsystem lightshipOcclusionSubsystem:
                        return lightshipOcclusionSubsystem._LatestIntrinsicsMatrix;
                    case LightshipPlaybackOcclusionSubsystem lightshipPlaybackOcclusionSubsystem:
                        return lightshipPlaybackOcclusionSubsystem._LatestIntrinsicsMatrix;
                    default:
                        Log.Warning(k_LatestIntrinsicsMatrixNotSupportedMessage);
                        return default;
                }
            }
        }

        /// <summary>
        /// Returns the extrinsics matrix of the most recent depth prediction.
        /// </summary>
        /// <exception cref="System.NotSupportedException">Thrown if getting extrinsics matrix is not supported.
        /// </exception>
        public Matrix4x4? LatestExtrinsicsMatrix
        {
            get
            {
                switch (_occlusionSubsystem)
                {
                    case LightshipOcclusionSubsystem lightshipOcclusionSubsystem:
                        return lightshipOcclusionSubsystem._LatestExtrinsicsMatrix;
                    case LightshipPlaybackOcclusionSubsystem lightshipPlaybackOcclusionSubsystem:
                        return lightshipPlaybackOcclusionSubsystem._LatestExtrinsicsMatrix;
                    default:
                        Log.Warning(k_LatestExtrinsicsMatrixNotSupportedMessage);
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

                        Material.EnableKeyword(k_OcclusionSuppressionFeature);
                        _semanticSegmentationManager.SuppressionMaskChannels = _suppressionChannels;
                    }
                    else
                    {
                        Material.DisableKeyword(k_OcclusionSuppressionFeature);
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

                        Material.EnableKeyword(k_OcclusionStabilizationFeature);
                        ToggleFusedDepthCamera(true);
                    }
                    else
                    {
                        Material.DisableKeyword(k_OcclusionStabilizationFeature);
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
            get { return Material.GetFloat(s_stabilizationThreshold); }
            set { Material.SetFloat(s_stabilizationThreshold, Mathf.Clamp(value, 0.0f, 1.0f)); }
        }

        /// <summary>
        /// Determines whether the command buffer should be attached.
        /// </summary>
        protected override bool ShouldAddCommandBuffer
        {
            get
            {
                return IsUsingLegacyRenderPipeline && IsAnyFeatureEnabled;
            }
        }

        /// <summary>
        /// Whether the second pass of background rendering is active to satisfy custom occlusion features.
        /// </summary>
        public bool IsRenderingActive
        {
            get => IsCommandBufferAdded || (!IsUsingLegacyRenderPipeline && IsAnyFeatureEnabled);
        }

        /// <summary>
        /// Whether any feature provided by the occlusion extension is enabled.
        /// </summary>
        private bool IsAnyFeatureEnabled
        {
            get
            {
                return _isOcclusionSuppressionEnabled || _isOcclusionStabilizationEnabled || _preferSmoothEdges
                    || (_optimalOcclusionDistanceMode != OptimalOcclusionDistanceMode.Static &&
                        IsUsingLightshipOcclusionSubsystem);
            }
        }

        /// <summary>
        /// Determines whether the occlusion manager is using the Lightship Occlusion Subsystem.
        /// Caching this helps to avoid redundant type casting.
        /// </summary>
        private bool IsUsingLightshipOcclusionSubsystem
        {
            get
            {
                _usingLightshipOcclusionSubsystem ??= _occlusionSubsystem != null
                    ? _occlusionSubsystem is LightshipOcclusionSubsystem
                    : null;

                return _usingLightshipOcclusionSubsystem ?? false;
            }
        }
        private bool? _usingLightshipOcclusionSubsystem;

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
        /// Whether to override the occlusion manager's settings to set the most optimal configuration
        /// for the occlusion extension.
        /// Currently, the following overrides are applied:
        /// 1) On iPhone devices with Lidar sensor, the best and medium occlusion mode will cause a significant performance hit
        /// as well as a crash. We will override the occlusion mode to fastest to avoid this issue and enable smooth edges
        /// for the best results.
        /// </summary>
        public bool OverrideOcclusionManagerSettings
        {
            get => _overrideOcclusionManagerSettings;
            set
            {
               _overrideOcclusionManagerSettings = value;
            }
        }

        /// <summary>
        /// Returns the depth texture used to render occlusions.
        /// </summary>
        public Texture2D DepthTexture { get; private set; }

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
        /// It lets every pixel pass through until the real depth texture is ready to use.
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
                    var maxDistance = Camera.farClipPlane;
                    _defaultDepthTexture = new Texture2D(2, 2, TextureFormat.RFloat, mipChain: false);
                    _defaultDepthTexture.SetPixelData(new[] {maxDistance, maxDistance, maxDistance, maxDistance}, 0);
                    _defaultDepthTexture.Apply(false);
                }

                return _defaultDepthTexture;
            }
        }

        /// <summary>
        /// The default texture bound to the fused depth property on the material.
        /// It lets every pixel pass through until the real depth texture is ready to use.
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
                    var val = OcclusionExtensionUtils.IsDepthReversed() ? 0.0f : 1.0f;
                    _defaultNonLinearDepthTexture = new Texture2D(2, 2, TextureFormat.RFloat, mipChain: false);
                    _defaultNonLinearDepthTexture.SetPixelData(new[] {val, val, val, val}, 0);
                    _defaultNonLinearDepthTexture.Apply(false);
                }

                return _defaultNonLinearDepthTexture;
            }
        }

        /// <summary>
        /// Get or set the material used for rendering the fused depth texture.
        /// This is relevant when the occlusion stabilization feature is enabled.
        /// The shader used by this material should output metric eye depth.
        /// </summary>
        public Material FusedDepthMaterial
        {
            get
            {
                return _fusedDepthCamera == null ? null : _fusedDepthCamera.Material;
            }

            set
            {
                if (_fusedDepthCamera != null)
                {
                    _fusedDepthCamera.Material = value;
                }
                else
                {
                    Log.Error(k_MissingFusedDepthCameraMessage);
                }
            }
        }

        /// <summary>
        /// Get or set the custom material used for processing the AR background depth buffer.
        /// </summary>
        public Material CustomMaterial
        {
            get => _customMaterial;
            set
            {
                OverrideMaterial(value);
                _customMaterial = value;
            }
        }

        [Obsolete("Use the CustomMaterial property instead.")]
        public Material CustomBackgroundMaterial
        {
            get => CustomMaterial;
            set => CustomMaterial = value;
        }

        [Obsolete("Use the Material property instead.")]
        public Material BackgroundMaterial
        {
            get => Material;
        }

        [Obsolete("Use the CustomBackgroundMaterial property instead. Assign null to use the default material.")]
        public bool UseCustomBackgroundMaterial
        {
            get => _customMaterial != null;
            set
            {
                if (value)
                {
                    if (_customMaterial == null)
                    {
                        Log.Error(k_MissingCustomBackgroundMaterialMessage);
                        return;
                    }

                    OverrideMaterial(_customMaterial);
                }
                else
                {
                    _customMaterial = null;
                    OverrideMaterial(null);
                }
            }
        }

        #endregion

        protected override bool OnAddRenderCommands(CommandBuffer cmd, Material mat)
        {
            cmd.Blit(null, BuiltinRenderTextureType.CameraTarget, mat);
            return true;
        }

        protected override void OnInitializeMaterial(Material mat)
        {
            // Bind pass-through texture by default
            mat.SetTexture(s_depthTextureId, DefaultDepthTexture);
            mat.SetTexture(s_fusedDepthTextureId, DefaultNonLinearDepthTexture);
        }

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

        protected override void Awake()
        {
            base.Awake();

            // Acquire components
            _occlusionManager = GetComponent<AROcclusionManager>();

            // Acquire the subsystem
            if (ValidateSubsystem() == (true, true))
            {
                ReconfigureSubsystem();
            }

            // Validate settings
            ValidateOcclusionDistanceMode();
            ValidateFeatures();
            ValidateRenderSettings();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

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

        protected override void Update()
        {
            base.Update();

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

            if (_overrideOcclusionManagerSettings)
            {
                ApplyOverrideOcclusionManagerSettings();
            }


            if (!_occlusionSubsystem.running ||
                _occlusionManager.currentOcclusionPreferenceMode == OcclusionPreferenceMode.NoOcclusion)
            {
                return;
            }

            // Determine the best depth to use
            if (FetchDepthData(out var depth))
            {
                // Assign the depth texture
                DepthTexture = depth;

                // Run update logic
                HandleOcclusionDistance();
                HandleOcclusionRendering(DepthTexture);
            }
            else
            {
                // No depth available, clear the depth texture
                DepthTexture = null;
            }
        }

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
                        destinationFilter: FilterMode.Bilinear,
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
                    // By creating a texture from the cpu image and retaining ourselves, the
                    // image becomes stable. This issue is probably due to the changes introduced
                    // in iOS 16 where the metal command buffer do not implicitly retain textures.
                    if (_occlusionSubsystem.currentEnvironmentDepthMode == EnvironmentDepthMode.Fastest)
                    {
                        // Update the depth texture from the cpu image
                        var gotGpuImage = ImageSamplingUtils.CreateOrUpdateTexture(
                            source: _cpuDepth,
                            destination: ref _gpuDepth,
                            destinationFilter: FilterMode.Bilinear,
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
            if (Mode == OptimalOcclusionDistanceMode.Static || !IsUsingLightshipOcclusionSubsystem)
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
                    region = OcclusionExtensionUtils.CalculateScreenRect(_principalOccludee, Camera);
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
            var depth = OcclusionExtensionUtils.SampleImageSubregion(_cpuDepth, _depthTransform, region);

            // Cache result
            XRDisplayContext.OccludeeEyeDepth = Mathf.Clamp(depth, k_MinimumDepthSample, k_MaximumDepthSample);
        }

        private void HandleOcclusionRendering(Texture2D depthTexture)
        {
            // Handle custom rendering
            if (!IsRenderingActive)
            {
                return;
            }

            // Acquire the material in use
            var material = Material;
            var unityCamera = Camera;

            // Bind depth
            material.SetTexture(s_depthTextureId, depthTexture);
            material.SetMatrix(s_depthTransformId, _depthTransform);

            // Set scale: this computes the affect the camera's localToWorld has on the length of the
            // forward vector, i.e., how much farther from the camera are things than with unit scale.
            var forward = unityCamera.transform.localToWorldMatrix.GetColumn(2);
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
                _fusedDepthCamera.SetViewProjection(unityCamera.worldToCameraMatrix, unityCamera.projectionMatrix);
                material.SetTexture(s_fusedDepthTextureId, _fusedDepthCamera.GpuTexture);
            }

            if (_preferSmoothEdges)
            {
                var width = depthTexture.width;
                var height = depthTexture.height;
                material.SetVector(s_depthTextureParams,
                    new Vector4(1.0f / width, 1.0f / height, width, height));
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

            _usingLightshipOcclusionSubsystem = null;
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
        /// Ensures that the render settings are correctly configured.
        /// </summary>
        private void ValidateRenderSettings()
        {
            // Apply custom material
            if (_customMaterial != null)
            {
                OverrideMaterial(_customMaterial);
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

            var material = Material;
            if (material == null)
            {
                Log.Error(k_MissingShaderResourceMessage);
                return;
            }

            // Recompile the shader for suppression
            if (_isOcclusionSuppressionEnabled)
            {
                material.EnableKeyword(k_OcclusionSuppressionFeature);
            }
            else
            {
                material.DisableKeyword(k_OcclusionSuppressionFeature);
            }

            // Recompile the shader for stabilization
            if (_isOcclusionStabilizationEnabled)
            {
                material.EnableKeyword(k_OcclusionStabilizationFeature);
                ToggleFusedDepthCamera(true);
            }
            else
            {
                material.DisableKeyword(k_OcclusionStabilizationFeature);
                ToggleFusedDepthCamera(false);
            }

            // Recompile the shader for depth compression
            if (_preferSmoothEdges && EligibleForEdgeSmoothing)
            {
                material.EnableKeyword(k_DepthEdgeSmoothingFeature);
            }
            else
            {
                material.DisableKeyword(k_DepthEdgeSmoothingFeature);

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
                _fusedDepthCamera.Configure(Camera, meshLayer: _meshManager.meshPrefab.gameObject.layer);
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
                    IsRenderingActive && _bypassOcclusionManagerUpdates;
            }
        }


        /// <summary>
        /// As we discover some limitations with the ARFoundation occlusion manager, we may need to override
        /// some of its settings to ensure that the occlusion extension works optimally
        /// This method will be the central place where these settings are overridden.
        /// </summary>
        private void ApplyOverrideOcclusionManagerSettings()
        {
            if (_occlusionManager == null || _occlusionSubsystem == null)
            {
                return;
            }

            // On devices with Lidar sensor, the best and medium occlusion mode will cause a significant performance hit
            // as well as a crash. We will override the occlusion mode to fastest to avoid this issue and enable smooth edges
            // for the best results.
        #if UNITY_IOS
            if (_occlusionSubsystem is not LightshipOcclusionSubsystem &&
                (_occlusionManager.currentEnvironmentDepthMode == EnvironmentDepthMode.Medium ||
                 _occlusionManager.currentEnvironmentDepthMode == EnvironmentDepthMode.Best))
            {
                _occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
                PreferSmoothEdges = true;
            }
        #endif

        }
    }
}
