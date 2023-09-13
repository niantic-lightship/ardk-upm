using System;
using System.Collections;
using System.Collections.Generic;

using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

using Niantic.Lightship.AR.ARFoundation.Occlusion;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.ARFoundation
{
    /// <summary>
    /// This component allows configuration of the additional functionality available in
    /// Lightship's implementation of <see cref="XROcclusionSubsystem"/>.
    /// The OptimalOcclusionDistanceMode defaults to <see cref="OptimalOcclusionDistanceMode.ClosestOccluder">
    /// unless a specific <see cref="_principalOccludee"> is set.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(ARCameraManager))]
    [RequireComponent(typeof(AROcclusionManager))]
    [DefaultExecutionOrder(ARUpdateOrder.k_OcclusionManager - 1)]
    public class LightshipOcclusionExtension : MonoBehaviour
    {
 #region Interpolation context fields
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
        [Range(1, 90)]
        private uint _targetFrameRate = LightshipOcclusionSubsystem.MaxRecommendedFrameRate;

        [Header("Optimal Occlusion")]
        [SerializeField]
        private OptimalOcclusionDistanceMode _optimalOcclusionDistanceMode = OptimalOcclusionDistanceMode.ClosestOccluder;

        /// <summary>
        /// Frame rate that depth inference will aim to run at
        /// </summary>
        public uint TargetFrameRate
        {
            get
            {
                if (_occlusionSubsystem is LightshipOcclusionSubsystem lightshipOcclusionSubsystem)
                {
                    return lightshipOcclusionSubsystem.TargetFrameRate;
                }

                Debug.LogWarning("TargetFrameRate is not supported on non-Lightship implementations of the XROcclusionSubsystem.");
                return 0;
            }
            set
            {
                if (value <= 0)
                {
                    Debug.LogError("Target frame rate value must be greater than zero.");
                    return;
                }

                _targetFrameRate = value;
                if (_occlusionSubsystem is LightshipOcclusionSubsystem lightshipOcclusionSubsystem)
                {
                    lightshipOcclusionSubsystem.TargetFrameRate = value;
                }
                else
                {
                    Debug.LogWarning("TargetFrameRate is not supported on non-Lightship implementations of the XROcclusionSubsystem.");
                }
            }
        }

        /// <summary>
        /// The adaption mode for the occlusion context
        /// </summary>
        public OptimalOcclusionDistanceMode Mode
        {
            get => _optimalOcclusionDistanceMode;
            set
            {
                if (value == OptimalOcclusionDistanceMode.SpecifiedGameObject && _principalOccludee == null)
                {
                    Debug.LogError
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
        private Renderer _principalOccludee;

        private AROcclusionManager _occlusionManager;
        private Matrix4x4? _displayMatrix;
        private ScreenOrientation _screenOrientation;
        private const float k_MinimumDepthSample = 0.2f;
        private const float k_MaximumDepthsample = 100.0f;

        // Used to exclude the edges of the image from 'Full Screen' Sampling
        // Objects at the edges should be omitted.
        private const float k_FullScreenSampleBorder = 0.2f;
        private static readonly Rect s_fullScreenRect =
            new Rect(k_FullScreenSampleBorder, k_FullScreenSampleBorder, 1-k_FullScreenSampleBorder, 1-k_FullScreenSampleBorder);

        private const string k_MissingOccludeeMessage =
            "Active OptimalOcclusionDistanceMode is SpecifiedGameObject but the Principal Occludee " +
            "object is null. Falling back to the ClosestOccluder mode.";

#endregion

#region Depth Suppression fields

        [Header("Depth Suppression")]
        [SerializeField]
        [Tooltip(
            "Enabling this option requires a Lightship Semantic Segmentation Manager to be present in your scene.")]
        private bool _isSemanticDepthSuppressionEnabled;

        [SerializeField]
        private ARSemanticSegmentationManager _semanticSegmentationManager;

        [SerializeField]
        private List<string> _suppressionChannels;

        /// <summary>
        /// Gets or sets whether semantic segmentation based depth suppression should be enabled.
        /// </summary>
        public bool IsDepthSuppressionEnabled
        {
            get { return _isSemanticDepthSuppressionEnabled; }

            set
            {
                if (_isSemanticDepthSuppressionEnabled != value)
                {
                    _isSemanticDepthSuppressionEnabled = value;

                    if (_isSemanticDepthSuppressionEnabled)
                    {
                        Perform
                        (
                            what: () => ToggleRendering(true),
                            after: () => _numberOfARFramesReceived > k_attachDelay,
                            onTimeout: () =>
                            {
                                Debug.LogError("Could not enable semantic depth suppression.");
                                _isSemanticDepthSuppressionEnabled = false;
                            }
                        );
                    }
                    else
                    {
                        ToggleRendering(false);
                    }
                }
            }
        }

        [SerializeField]
        private bool _useCustomBackgroundMaterial;

        [SerializeField]
        private Material _customBackgroundMaterial;

        // Material constructed from the background shader
        private Material _backgroundMaterial;

        // The background renderer
        private CommandBuffer _backgroundCommandBuffer;

        // Whether the background renderer is attached to the main camera
        private bool _isCommandBufferInUse;

        // The ARF renderer gets added to the camera on the first AR frame, we need to add our own after that
        private const int k_attachDelay = 2;
        private int _numberOfARFramesReceived;

        // Shader
        private Shader _backgroundShader;

        private static readonly int s_depthTexture = Shader.PropertyToID("_Depth");
        private static readonly int s_semanticsTexture = Shader.PropertyToID("_Semantics");
        private static readonly int s_semanticsTransform = Shader.PropertyToID("_SemanticsTransform");
        private static readonly int s_displayMatrix = Shader.PropertyToID("_DisplayMatrix");
        private static readonly int s_bitMask = Shader.PropertyToID("_BitMask");
        private static readonly int s_cameraForwardScale = Shader.PropertyToID("_UnityCameraForwardScale");

        private const string k_LightshipOcclusionExtensionShaderName = "Lightship/OcclusionExtension";
        public static readonly string occlusionExtensionShaderName = k_LightshipOcclusionExtensionShaderName;

#endregion

        // Required components
        private Camera _camera;
        private ARCameraManager _cameraManager;
        private XROcclusionSubsystem _occlusionSubsystem;

        // Helpers
        private bool _isValidated;

        private void ValidateOcclusionDistanceMode()
        {
            if (Mode == OptimalOcclusionDistanceMode.SpecifiedGameObject && _principalOccludee == null)
            {
                Debug.LogWarning(k_MissingOccludeeMessage);
                Mode = OptimalOcclusionDistanceMode.ClosestOccluder;
            }
        }

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

            if (_isSemanticDepthSuppressionEnabled)
            {
                Debug.Assert(_semanticSegmentationManager, "ARSemanticSegmentation manager is required.");
            }

            // Update the subsystem's value before it's started in the Enable tick
            TargetFrameRate = _targetFrameRate;

            if (!_backgroundShader)
            {
                _backgroundShader = Shader.Find("Lightship/OcclusionExtension");
            }
        }

        private void OnEnable()
        {
            _cameraManager.frameReceived += CameraManagerOnFrameReceived;

            // For now, background rendering is only responsible for depth suppression.
            // In the future each individual feature should trigger the custom rendering.
            if (_isSemanticDepthSuppressionEnabled)
            {
                Perform
                (
                    // Enable custom background rendering...
                    what: () => ToggleRendering(true),

                    // ... after the ARF renderer has been attached
                    after: () => _numberOfARFramesReceived > k_attachDelay
                );
            }
        }

        private void OnDisable()
        {
            if (_cameraManager != null)
                _cameraManager.frameReceived -= CameraManagerOnFrameReceived;

            // Disable custom background rendering
            ToggleRendering(false);
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
                    return;
            }

            TargetFrameRate = _targetFrameRate;
            if (Mode == OptimalOcclusionDistanceMode.Static)
                return;

            if (!_occlusionSubsystem.TryAcquireEnvironmentDepthCpuImage(out var depthBuffer))
                return;

            if (!_displayMatrix.HasValue || _screenOrientation != Screen.orientation)
            {
                _screenOrientation = Screen.orientation;
                _displayMatrix = CameraMath.CalculateDisplayMatrix
                (
                    depthBuffer.width,
                    depthBuffer.height,
                    Screen.width,
                    Screen.height,
                    _screenOrientation
                );
            }

            ValidateOcclusionDistanceMode();

            // Acquire sample bounds
            var region =
                (Mode == OptimalOcclusionDistanceMode.SpecifiedGameObject && _principalOccludee != null)
                    ? CalculateScreenRect(_principalOccludee, _camera)
                    : s_fullScreenRect;

            // Sample for warp only if we are using Lightship Depth.
            if (_occlusionSubsystem is LightshipOcclusionSubsystem)
            {
                // Sparsely sample depth within bounds
                var depth = SampleSubregion(depthBuffer, _displayMatrix.Value, region);

                // Cache result
                OcclusionContext.Shared.OccludeeEyeDepth =
                    Mathf.Clamp(depth, k_MinimumDepthSample, k_MaximumDepthsample);

            }

            // Handle custom rendering
            if (_isCommandBufferInUse)
            {
                // Semantic depth suppression
                if (_isSemanticDepthSuppressionEnabled &&
                    _semanticSegmentationManager.TryGetPackedSemanticsChannelsTexture(out var semantics,
                        out var samplerMatrix))
                {
                    _backgroundMaterial.SetTexture(s_depthTexture, _occlusionManager.environmentDepthTexture);
                    _backgroundMaterial.SetTexture(s_semanticsTexture, semantics);
                    _backgroundMaterial.SetMatrix(s_semanticsTransform, samplerMatrix);
                    _backgroundMaterial.SetMatrix(s_displayMatrix, _displayMatrix.Value);

                    // Update the semantics mask
                    var channelTextureMask = (int) UpdateSemanticsMask(_suppressionChannels);
                    _backgroundMaterial.SetInteger(s_bitMask, channelTextureMask);

                    // Set scale: this computes the affect the camera's localToWorld has on the the length of the
                    // forward vector, i.e., how much farther from the camera are things than with unit scale.
                    var forward = transform.localToWorldMatrix.GetColumn(2);
                    var scale = forward.magnitude;
                    _backgroundMaterial.SetFloat(s_cameraForwardScale, scale);
                }
            }

            // FIXME: This XRCPUImage gets cleaned up if we are using Lightship Depth, but doesn't get cleaned up
            // if we are using platform i.e. ARKit depth. We need to remove this conditional and handle the buffer
            // cleanup consistently for all platforms.
            if (_occlusionSubsystem is not LightshipOcclusionSubsystem)
            {
                depthBuffer.Dispose();
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
        }

        /// Sets the main occludee used to adjust interpolation preference for.
        /// @note This method changes the adaption mode setting.
        public void TrackOccludee(Renderer occludee)
        {
            if (occludee == null)
                throw new ArgumentNullException(nameof(occludee));

            _principalOccludee = occludee;
            Mode = OptimalOcclusionDistanceMode.SpecifiedGameObject;
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
                Debug.LogWarning
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
        /// Attaches or detaches the background rendering command buffer to the main camera.
        /// </summary>
        /// <param name="isEnabled">Whether background rendering should be enabled.</param>
        private void ToggleRendering(bool isEnabled)
        {
            if (isEnabled == _isCommandBufferInUse)
            {
                return;
            }

            // Acquire the command buffer
            var commandBuffer = GetOrConstructCommandBuffer();

            if (isEnabled)
            {
                // Attach to the camera
                _camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
                _camera.AddCommandBuffer(CameraEvent.BeforeGBuffer, commandBuffer);
            }
            else
            {
                // Detach from the camera
                _camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
                _camera.RemoveCommandBuffer(CameraEvent.BeforeGBuffer, commandBuffer);

                // Release the command buffer
                _backgroundCommandBuffer?.Dispose();
                _backgroundCommandBuffer = null;
            }

            _isCommandBufferInUse = isEnabled;
        }

        /// <summary>
        /// Retrieves the background rendering command buffer. If it doesn't exist, it will be constructed.
        /// </summary>
        private CommandBuffer GetOrConstructCommandBuffer()
        {
            if (_backgroundMaterial == null && !_useCustomBackgroundMaterial)
            {
                _backgroundMaterial = new Material(_backgroundShader) { hideFlags = HideFlags.HideAndDontSave };
            }
            else
            {
                _backgroundMaterial = _customBackgroundMaterial;
            }

            if (_backgroundCommandBuffer == null)
            {
                _backgroundCommandBuffer = new CommandBuffer();
                _backgroundCommandBuffer.name = "LightshipOcclusionExtension";
                _backgroundCommandBuffer.Blit(null, BuiltinRenderTextureType.CameraTarget, _backgroundMaterial);
            }

            return _backgroundCommandBuffer;
        }

        private uint UpdateSemanticsMask(IReadOnlyList<string> channelNames)
        {
            var mask = 0u;
            const int bitsPerPixel = sizeof(UInt32) * 8;

            UInt32 GetChannelTextureMask(int channelIndex)
            {
                if (channelIndex is < 0 or >= bitsPerPixel)
                    return 0u;

                return 1u << (bitsPerPixel - 1 - channelIndex);
            }

            for (var i = 0; i < channelNames.Count; i++)
            {
                var cIdx = _semanticSegmentationManager.GetChannelIndex(channelNames[i]);
                mask |= GetChannelTextureMask(cIdx);
            }

            return mask;
        }

        private void CameraManagerOnFrameReceived(ARCameraFrameEventArgs args)
        {
            // Accumulate the number of frames
            _numberOfARFramesReceived++;
        }

        #region Utils

        private void Perform(Action what, Func<bool> after, Action onTimeout = null)
        {
            if (after.Invoke())
            {
                what?.Invoke();
                return;
            }

            StartCoroutine(ConditionalAction(what, after, 10.0f, onTimeout));
        }

        private IEnumerator ConditionalAction(Action action, Func<bool> condition, float timeout = 10.0f, Action onTimeout = null)
        {
            var startTime = Time.unscaledTime;

            while (Time.unscaledTime - startTime < timeout)
            {
                if (condition.Invoke())
                {
                    action?.Invoke();
                    yield break;
                }

                yield return null;
            }

            if (onTimeout != null && !condition.Invoke())
            {
                onTimeout.Invoke();
            }
        }

        /// Sparsely samples the specified subregion for the closest depth value.
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
