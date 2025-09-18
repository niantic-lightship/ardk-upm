// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Occlusion.Features;
using Niantic.Lightship.AR.Semantics;
using Niantic.Lightship.AR.Subsystems.Semantics;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Occlusion
{
    /// <summary>
    /// Contains logic for deploying rendering components to the occlusion extension.
    /// </summary>
    public partial class LightshipOcclusionExtension
    {
        [FormerlySerializedAs("_preferSmoothEdges")]
        [SerializeField]
        [Tooltip("When enabled, will employ bilinear sampling on the depth texture for smoother occlusion edges.")]
        private bool _requestPreferSmoothEdges;
        private bool _currentPreferSmoothEdges;

        [FormerlySerializedAs("_isOcclusionStabilizationEnabled")]
        [SerializeField]
        [Tooltip("When enabled, the occlusions use the combination of the fused mesh and depth image to "
            + "achieve more stable results while still accounting for moving objects.")]
        private bool _requestOcclusionStabilizationEnabled;
        private bool _currentOcclusionStabilizationEnabled;

        [FormerlySerializedAs("_isOcclusionSuppressionEnabled")]
        [SerializeField]
        [Tooltip("When enabled, rendering will be altered so that it virtual objects will not be occluded in "
            + "pixels that contain the specified semantic suppression channels. It is recommended to use this "
            + " feature to reduce visual artifacts such as objects being clipped by the floor or the sky.")]
        private bool _requestOcclusionSuppressionEnabled;
        private bool _currentOcclusionSuppressionEnabled;

        // Persistent features
        private OcclusionComponent _occlusionComponent;

        /// <summary>
        /// Ensures that the requested render components are present in the pipeline.
        /// <remarks>This runs in update, so any heavy calls should be guarded by state variables.</remarks>>
        /// </summary>
        private void ValidateRenderComponents()
        {
            // Deploy the occlusion feature
            if (_occlusionComponent == null)
            {
                bool didAddComponent = _occlusionTechnique == OcclusionTechnique.ZBuffer
                    ? AddRenderComponent<ZBufferOcclusion>()
                    : AddRenderComponent<OcclusionMesh>();

                // Make sure the occlusion component was added first
                // Other components don't achieve anything without it
                if (!didAddComponent)
                {
                    return;
                }
            }

            // Deploy occlusion suppression
            if (_requestOcclusionSuppressionEnabled != _currentOcclusionSuppressionEnabled)
            {
                IsOcclusionSuppressionEnabled = _requestOcclusionSuppressionEnabled;
            }

            // Deploy occlusion stabilization
            if (_requestOcclusionStabilizationEnabled != _currentOcclusionStabilizationEnabled)
            {
                IsOcclusionStabilizationEnabled = _requestOcclusionStabilizationEnabled;
            }

            // Deploy edge smoothing
            if (_requestPreferSmoothEdges != _currentPreferSmoothEdges)
            {
                PreferSmoothEdges = _requestPreferSmoothEdges;
            }
        }

        /// <summary>
        /// Invoked when a new component is being added to the renderer.
        /// </summary>
        /// <param name="component">The component to be added.</param>
        /// <returns>Whether the component is eligible to run on this renderer.</returns>
        protected override bool OnAddRenderComponent(RenderComponent component)
        {
            switch (component)
            {
                // The writing the z-buffer requires the occlusion subsystem
                case ZBufferOcclusion zBufferOcclusion:
                {
                    // Check requirements
                    if (_occlusionSubsystem == null)
                    {
                        return false;
                    }

                    // Configure the component
                    _occlusionComponent = zBufferOcclusion;
                    zBufferOcclusion.Configure(_occlusionSubsystem);
                    return true;
                }

                // Creating an occluder mesh requires the occlusion and camera subsystems
                case OcclusionMesh occlusionMesh:
                {
                    // Check requirements
                    if (_occlusionSubsystem == null || _cameraSubsystem == null)
                    {
                        return false;
                    }

                    // Configure the component
                    _occlusionComponent = occlusionMesh;
                    occlusionMesh.Configure(_occlusionSubsystem, _cameraSubsystem);
                    return true;
                }

                // The occlusion suppression feature requires the semantic segmentation manager
                case Suppression occlusionSuppression:
                {
                    if (_semanticsSubsystem == null)
                    {
                        // Attempt to find the semantics subsystem
                        var xrManager = XRGeneralSettings.Instance.Manager;
                        if (xrManager != null)
                        {
                            _semanticsSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XRSemanticsSubsystem>();
                        }
                    }

                    // The manager is required to run the semantics subsystem
                    var hasActiveManager = false;
                    if (TryGetComponent<ARSemanticSegmentationManager>(out var manager) ||
                        (manager = FindObjectOfType<ARSemanticSegmentationManager>()) != null)
                    {
                        hasActiveManager = manager.enabled;
                    }

                    if (!hasActiveManager)
                    {
                        Log.Error(k_MissingSemanticSegmentationManagerMessage);
                        return false;
                    }

                    // Configure the component
                    return occlusionSuppression.Configure(
                        _xrOrigin,
                        _semanticsSubsystem as LightshipSemanticsSubsystem,
                        _occlusionTechnique,
                        _requestedSuppressionChannels);
                }

                // The occlusion stabilization feature requires the mesh manager
                case Stabilization occlusionStabilization:
                {
                    // https://docs.unity3d.com/2022.3/Documentation/Manual/SL-CameraDepthTexture.html
                    // Direct3D 11+ (Windows), OpenGL 3+ (Mac/Linux), OpenGL ES 3.0+ (iOS), Metal (iOS)
                    // and popular consoles support depth textures.
                    if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
                    {
                        Log.Error("Occlusion stabilization is not yet supported on Vulkan devices.");
                        return false;
                    }

                    if (_meshManager == null)
                    {
                        // Attempt to find the mesh manager
                        _meshManager = FindObjectOfType<ARMeshManager>();
                    }

                    // Configure the component
                    return occlusionStabilization.Configure(
                        _occlusionTechnique,
                        Camera,
                        _meshManager,
                        _stableDepthMaterial);
                }

                case EdgeSmoothing:
                {
                    // Check requirements
                    var requirementsFailed = _occlusionComponent is not ZBufferOcclusion ||

                        // High resolution depth images do not require edge smoothing
                        (!IsUsingLightshipOcclusionSubsystem &&
                        _occlusionSubsystem.currentEnvironmentDepthMode != EnvironmentDepthMode.Fastest);

                    // Cancel the request
                    if (requirementsFailed)
                    {
                        Log.Warning(
                            "Edge smoothing is only supported when using low resolution " +
                            "depth images while employing ZBuffer occlusion technique.");
                        return false;
                    }

                    return true;
                }

                case DebugView debugView:
                    // Debug visualization is always allowed
                    debugView.Configure(_occlusionTechnique);
                    return true;

                default:
                    // Reject unknown components
                    return false;
            }
        }

        /// <summary>
        /// Whether any feature provided by the occlusion extension is enabled.
        /// </summary>
        private bool IsAnyFeatureEnabled
        {
            get
            {
                // Evaluate whether any feature is requested that requires
                // the occlusion extension shader to run on the frame
                return

                    // The occlusion mesh technique is considered to be a feature
                    _occlusionTechnique == OcclusionTechnique.OcclusionMesh ||

                    // Interpolates depth frames?
                    (_optimalOcclusionDistanceMode != OptimalOcclusionDistanceMode.Static &&
                        IsUsingLightshipOcclusionSubsystem) ||

                    // Uses occlusion enhancers?
                    HasRenderComponent<Suppression>() || HasRenderComponent<Stabilization>() ||
                    HasRenderComponent<EdgeSmoothing>();
            }
        }

        /// <summary>
        /// When enabled, the component displays the depth image used for occlusions.
        /// Note that visualization can only be used if a custom occlusion feature is
        /// active, e.g. suppression or stabilization.
        /// </summary>
        public bool Visualization
        {
            get => HasRenderComponent<DebugView>();
            set
            {
                if (value)
                {
                    AddRenderComponent<DebugView>();
                }
                else
                {
                    RemoveRenderComponent<DebugView>();
                }
            }
        }

        /// <summary>
        /// When enabled, the depth image will be sampled bilinearly during rendering.
        /// </summary>
        public bool PreferSmoothEdges
        {
            get => _currentPreferSmoothEdges;
            set
            {
                if (_currentPreferSmoothEdges != value)
                {
                    // Execute the request
                    _currentPreferSmoothEdges = value
                        ? AddRenderComponent<EdgeSmoothing>()
                        : !RemoveRenderComponent<EdgeSmoothing>();
                }

                // Sync with the inspector toggle
                _requestPreferSmoothEdges = _currentPreferSmoothEdges;
            }
        }

        /// <summary>
        /// Get or set whether semantic segmentation based occlusion suppression is enabled.
        /// </summary>
        public bool IsOcclusionSuppressionEnabled
        {
            get => _currentOcclusionSuppressionEnabled;
            set
            {
                if (_currentOcclusionSuppressionEnabled != value)
                {
                    // Execute the request
                    _currentOcclusionSuppressionEnabled = value ?
                        AddRenderComponent<Suppression>() :
                        !RemoveRenderComponent<Suppression>();
                }

                // Sync with the inspector toggle
                _requestOcclusionSuppressionEnabled = _currentOcclusionSuppressionEnabled;
            }
        }

        /// <summary>
        /// Get or set whether meshing based occlusion stabilization is enabled.
        /// </summary>
        public bool IsOcclusionStabilizationEnabled
        {
            get => _currentOcclusionStabilizationEnabled;
            set
            {
                if (_currentOcclusionStabilizationEnabled != value)
                {
                    // Execute the request
                    _currentOcclusionStabilizationEnabled = value
                        ? AddRenderComponent<Stabilization>()
                        : !RemoveRenderComponent<Stabilization>();
                }

                // Sync with the inspector toggle
                _requestOcclusionStabilizationEnabled = _currentOcclusionStabilizationEnabled;
            }
        }

        /// <summary>
        /// The stabilization threshold determines whether to prefer per-frame (0)
        /// or fused depth (1) during occlusion stabilization.
        /// </summary>
        public float StabilizationThreshold
        {
            get
            {
                return _stabilizationThreshold;
            }
            set
            {
                _stabilizationThreshold = value;
                var component = GetRenderComponent<Stabilization>();
                if (component != null)
                {
                    component.Threshold = value;
                }
            }
        }
        private float _stabilizationThreshold = 0.5f;

        /// <summary>
        /// Get or set the material used for rendering the fused depth texture.
        /// This is relevant when the occlusion stabilization feature is enabled.
        /// The shader used by this material should output metric eye depth.
        /// </summary>
        public Material StableDepthMaterial
        {
            get
            {
                return GetRenderComponent<Stabilization>()?.Material;
            }

            set
            {
                // Update the material
                _stableDepthMaterial = value;

                // Recreate the stabilization component
                if (HasRenderComponent<Stabilization>())
                {
                    RemoveRenderComponent<Stabilization>();
                    AddRenderComponent<Stabilization>();
                }
            }
        }
        private Material _stableDepthMaterial;

        /// <summary>
        /// Adds a semantic segmentation channel to the collection of channels that are suppressed in the depth buffer.
        /// </summary>
        /// <param name="channelName">Semantic segmentation channel to add</param>
        /// <returns>True if the channel was successfully added.</returns>
        public bool AddSemanticSuppressionChannel(string channelName) =>
            GetRenderComponent<Suppression>()?.AddChannel(channelName) ?? false;

        /// <summary>
        /// Removes a semantic segmentation channel, if it exists, from the collection of channels
        /// that are suppressed in the depth buffer.
        /// </summary>
        /// <param name="channelName">Semantic segmentation channel to remove.</param>
        /// <returns>True if the channel was found and removed.</returns>
        public bool RemoveSemanticSuppressionChannel(string channelName) =>
            GetRenderComponent<Suppression>()?.RemoveChannel(channelName) ?? false;

        /// <summary>
        /// Returns the occluder mesh if the occlusion mesh technique is enabled.
        /// Returns null otherwise.
        /// </summary>
        internal Mesh OccluderMesh => GetRenderComponent<OcclusionMesh>()?.Mesh;
    }
}
