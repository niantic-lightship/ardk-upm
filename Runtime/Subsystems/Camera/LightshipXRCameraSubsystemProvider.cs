using System;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Camera
{
    /// <summary>
    /// An XRCameraSubsystem provider that uses the Lightship native camera subsystem
    /// implementation to acquire camera images.
    /// </summary>
    internal class LightshipXRCameraSubsystemProvider : XRCameraSubsystem.Provider
    {
        /// <summary>
        /// Possible YUV formats from the native camera.
        /// </summary>
        private enum YuvFormat
        {
            Unknown = 0,
            Nv12 = 1,
            Nv21 = 2,
            I420 = 3,
        }

        /// <summary>
        /// The shader property name for the luminance component of the camera video frame.
        /// </summary>
        private const string KTextureLumaPropertyName = "_TextureY";

        /// <summary>
        /// The shader property name for the chrominance components of the camera video frame.
        /// </summary>
        private const string KTextureChromaPropertyName = "_TextureUV";

        /// <summary>
        /// The shader property name for the RGBA colour components of the camera video frame.
        /// </summary>
        private const string KTextureRgbaPropertyName = "_MainTex";

        /// <summary>
        /// An instance of the <see cref="XRCpuImage.Api"/> used to operate on <see cref="XRCpuImage"/> objects.
        /// </summary>
        public override XRCpuImage.Api cpuImageApi => LightshipCpuImageApi.Instance;

        /// <summary>
        /// The native handle for the camera subsystem.
        /// </summary>
        private IntPtr _nativeHandle;

        /// <summary>
        /// The latest image on GPU memory. Each array element corresponds to an image plane.
        /// <remarks>This is implemented with a Unity texture object for now,
        /// but will be replaced with a native texture implementation.</remarks>
        /// </summary>
        private Texture2D[] _textures;

        /// <summary>
        /// Whether the provider supports XRTextureDescriptors and thus
        /// triggers frame updates through the ARCameraManager.
        /// </summary>
        private readonly bool _supportsTextureDescriptors;

        /// <summary>
        /// The timestamp of the last acquired camera image in milliseconds.
        /// </summary>
        /// <remarks>
        /// The source of this timestamp is the AImage_getTimestamp API. We can use this
        /// with the OVRPlugin (to get camera poses) because both sides ultimately read
        /// CLOCK_BOOTTIME / elapsedRealtime.
        /// </remarks>
        protected double LastImageTimestampMs { get; private set; }

        /// <summary>
        /// Constructs the Lightship camera subsystem provider.
        /// </summary>
        public LightshipXRCameraSubsystemProvider()
            : this(supportsTextureDescriptors: false)
        {
        }

        /// <summary>
        /// Constructs the Lightship camera subsystem provider.
        /// <param name="supportsTextureDescriptors">Whether the provider supports XRTextureDescriptors and thus
        /// triggers frame updates through the ARCameraManager.</param>
        /// </summary>
        protected LightshipXRCameraSubsystemProvider(bool supportsTextureDescriptors)
        {
            _supportsTextureDescriptors = supportsTextureDescriptors;
            _nativeHandle = NativeApi.Construct();
            if (_nativeHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create the native camera subsystem.");
            }
        }

        /// <summary>
        /// Destroy the camera for the subsystem.
        /// </summary>
        public override void Destroy()
        {
            NativeApi.Destruct(_nativeHandle);
            _nativeHandle = IntPtr.Zero;

            foreach (var texture in _textures)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }

            _textures = null;
        }

        /// <inheritdoc />
        protected override bool TryInitialize()
        {
#if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                Debug.LogError("Camera permission is not granted. " +
                    "Please request camera permission before initializing the camera subsystem.");
                return false;
            }
#endif

            return NativeApi.Initialize(_nativeHandle);
        }

        /// <inheritdoc />
        public override void Start() => NativeApi.Start(_nativeHandle);

        /// <inheritdoc />
        public override void Stop() => NativeApi.Stop(_nativeHandle);

        /// <summary>
        /// Get the latest native camera image.
        /// </summary>
        /// <param name="cameraImageCinfo">The metadata required to construct a <see cref="XRCpuImage"/>.</param>
        /// <returns><see langword="true"/> if the camera image is acquired. Otherwise, <see langword="false"/>.</returns>
        /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support camera image.</exception>
        public override bool TryAcquireLatestCpuImage(out XRCpuImage.Cinfo cameraImageCinfo)
        {
            cameraImageCinfo = default;
            var resourceHandle = NativeApi.TryAcquireLatestImageYUV(_nativeHandle, out IntPtr plane0, out int size0,
                out IntPtr plane1, out int size1, out IntPtr _, out int _, out int width, out int height,
                out var format, out ulong timestampMs);
            if (resourceHandle == IntPtr.Zero)
            {
                return false;
            }

            // Check if the image format is supported
            // TODO(ahegedus): add support for NV21 and I420 formats
            if ((YuvFormat)format != YuvFormat.Nv21)
            {
                Debug.LogError($"Unsupported YUV format: {format}");
                NativeApi.Resource.Release(resourceHandle);
                return false;
            }

            // Update the last frame timestamp
            LastImageTimestampMs = timestampMs;

            // Make a copy of the image data
            var success = LightshipCpuImageApi.Instance.TryAddManagedXRCpuImage(
                // Planes data
                new[] {plane0, plane1},

                // Planes data sizes in bytes
                new[] {size0, size1},

                // Image dimensions
                width, height,

                // Image format
                (YuvFormat)format == YuvFormat.Nv21
                    ? XRCpuImage.Format.AndroidYuv420_888
                    : XRCpuImage.Format.IosYpCbCr420_8BiPlanarFullRange,

                // Timestamp
                timestampMs, out cameraImageCinfo);

            // Release the native resource
            NativeApi.Resource.Release(resourceHandle);

            return success;
        }

        /// <summary>
        /// Get the camera intrinsics information.
        /// </summary>
        /// <param name="cameraIntrinsics">The camera intrinsics information returned from the method.</param>
        /// <returns><see langword="true"/> if the method successfully gets the camera intrinsics information.
        /// Otherwise, <see langword="false"/>.</returns>
        public override bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics)
        {
            var intrinsics = new float[9];
            if (NativeApi.TryGetLatestIntrinsics(_nativeHandle, outMatrix3X3: intrinsics))
            {
                if (NativeApi.TryGetSensorResolution(_nativeHandle, out int width, out int height))
                {
                    cameraIntrinsics = new XRCameraIntrinsics(
                        focalLength: new Vector2(intrinsics[0], intrinsics[4]),
                        principalPoint: new Vector2(intrinsics[2], intrinsics[5]),
                        resolution: new Vector2Int(width, height));
                    return true;
                }
            }

            cameraIntrinsics = default;
            return false;
        }

        /// <summary>
        /// Returns the offset of the camera lens relative to the device center.
        /// </summary>
        /// <param name="translation">The position of the camera device's lens optical center, as a three-dimensional vector (x,y,z).</param>
        /// <param name="rotation">The orientation of the camera relative to the sensor coordinate system.</param>
        /// <returns>True if the lens pose was successfully retrieved, false otherwise.</returns>
        protected bool TryGetLensOffset(out Vector3 translation, out Quaternion rotation)
        {
            var resTranslation = new float[3];
            var resRotation = new float[4];
            if (NativeApi.TryGetLensOffset(_nativeHandle, outVector3: resTranslation, outQuaternion: resRotation))
            {
                translation = new Vector3(resTranslation[0], resTranslation[1], resTranslation[2]);
                rotation = new Quaternion(resRotation[0], resRotation[1], resRotation[2], resRotation[3]);
                return true;
            }

            translation = default;
            rotation = default;
            return false;
        }

        /// <summary>
        /// Get the <see cref="XRTextureDescriptor"/>s associated with the current <see cref="XRCameraFrame"/>.
        /// </summary>
        /// <param name="defaultDescriptor">A default value used to fill the returned array before copying in real
        /// values. This ensures future additions to this <see langword="struct"/> are backwards compatible.</param>
        /// <param name="allocator">The allocation strategy to use for the returned data..</param>
        /// <returns>The current texture descriptors.</returns>
        public override NativeArray<XRTextureDescriptor> GetTextureDescriptors(XRTextureDescriptor defaultDescriptor,
            Allocator allocator)
        {
            // TODO(ahegedus): This should be replaced with a native texture implementation.
            if (_supportsTextureDescriptors &&
                cpuImageApi != null &&
                TryAcquireLatestCpuImage(out var cinfo))
            {
                var cpuImage = new XRCpuImage(cpuImageApi, cinfo);
                if (!cpuImage.valid)
                {
                    Debug.LogError("Failed to acquire the latest CPU image.");
                    return new NativeArray<XRTextureDescriptor>(0, allocator);
                }

                // Copy the image to gpu memory
                var success = cpuImage.CreateOrUpdateTextures(ref _textures, linearColorSpace: cpuImage.planeCount > 1);
                cpuImage.Dispose();

                // Check if the textures were created successfully
                if (!success)
                {
                    Debug.LogError("Failed to create or update textures.");
                    return new NativeArray<XRTextureDescriptor>(0, allocator);
                }

                // Get the number of planes
                var numPlanes = cpuImage.planeCount;
                if (_textures.Length != numPlanes)
                {
                    Debug.LogError($"Unexpected number of planes: {_textures.Length} != {numPlanes}");
                    return new NativeArray<XRTextureDescriptor>(0, allocator);
                }

                // Construct the texture descriptors
                var textureDescriptors = new NativeArray<XRTextureDescriptor>(numPlanes, allocator);
                for (var planeIdx = 0; planeIdx < numPlanes; planeIdx++)
                {
                    var propertyId = numPlanes < 2
                        ? Shader.PropertyToID(KTextureRgbaPropertyName)
                        : planeIdx == 0
                            ? Shader.PropertyToID(KTextureLumaPropertyName)
                            : Shader.PropertyToID(KTextureChromaPropertyName);

                    // Create the texture descriptor entry from the Unity texture
                    textureDescriptors[planeIdx] = new XRTextureDescriptor
                    (
                        _textures[planeIdx].GetNativeTexturePtr(),
                        _textures[planeIdx].width,
                        _textures[planeIdx].height,
                        0,
                        _textures[planeIdx].format,
                        propertyId,
                        0,
                        TextureDimension.Tex2D
                    );
                }

                return textureDescriptors;
            }

            return new NativeArray<XRTextureDescriptor>(0, allocator);
        }
    }
}
