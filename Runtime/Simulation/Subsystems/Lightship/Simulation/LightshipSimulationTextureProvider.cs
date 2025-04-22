// Copyright 2022-2025 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Simulation;

namespace Niantic.Lightship.AR.Simulation
{
    /// <summary>
    /// Based on Unity Simulation's CameraTextureProvider.
    /// </summary>
    internal abstract class LightshipSimulationTextureProvider : MonoBehaviour
    {
        /// <summary>
        /// Event that is invoked when a new frame is received.
        /// </summary>
        internal event Action<LightshipCameraTextureFrameEventArgs> FrameReceived;

        /// <summary>
        /// The target texture to render the simulated image into.
        /// </summary>
        private RenderTexture _targetTexture;

        /// <summary>
        /// The copy of the simulated image to be consumed by listeners.
        /// </summary>
        private Texture2D _outputTexture;

        /// <summary>
        /// Setting for where should the provider allocate data for the output texture.
        /// </summary>
        protected enum TextureAllocationMode
        {
            /// <summary>
            /// The resulting texture will only contain meaningful data on the GPU.
            /// </summary>
            GPU,

            /// <summary>
            /// The resulting texture will contain meaningful data on the CPU.
            /// </summary>
            CPU,

            /// <summary>
            /// The resulting texture will contain meaningful data on both the CPU and GPU.
            /// </summary>
            CPUAndGPU
        }

        /// <summary>
        /// Configures where should the provider allocate data for the output texture.
        /// </summary>
        protected virtual TextureAllocationMode TextureAllocation => TextureAllocationMode.CPUAndGPU;

        /// <summary>
        /// Shader property id for the output texture.
        /// </summary>
        protected abstract int PropertyNameId { get; }

        protected abstract void OnConfigureCamera(Camera renderCamera);
        protected abstract bool OnAllocateResources(out RenderTexture target, out Texture2D output);

        /// <summary>
        /// Whether the provider has been initialized.
        /// </summary>
        private bool Initialized { get; set; }

        // Components and helpers
        private Camera _simulatedCamera;
        private IntPtr _outputTexturePtr = IntPtr.Zero;

        // Configuration for the simulated camera sensor
        protected const int ImageWidth = 720;
        protected const int ImageHeight = 540;
        private const float SensorFocalLength = 26.0f;

        internal static TProvider AddToCamera<TProvider>(Camera simulationCamera, Camera xrCamera)
            where TProvider : LightshipSimulationTextureProvider
        {
            var cameraTextureProvider = simulationCamera.gameObject.AddComponent<TProvider>();
            cameraTextureProvider.InitializeProvider(xrCamera, simulationCamera);
            return cameraTextureProvider;
        }

        private void InitializeProvider(Camera xrCamera, Camera simulationCamera)
        {
            // Allocate resources
            if (!OnAllocateResources(out _targetTexture, out _outputTexture))
            {
                Debug.LogError("Failed to allocate resources for the simulation camera.");
                return;
            }

            // Cache components
            _simulatedCamera = simulationCamera;

            // Configure the simulated (physical) camera
            var simulationEnvironmentLayer = 1 << XRSimulationRuntimeSettings.Instance.environmentLayer;
            _simulatedCamera.cullingMask = simulationEnvironmentLayer;
            _simulatedCamera.depth = xrCamera.depth - 1;
            _simulatedCamera.usePhysicalProperties = true;
            _simulatedCamera.focalLength = SensorFocalLength;
            _simulatedCamera.nearClipPlane = xrCamera.nearClipPlane;
            _simulatedCamera.farClipPlane = xrCamera.farClipPlane;
            OnConfigureCamera(_simulatedCamera);

            // Flipping the projection matrix simulates the upside-down nature of native images
            // TODO(ahegedus): Is this really necessary?
            _simulatedCamera.projectionMatrix *= Matrix4x4.Scale(new Vector3(1, -1, 1));

            // Configure the XR camera
            // Our ar camera will be stripped from seeing the simulation layer
            xrCamera.cullingMask -= simulationEnvironmentLayer;
            xrCamera.clearFlags = CameraClearFlags.Color;
            xrCamera.backgroundColor = Color.black;

            Initialized = true;
        }

        private void Update()
        {
            if (!Initialized)
            {
                return;
            }

            // Render the camera into texture
            // We invert culling because the projection matrix is flipped
            GL.invertCulling = true;
            _simulatedCamera.Render();
            GL.invertCulling = false;

            // Invoke the post render callback
            OnPostRenderCamera(_simulatedCamera, _targetTexture);

            // Process the resulting texture
            if (TryUpdateTexture())
            {
                PublishFrame();
            }
        }

        protected virtual void OnDestroy()
        {
            if (_simulatedCamera != null)
            {
                _simulatedCamera.targetTexture = null;
            }

            if (_targetTexture != null)
            {
                _targetTexture.Release();
            }

            if (_outputTexture != null)
            {
                UnityObjectUtils.Destroy(_outputTexture);
            }
        }

        protected virtual void OnPostRenderCamera(Camera renderCamera, RenderTexture targetTexture) { }

        /// <summary>
        /// Invoked when it is time to copy the simulation camera's target texture to the provider texture.
        /// </summary>
        /// <returns>True if the texture was successfully updated, false otherwise.</returns>
        private bool TryUpdateTexture()
        {
            if (!_targetTexture.IsCreated())
            {
                if (!_targetTexture.Create())
                {
                    return false;
                }
            }

            // Reinitialize the provider texture if the dimensions of the render texture have changed
            if (_outputTexture.width != _targetTexture.width || _outputTexture.height != _targetTexture.height)
            {
                if (!_outputTexture.Reinitialize(_targetTexture.width, _targetTexture.height))
                {
                    return false;
                }
            }

            if (TextureAllocation == TextureAllocationMode.GPU)
            {
                // Make a GPU copy
                Graphics.CopyTexture(_targetTexture, _outputTexture);
            }
            else
            {
                // Push context
                var previousActive = RenderTexture.active;
                RenderTexture.active = _targetTexture;

                // Make a CPU copy
                _outputTexture.ReadPixels(new Rect(0, 0, _targetTexture.width, _targetTexture.height), 0, 0);

                if (TextureAllocation == TextureAllocationMode.CPUAndGPU)
                {
                    // Make a GPU copy
                    _outputTexture.Apply(false);
                }

                // Pop context
                RenderTexture.active = previousActive;
            }

            _outputTexturePtr = _outputTexture.GetNativeTexturePtr();
            return true;
        }

        private void PublishFrame()
        {
            var cameraParams = new XRCameraParams
            {
                zFar = _simulatedCamera.farClipPlane,
                zNear = _simulatedCamera.nearClipPlane,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                screenOrientation = GameViewUtils.GetEditorScreenOrientation()
            };

            // Calculate the XRCameraIntrinsics
            var intrinsics = CameraMath.CalculateIntrinsics(_simulatedCamera);

            // Calculate the projection matrix
            var projectionMatrix = CameraMath.CalculateProjectionMatrix(intrinsics, cameraParams);

            // Calculate the display matrix
            var displayMatrix = CameraMath.CalculateDisplayMatrix
            (
                _targetTexture.width,
                _targetTexture.height,
                (int)cameraParams.screenWidth,
                (int)cameraParams.screenHeight,
                cameraParams.screenOrientation,

                true,
                // Via ARF, external textures are formatted with their
                // first pixel in the top left. Thus, the displayMatrix needs
                // to include a flip because Unity displays textures with
                // their first pixel in the bottom left.
                // For this we invert the projection matrix in OnPreCull
                layout: CameraMath.MatrixLayout.RowMajor
            );

            // Acquire the timestamp
            var timestampNs = (long)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1e6);

            // Propagate frame to listeners
            FrameReceived?.Invoke(
                new LightshipCameraTextureFrameEventArgs(timestampNs, projectionMatrix, displayMatrix, intrinsics,
                    _outputTexture));
        }

        internal bool TryGetLatestImagePtr(out IntPtr nativePtr)
        {
            if (_outputTexture != null && _outputTexturePtr != IntPtr.Zero)
            {
                nativePtr = _outputTexturePtr;
                return true;
            }

            nativePtr = IntPtr.Zero;
            return false;
        }

        /// <summary>
        /// Tries to acquire the image on GPU memory via a XRTextureDescriptor.
        /// </summary>
        /// <param name="planeDescriptor">The XRTextureDescriptor for the image.</param>
        /// <returns>True if the image is successfully acquired, false otherwise.</returns>
        internal bool TryGetTextureDescriptor(out XRTextureDescriptor planeDescriptor)
        {
            // Don't provide a descriptor if the texture is cpu only
            if (TextureAllocation == TextureAllocationMode.CPU)
            {
                planeDescriptor = default;
                return false;
            }

            if (!TryGetLatestImagePtr(out var nativePtr))
            {
                planeDescriptor = default;
                return false;
            }

            planeDescriptor = new XRTextureDescriptor
            (
                nativePtr,
                _outputTexture.width,
                _outputTexture.height,
                _outputTexture.mipmapCount,
                _outputTexture.format,
                PropertyNameId,
                0,
                TextureDimension.Tex2D
            );

            return true;
        }

        /// <summary>
        /// Tries to acquire the image on GPU memory via a XRTextureDescriptor.
        /// </summary>
        /// <param name="planeDescriptors">The XRTextureDescriptor for the image.</param>
        /// <param name="allocator"></param>
        /// <returns>True if the image is successfully acquired, false otherwise.</returns>
        internal bool TryGetTextureDescriptors(out NativeArray<XRTextureDescriptor> planeDescriptors,
            Allocator allocator)
        {
            if (TryGetTextureDescriptor(out var descriptor))
            {
                planeDescriptors = new NativeArray<XRTextureDescriptor>(new []{descriptor}, allocator);
                return true;
            }

            planeDescriptors = default;
            return false;
        }

        /// <summary>
        /// Tries to acquire the image data on CPU memory.
        /// </summary>
        /// <param name="data">The image data on cpu memory.</param>
        /// <param name="dimensions">The dimensions of the image.</param>
        /// <param name="format">The format of the image.</param>
        /// <returns>True if the image data is successfully acquired, false otherwise.</returns>
        internal bool TryGetCpuData(out NativeArray<byte> data, out Vector2Int dimensions, out TextureFormat format)
        {
            var allocatesOnCPU = TextureAllocation is TextureAllocationMode.CPU or TextureAllocationMode.CPUAndGPU;
            var isTextureReadable = _outputTexture != null && _outputTexture.isReadable;

            if (!allocatesOnCPU || !isTextureReadable)
            {
                data = default;
                dimensions = default;
                format = default;
                return false;
            }

            data = _outputTexture.GetPixelData<byte>(0);
            dimensions = new Vector2Int(_outputTexture.width, _outputTexture.height);
            format = _outputTexture.format;
            return data.IsCreated;
        }
    }
}
