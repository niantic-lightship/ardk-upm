// Copyright 2022-2025 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Occlusion.Features;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Semantics;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.Subsystems.Semantics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;
using Unity.XR.CoreUtils;
using UnityEngine.Serialization;

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// This component allows configuration of the additional functionality available in
    /// Lightship's implementation of <see cref="XROcclusionSubsystem"/>.
    /// </summary>
    [PublicAPI("apiref/Niantic/Lightship/AR/Occlusion/LightshipOcclusionExtension/")]
    [RequireComponent(typeof(AROcclusionManager))]
    [DefaultExecutionOrder(ARUpdateOrder.k_OcclusionManager - 1)]
#if ARF_6_0_OR_NEWER
    [Obsolete("This component is not yet supported in combination with AR Foundation version 6.0 or later.")]
#endif
    public partial class LightshipOcclusionExtension : CompositeRenderer
    {
        [SerializeField]
        [Tooltip("Frame rate that depth inference will aim to run at")]
        [Range(1, 90)]
        private uint _targetFrameRate = LightshipOcclusionSubsystem.MaxRecommendedFrameRate;

        [SerializeField]
        [Tooltip("The preferred technique used for occluding virtual objects.")]
        private OcclusionTechnique _occlusionTechnique = OcclusionTechnique.Automatic;

        [SerializeField]
        [Tooltip("The current method used for determining the distance at which occlusions "
            + "will have the best visual quality.\n\n"
            + "ClosestOccluder -- average depth from the whole field of view.\n"
            + "SpecifiedGameObject -- depth of the specified object.")]
        private OptimalOcclusionDistanceMode _optimalOcclusionDistanceMode =
            OptimalOcclusionDistanceMode.ClosestOccluder;

        [SerializeField]
        [Tooltip("The principal virtual object being occluded in SpecifiedGameObject mode. "
            + "Occlusions will look most effective for this object, so it should be the "
            + "visual focal point of the experience.")]
        private Renderer _principalOccludee;

        [FormerlySerializedAs("_suppressionChannels")]
        [SerializeField]
        [Tooltip("The list of semantic channels to be suppressed in the depth buffer. "
            + "Pixels classified as any of these channels will not occlude virtual objects.")]
        private List<string> _requestedSuppressionChannels = new();

        [SerializeField]
        [HideInInspector]
        private bool _useCustomMaterial;

        [FormerlySerializedAs("_customBackgroundMaterial")]
        [SerializeField]
        private Material _customMaterial;

        [FormerlySerializedAs("_bypassOcclusionManagerUpdates")]
        [SerializeField]
        private bool _requestBypassOcclusionManagerUpdates;
        private bool _currentBypassOcclusionManagerUpdates;

        [SerializeField]
        [Tooltip("Allow the occlusion extension to override the occlusion manager's settings to set "
            + "the most optimal configuration (see documentation for more details).")]
        private bool _overrideOcclusionManagerSettings = true;

        [SerializeField]
        private ARMeshManager _meshManager;

        // Required components
        private AROcclusionManager _occlusionManager;

        // AR Foundation components and subsystems
        private XROrigin _xrOrigin;
        private XRCameraSubsystem _cameraSubsystem;
        private XROcclusionSubsystem _occlusionSubsystem;
        private XRSemanticsSubsystem _semanticsSubsystem;
        private LightshipOcclusionSubsystem _lightshipOcclusionSubsystem;
        private LightshipPlaybackOcclusionSubsystem _lightshipPlaybackOcclusionSubsystem;

        // State
        private bool _isInitialized;
        private bool _silenceMissingCPUImageMessage;
        private bool _silenceAddRenderCommandsWarnings;
        private OcclusionPreferenceMode? _preferredManagerSetting;

        #region Properties

        /// <summary>
        /// The name of the shader used by the rendering material.
        /// </summary>
        protected override string ShaderName
        {
            get
            {
                return _occlusionTechnique switch
                {
                    // Using the occlusion mesh technique
                    OcclusionTechnique.OcclusionMesh => OcclusionMesh.RequiredShaderName,

                    // Default to the z-buffer technique
                    _ => ZBufferOcclusion.RequiredShaderName
                };
            }
        }

        /// <summary>
        /// The name of the occlusion extension command buffer.
        /// </summary>
        protected override string RendererName => "LightshipOcclusionExtension Pass (LegacyRP)";

        /// <summary>
        /// Determines whether the command buffer should be attached to the camera.
        /// </summary>
        protected override bool ShouldAddCommandBuffer => IsUsingLegacyRenderPipeline && IsAnyFeatureEnabled;

        /// <summary>
        /// Whether the second pass of background rendering is active to satisfy custom occlusion features.
        /// </summary>
        public bool IsRenderingActive => IsCommandBufferAdded || (!IsUsingLegacyRenderPipeline && IsAnyFeatureEnabled);

        /// <summary>
        /// The occlusion extension command buffer needs to run after the AR Background command buffer.
        /// </summary>
        protected override string[] OnRequestExternalPassDependencies(CameraEvent evt)
        {
#if !UNITY_EDITOR && NIANTIC_LIGHTSHIP_ML2_ENABLED
            // We don't expect built-in command buffers (e.g. background rendering) on ML2
            return null;
#endif
            // On some platforms, the ARCameraBackground component is not present,
            // so we need only add it as a dependency when it is in use.
            return GetComponent<ARCameraBackground>() != null ? new[] {"AR Background"} : null;
        }

        protected override bool OnAddRenderCommands(CommandBuffer cmd, Material mat)
        {
            switch (_occlusionTechnique)
            {
                case OcclusionTechnique.Automatic:
                    Log.Warning(
                        "Occlusion technique is not determined at the time of constructing the command buffer.");
                    return false;

                case OcclusionTechnique.ZBuffer:
                    cmd.Blit(null, BuiltinRenderTextureType.CameraTarget, mat);
                    return true;

                case OcclusionTechnique.OcclusionMesh:
                    var mesh = (_occlusionComponent as OcclusionMesh)?.Mesh;
                    if (mesh == null)
                    {
                        if (!_silenceAddRenderCommandsWarnings)
                        {
                            Log.Warning("OnAddRenderCommands: No occlusion mesh available.");
                            _silenceAddRenderCommandsWarnings = true;
                        }
                        return false;
                    }

                    _silenceAddRenderCommandsWarnings = false;
                    cmd.DrawMesh(mesh, Matrix4x4.identity, mat);
                    return true;

                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// The current occlusion technique in use.
        /// </summary>
        internal OcclusionTechnique CurrentOcclusionTechnique => _occlusionTechnique;

        /// <summary>
        /// Determines whether the occlusion manager is using the Lightship Occlusion Subsystem.
        /// </summary>
        private bool IsUsingLightshipOcclusionSubsystem => _lightshipOcclusionSubsystem != null;

        /// <summary>
        /// Determines whether the <see cref="TargetFrameRate"/> API is supported with the current configuration.
        /// </summary>
        public bool SupportsTargetFrameRate => IsUsingLightshipOcclusionSubsystem;

        /// <summary>
        /// The framerate that depth inference will aim to run at. Setting the value to 0 will result
        /// in using the recommended frame rate.
        /// Call <see cref="SupportsTargetFrameRate"/> to check if the target frame rate is supported.
        /// </summary>
        public uint TargetFrameRate
        {
            get => IsUsingLightshipOcclusionSubsystem ? _lightshipOcclusionSubsystem.TargetFrameRate : _targetFrameRate;
            set
            {
                _targetFrameRate = value <= 0u ? LightshipOcclusionSubsystem.MaxRecommendedFrameRate : value;
                if (IsUsingLightshipOcclusionSubsystem)
                {
                    _lightshipOcclusionSubsystem.TargetFrameRate = _targetFrameRate;
                }
            }
        }

        /// <summary>
        /// Returns the intrinsics matrix for <see cref="DepthTexture"/>. Contains values for the
        /// camera's focal length and principal point. Converts between 2D image pixel coordinates
        /// and 3D world coordinates relative to the camera.
        /// </summary>
        public Matrix4x4? LatestIntrinsicsMatrix
        {
            get
            {
                // Using inferred depth (cropped aspect ratio)
                if (_lightshipOcclusionSubsystem != null)
                {
                    return _lightshipOcclusionSubsystem.LatestIntrinsicsMatrix;
                }

                // Using playback depth (possibly cropped aspect ratio)
                if (_lightshipPlaybackOcclusionSubsystem != null)
                {
                    return _lightshipPlaybackOcclusionSubsystem.LatestIntrinsicsMatrix;
                }

                // Using platform depth (aspect ratio matches camera image)
                if (_cameraSubsystem != null)
                {
                    var texture = _occlusionManager.environmentDepthTexture;
                    if (texture != null && _cameraSubsystem.TryGetIntrinsics(out var intrinsicsData))
                    {
                        var res = new Vector2Int(texture.width, texture.height);
                        Matrix4x4 intrinsics = Matrix4x4.identity;

                        // Scale camera intrinsics to match the depth texture resolution
                        intrinsics[0, 0] = intrinsicsData.focalLength.x / intrinsicsData.resolution.x * res.x;
                        intrinsics[1, 1] = intrinsicsData.focalLength.y / intrinsicsData.resolution.y * res.y;
                        intrinsics[0, 2] = intrinsicsData.principalPoint.x / intrinsicsData.resolution.x * res.x;
                        intrinsics[1, 2] = intrinsicsData.principalPoint.y / intrinsicsData.resolution.y * res.y;

                        return intrinsics;
                    }
                }

                return default;
            }
        }

        /// <summary>
        /// Returns the extrinsics matrix for <see cref="DepthTexture"/>.
        /// </summary>
        public Matrix4x4? LatestExtrinsicsMatrix
        {
            get
            {
                // Using inferred depth (pose is associated with the depth prediction)
                if (_lightshipOcclusionSubsystem != null)
                {
                    return _lightshipOcclusionSubsystem.LatestExtrinsicsMatrix;
                }

                // Using playback depth (pose is associated with the depth prediction)
                if (_lightshipPlaybackOcclusionSubsystem != null)
                {
                    return _lightshipPlaybackOcclusionSubsystem.LatestExtrinsicsMatrix;
                }

                // Using platform depth (use latest camera pose)
                var displayRotation = CameraMath.CameraToDisplayRotation(XRDisplayContext.GetScreenOrientation());
                return Camera.transform.localToWorldMatrix * Matrix4x4.Rotate(displayRotation);
            }
        }

        /// <summary>
        /// Get or set the current mode in use for determining the distance at which occlusions
        /// will have the best visual quality.
        /// </summary>
        public OptimalOcclusionDistanceMode OcclusionDistanceMode
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
        /// Whether to disable automatically updating the depth texture of the occlusion manager.
        /// This feature can be used to avoid redundant texture operations since depth is ultimately
        /// going to be overriden by the Lightship Occlusion Extension anyway. Not using this setting
        /// may result in undesired synchronization with the rendering thread that impacts performance.
        /// </summary>
        public bool BypassOcclusionManagerUpdates
        {
            get => _currentBypassOcclusionManagerUpdates;
            set
            {
                if (!_isInitialized)
                {
                    Log.Error("Cannot set BypassOcclusionManagerUpdates before initialization.");
                    return;
                }

                // We'll attempt to fulfill the request whitin the current
                // call, but the actual setting may not be applied until
                // the next frame, due to the delayed start of rendering
                // and this setting's dependency on it.
                _requestBypassOcclusionManagerUpdates = value;

                // Not allowed without using the Lightship Occlusion Subsystem
                if (!IsUsingLightshipOcclusionSubsystem)
                {
                    // Cancel the request
                    _currentBypassOcclusionManagerUpdates = false;
                    _requestBypassOcclusionManagerUpdates = false;
                    return;
                }

                // Only skip occlusion manager texture updates if the occlusion extension is rendering
                var disableFetchTextureDescriptors = value && IsRenderingActive;

                // Apply the setting
                if (_lightshipOcclusionSubsystem.RequestDisableFetchTextureDescriptors !=
                    disableFetchTextureDescriptors)
                {
                    _lightshipOcclusionSubsystem.RequestDisableFetchTextureDescriptors = disableFetchTextureDescriptors;
                }

                _currentBypassOcclusionManagerUpdates =
                    _lightshipOcclusionSubsystem.RequestDisableFetchTextureDescriptors;
            }
        }

        /// <summary>
        /// Whether to override the occlusion manager's settings to set the most optimal configuration
        /// for the occlusion extension. Currently, the following overrides are applied:
        /// 1) On iPhone devices with Lidar sensor, the best and medium occlusion mode will cause a
        /// significant performance hit as well as a crash. We will override the occlusion mode to
        /// fastest to avoid this issue and enable smooth edges for the best results.
        /// 2) The occlusion preference mode is set to NoOcclusion when the occlusion extension is active.
        /// </summary>
        public bool OverrideOcclusionManagerSettings
        {
            get => _overrideOcclusionManagerSettings;
            set { _overrideOcclusionManagerSettings = value; }
        }

        /// <summary>
        /// Returns the raw depth texture used in rendering.
        /// </summary>
        public Texture2D DepthTexture
        {
            get => _occlusionComponent?.GPUDepth;
        }

        /// <summary>
        /// Returns a transform for converting between normalized image coordinates and a coordinate space
        /// appropriate for rendering <see cref="DepthTexture"/> on the viewport.
        /// </summary>
        public Matrix4x4 DepthTransform
        {
            get { return _occlusionComponent?.DepthTransform ?? Matrix4x4.identity; }
        }

        /// <summary>
        /// Get or set the custom material used for processing the AR background depth buffer.
        /// If set to null, the default material will be used.
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

        #endregion

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
            var cpuDepth = _occlusionComponent?.CPUDepth;
            if (cpuDepth is not {valid: true})
            {
                Log.Info(k_MissingCpuImageMessage);
                depth = float.NaN;
                return false;
            }

            // Acquire the image data
            var data = ImageSamplingUtils.AcquireDataBuffer<float>(cpuDepth.Value);
            if (!data.HasValue)
            {
                depth = float.NaN;
                return false;
            }

            // Sample the image
            var uv = new Vector2(screenX / (float)Screen.width, screenY / (float)Screen.height);
            depth = data.Value.Sample(cpuDepth.Value.width, cpuDepth.Value.height, uv,
                _occlusionComponent.DepthTransform);

            // Success
            return true;
        }

        /// <summary>
        /// Sets the principal virtual object being occluded in the SpecifiedGameObject occlusion mode.
        /// <remarks>This method changes the optimal occlusion distance mode setting.</remarks>>
        /// </summary>
        public void TrackOccludee(Renderer occludee)
        {
            if (occludee == null)
            {
                throw new ArgumentNullException(nameof(occludee));
            }

            _principalOccludee = occludee;
            OcclusionDistanceMode = OptimalOcclusionDistanceMode.SpecifiedGameObject;
        }

        protected override void Awake()
        {
            base.Awake();

            // Acquire components
            _occlusionManager = GetComponent<AROcclusionManager>();

            // Initialization should succeed in awake in most cases
            _isInitialized = TryInitialize();
        }

        protected override void Update()
        {
            base.Update();

            // If automatic XR loading is enabled, then subsystems will be available before the Awake call.
            // However, if XR loading is done manually, then this component needs to check every Update call
            // if a compatible XROcclusionSubsystem has been loaded.
            if (!_isInitialized)
            {
                if ((_isInitialized = TryInitialize()) == false)
                {
                    return;
                }
            }

            // Maintain occlusion enhancing render components
            ValidateRenderComponents();

            // Update the occlusion manager settings
            HandleOcclusionManagerSettings();

            // Update the back-projection plane distance for 2D interpolation
            HandleOcclusionDistance();
        }

        /// <summary>
        /// As we discover some limitations with the ARFoundation occlusion manager, we may need to override
        /// some of its settings to ensure that the occlusion extension works optimally. This method will be
        /// the central place where these settings are overridden.
        /// </summary>
        private void HandleOcclusionManagerSettings()
        {
            // Get the current rendering state
            var isRenderingActive = IsRenderingActive;

            // Apply disable fetch texture descriptors setting
            if (_currentBypassOcclusionManagerUpdates && !isRenderingActive)
            {
                // Let the manager update its textures
                BypassOcclusionManagerUpdates = false;

                // Remember to enable this when rendering becomes active again
                _requestBypassOcclusionManagerUpdates = true;
            }
            else if (_currentBypassOcclusionManagerUpdates != _requestBypassOcclusionManagerUpdates)
            {
                // Apply the preferred setting
                BypassOcclusionManagerUpdates = _requestBypassOcclusionManagerUpdates;
            }

            if (!_overrideOcclusionManagerSettings)
            {
                return;
            }

            // Cache the original occlusion manager preference setting
            _preferredManagerSetting ??= _occlusionManager.requestedOcclusionPreferenceMode;

            // Determine the optimal occlusion preference mode
            var preferredLightshipSetting =
                isRenderingActive ? OcclusionPreferenceMode.NoOcclusion : _preferredManagerSetting.Value;

            // Apply the setting
            if (_occlusionManager.requestedOcclusionPreferenceMode != preferredLightshipSetting)
            {
                _occlusionManager.requestedOcclusionPreferenceMode = preferredLightshipSetting;
            }

            // On devices with Lidar sensor, the best and medium occlusion mode will cause a significant
            // performance hit as well as a crash. We will override the occlusion mode to fastest to
            // avoid this issue.
#if UNITY_IOS
            if (!IsUsingLightshipOcclusionSubsystem &&
                _occlusionManager.currentEnvironmentDepthMode is EnvironmentDepthMode.Medium
                    or EnvironmentDepthMode.Best)
            {
                _occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
            }
#endif
        }

        /// <summary>
        /// Determines and sets the global back-projection plane distance for 2D interpolation.
        /// </summary>
        private void HandleOcclusionDistance()
        {
            // In case the occlusion distance mode is set to static, we don't need to update the occlusion distance
            if (OcclusionDistanceMode == OptimalOcclusionDistanceMode.Static || !IsUsingLightshipOcclusionSubsystem)
            {
                return;
            }

            // Get the depth image on cpu to sample
            var cpuDepth = _occlusionComponent?.CPUDepth;

            // Check if we have a valid cpu image
            if (cpuDepth is not {valid: true})
            {
                if (!_silenceMissingCPUImageMessage)
                {
                    Log.Info(k_MissingCpuImageMessage);
                    _silenceMissingCPUImageMessage = true;
                }
                return;
            }
            _silenceMissingCPUImageMessage = false;

            // Acquire the image subregion to sample
            Rect region;
            if (OcclusionDistanceMode != OptimalOcclusionDistanceMode.SpecifiedGameObject)
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
                    OcclusionDistanceMode = OptimalOcclusionDistanceMode.ClosestOccluder;
                    Log.Warning(k_MissingOccludeeMessage);
                }
            }

            // Sparsely sample depth within bounds
            var depth =
                OcclusionExtensionUtils.SampleImageSubregion(cpuDepth.Value, _occlusionComponent.DepthTransform, region);

            // Cache result
            XRDisplayContext.OccludeeEyeDepth = Mathf.Clamp(depth, k_MinimumDepthSample, k_MaximumDepthSample);
        }

        private bool TryInitialize()
        {
            // Reset to defaults
            _occlusionSubsystem = null;
            _lightshipOcclusionSubsystem = null;
            _lightshipPlaybackOcclusionSubsystem = null;

            // Ensure the XR manager is initialized
            var xrManager = XRGeneralSettings.Instance.Manager;
            if (!xrManager.isInitializationComplete)
            {
                return false;
            }

            // Acquire necessary subsystems
            _occlusionSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XROcclusionSubsystem>();
            if (_occlusionSubsystem == null)
            {
                Log.Warning
                (
                    $"Destroying {typeof(LightshipOcclusionExtension).FullName} component because " +
                    $"no active {typeof(XROcclusionSubsystem).FullName} is available. " +
                    "Please ensure that a valid loader configuration exists in the XR project settings."
                );

                Destroy(this);
                return false;
            }

            // Acquire optional subsystems
            _xrOrigin = FindObjectOfType<XROrigin>();
            _cameraSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XRCameraSubsystem>();
            _semanticsSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XRSemanticsSubsystem>();
            _lightshipOcclusionSubsystem = _occlusionSubsystem as LightshipOcclusionSubsystem;
            _lightshipPlaybackOcclusionSubsystem = _occlusionSubsystem as LightshipPlaybackOcclusionSubsystem;

            // Set target frame rate
            TargetFrameRate = _targetFrameRate;

            // Verify built-in material
            if (Material == null)
            {
                Log.Error("Missing built-in material for occlusion rendering.");
            }

            // Apply external material if set
            if (_customMaterial != null)
            {
                if (_occlusionTechnique == OcclusionTechnique.Automatic)
                {
                    Log.Error("Cannot apply custom material when the occlusion technique is set to automatic. " +
                        "Please make a choice via the inspector.");
                }
                else
                {
                    OverrideMaterial(_customMaterial);
                }
            }

            // Determine the occlusion technique
            if (_occlusionTechnique == OcclusionTechnique.Automatic)
            {
#if NIANTIC_LIGHTSHIP_ML2_ENABLED
                _occlusionTechnique = OcclusionTechnique.OcclusionMesh;
#else
                _occlusionTechnique = Application.platform is
                    RuntimePlatform.WindowsEditor or RuntimePlatform.OSXEditor or
                    RuntimePlatform.IPhonePlayer or RuntimePlatform.Android
                    ? OcclusionTechnique.ZBuffer
                    : OcclusionTechnique.OcclusionMesh;
#endif
                // Dispose the current material with the (possibly) non-matching shader
                // The new material will be created in the next frame automatically
                OverrideMaterial(null);
            }

            // The occlusion mesh technique does not need depth frame interpolation
            if (_occlusionTechnique == OcclusionTechnique.OcclusionMesh)
            {
                OcclusionDistanceMode = OptimalOcclusionDistanceMode.Static;
            }

            // Ensures that the occlusion distance mode is correctly set
            if (OcclusionDistanceMode == OptimalOcclusionDistanceMode.SpecifiedGameObject && _principalOccludee == null)
            {
                // Falls back to the default technique
                OcclusionDistanceMode = OptimalOcclusionDistanceMode.ClosestOccluder;
                Log.Warning(k_MissingOccludeeMessage);
            }

            return true;
        }

        #region Deprecated APIs

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

        [Obsolete("Use the CustomMaterial property instead. Assign null to use the default material.")]
        public bool UseCustomBackgroundMaterial
        {
            get => _customMaterial != null;
            set
            {
                if (value)
                {
                    if (_customMaterial == null)
                    {
                        Log.Error("Set to use a custom background material without a valid reference.");
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

        [Obsolete("Use the StableDepthMaterial property instead.")]
        public Material FusedDepthMaterial
        {
            get => StableDepthMaterial;
            set => StableDepthMaterial = value;
        }

        [Obsolete("Use OcclusionDistanceMode instead.")]
        public OptimalOcclusionDistanceMode Mode
        {
            get => OcclusionDistanceMode;
            set => OcclusionDistanceMode = value;
        }

        [Obsolete("This constant is no longer used.")]
        public const string ZBufferOcclusionShaderName = "Lightship/ZBufferOcclusion";

        [Obsolete("This constant is no longer used.")]
        public const string OcclusionMeshShaderName = "Lightship/OcclusionMesh";

        #endregion
    }
}
