using System;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.ARFoundation
{
    /// <summary>
    /// The provider for occlusion context
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(AROcclusionManager))]
    internal class OcclusionContextProvider : MonoBehaviour
    {
        /// <summary>
        /// The types of adaption modes for the occlusion context
        /// </summary>
        public enum AdaptionMode
        {
            /// Take a few samples of the full buffer to
            /// determine the closest occluder on the screen.
            SampleFullScreen = 0,

            // Sample the sub-region of the buffer that is directly over
            // the main CG object, to determine the distance of its occluder
            // in the world.
            TrackOccludee = 1
        }

        [SerializeField]
        private AdaptionMode _mode = AdaptionMode.SampleFullScreen;

        /// <summary>
        /// The adaption mode for the occlusion context
        /// </summary>
        public AdaptionMode Mode
        {
            get => _mode;
            set
            {
                _mode = value == AdaptionMode.TrackOccludee && _occludee == null
                    ? AdaptionMode.SampleFullScreen
                    : value;
            }
        }

        [SerializeField]
        private Renderer _occludee;

        private Camera _camera;
        private LightshipOcclusionSubsystem _occlusionSubsystem;
        private XRCameraSubsystem _cameraSubsystem;

        private bool _isValidated;

        /// Sets the main occludee used to adjust interpolation preference for.
        /// @note This method changes the adaption mode setting.
        public void TrackOccludee(Renderer occludee)
        {
            _occludee = occludee;
            _mode = AdaptionMode.TrackOccludee;
        }

        private void Awake()
        {
            var (isLoaderInitialized, isValidSubsystemLoaded) = ValidateSubsystem();
            if (isLoaderInitialized && !isValidSubsystemLoaded)
                return;

            _camera = GetComponent<Camera>();

            if (_mode == AdaptionMode.TrackOccludee && _occludee == null)
            {
                Debug.LogError("Missing occludee renderer to track.");
                _mode = AdaptionMode.SampleFullScreen;
            }
        }

        // Returns (isLoaderInitialized, isValidSubsystemLoaded)
        private (bool, bool) ValidateSubsystem()
        {
            if (_isValidated)
                return (true, true);

            var xrManager = XRGeneralSettings.Instance.Manager;
            if (!xrManager.isInitializationComplete)
                return (false, false);

            _occlusionSubsystem =
                xrManager.activeLoader.GetLoadedSubsystem<XROcclusionSubsystem>() as LightshipOcclusionSubsystem;

            if (_occlusionSubsystem == null)
            {
                Debug.Log
                (
                    "Destroying OcclusionContextProvider component because " +
                    "it is only compatible with Lightship occlusion."
                );

                Destroy(this);
                return (true, false);
            }

            _cameraSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XRCameraSubsystem>();
            _isValidated = true;
            return (true, true);
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

            var cameraParams = new XRCameraParams
            {
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                screenOrientation = Screen.orientation,
                zNear = _camera.nearClipPlane,
                zFar = _camera.farClipPlane
            };

            if (!_cameraSubsystem.TryGetLatestFrame(cameraParams, out var frame) ||
                !_occlusionSubsystem.TryAcquireEnvironmentDepthCpuImage(out var depthBuffer))
                return;

            Debug.Assert(depthBuffer.valid);

            // Acquire sample bounds
            var region =
                _mode == AdaptionMode.TrackOccludee && _occludee != null
                    ? CalculateScreenRect(forRenderer: _occludee, usingCamera: _camera)
                    : new Rect(0.1f, 0.1f, 0.8f, 0.8f);

#if UNITY_IOS
            // The ARFoundation display matrix is transposed on iOS platform
            var displayMatrix = frame.displayMatrix.transpose;
#else
            var displayMatrix = frame.displayMatrix;
#endif
            // Sparsely sample depth within bounds
            var depth = SampleSubregion(depthBuffer, displayMatrix, region);

            // Cache result
            OcclusionContext.Shared.OccludeeEyeDepth = Mathf.Clamp(depth, 0.2f, 100.0f);
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
    }
}
