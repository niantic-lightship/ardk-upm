// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Textures;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Common
{
    internal class LightshipCpuImageApi : XRCpuImage.Api
    {
        /// <summary>
        /// The shared API instance.
        /// </summary>
        public static LightshipCpuImageApi instance { get; } = new LightshipCpuImageApi();

        private Dictionary<int, NativeImage> _images;
        private NativeDataRepository _pool;
        private Texture2D _convertedTexture;

        private struct NativeImage
        {
            public readonly int Handle;
            public readonly XRCpuImage.Format Format;
            public readonly Vector2Int Dimensions;
            public double TimestampS;

            public NativeImage(int handle, TextureFormat format, int width, int height, ulong timestampMs)
            {
                Handle = handle;
                Format = format.ConvertToXRCpuImageFormat();
                Dimensions = new Vector2Int(width, height);
                TimestampS = timestampMs / 1000.0;
            }
        }

        private LightshipCpuImageApi()
        {
            _images = new Dictionary<int, NativeImage>();
            _pool = new NativeDataRepository();
            _convertedTexture = new Texture2D(16, 16);
        }

        /// <summary>
        /// Utility method to reset the singleton instance in between tests. Not intended to actually be used
        /// in production.
        /// </summary>
        public void Reset()
        {
            _images.Clear();

            if (_pool.Size > 0)
            {
                Log.Error("Failed to dispose memory allocated in the NativeDataRepository.");
            }

            _pool = new NativeDataRepository();
            _convertedTexture = new Texture2D(16, 16);
        }

        public bool TryAddManagedXRCpuImage
        (
            IntPtr data,
            int size,
            int width,
            int height,
            TextureFormat format,
            ulong timestampMs,
            out XRCpuImage.Cinfo cinfo
        )
        {
            if (_pool.TryCopyFrom(data, size, out var handle))
            {
                var image = new NativeImage(handle, format, width, height, timestampMs);
                _images.Add(handle, image);
                cinfo = new XRCpuImage.Cinfo(handle, image.Dimensions, 1, image.TimestampS, image.Format);

                return true;
            }

            cinfo = default;
            return false;
        }

        /// <summary>
        /// Dispose an existing native image identified by <paramref name="nativeHandle"/>.
        /// </summary>
        /// <param name="nativeHandle">A unique identifier for this camera image.</param>
        public override void DisposeImage(int nativeHandle)
        {
            _images.Remove(nativeHandle);
            _pool.Dispose(nativeHandle);
        }

        public override bool TryGetPlane(int nativeHandle, int planeIndex, out XRCpuImage.Plane.Cinfo planeCinfo)
        {
            planeCinfo = default;

            if (planeIndex != 0 || !_images.TryGetValue(nativeHandle, out var image) || !_pool.TryGetData(nativeHandle, out var nativeArray))
            {
                return false;
            }

            var dataLength = nativeArray.Length; // bytes
            var pixelStride = image.Format.BytesPerPixel();
            var rowStride = pixelStride * image.Dimensions.x;

            unsafe
            {
                planeCinfo = new XRCpuImage.Plane.Cinfo((IntPtr)nativeArray.GetUnsafePtr(), dataLength, rowStride, pixelStride);
            }

            return true;
        }

        /// <summary>
        /// Determine whether a native image handle returned by
        /// <see cref="LightshipCameraSubsystem.Provider.TryAcquireLatestCpuImage"/> is currently valid. An image can
        /// become invalid if it has been disposed.
        /// </summary>
        /// <remarks>
        /// If a handle is valid, <see cref="TryConvert"/> and <see cref="TryGetConvertedDataSize"/> should not fail.
        /// </remarks>
        /// <param name="nativeHandle">A unique identifier for the camera image in question.</param>
        /// <returns><c>true</c>, if it is a valid handle. Otherwise, <c>false</c>.</returns>
        /// <seealso cref="DisposeImage"/>
        public override bool NativeHandleValid(int nativeHandle)
        {
            return _images.ContainsKey(nativeHandle);
        }

        /// <summary>
        /// Get the number of bytes required to store an image with the given dimensions and
        /// [TextureFormat](https://docs.unity3d.com/ScriptReference/TextureFormat.html).
        /// </summary>
        /// <param name="nativeHandle">A unique identifier for the camera image to convert.</param>
        /// <param name="dimensions">The dimensions of the output image.</param>
        /// <param name="format">The <c>TextureFormat</c> for the image.</param>
        /// <param name="size">The number of bytes required to store the converted image.</param>
        /// <returns><c>true</c> if the output <paramref name="size"/> was set.</returns>
        public override bool TryGetConvertedDataSize
        (
            int nativeHandle,
            Vector2Int dimensions,
            TextureFormat format,
            out int size
        )
        {
            var bytesPerPixel = format.BytesPerPixel();
            if (bytesPerPixel == 0)
            {
                size = 0;
                return false;
            }

            size = dimensions.x * dimensions.y * bytesPerPixel;
            return size > 0;
        }

        /// <summary>
        /// Convert the image with handle <paramref name="nativeHandle"/> using the provided
        /// <paramref cref="conversionParams"/>.
        /// </summary>
        /// <param name="nativeHandle">A unique identifier for the camera image to convert.</param>
        /// <param name="conversionParams">The parameters to use during the conversion.</param>
        /// <param name="destinationBuffer">
        ///     A buffer to write the converted image to. Its contents are undefined if the new dimensions
        ///     are larger than the image's original dimensions.
        /// </param>
        /// <param name="bufferLength">The number of bytes available in the buffer.</param>
        /// <returns>
        /// <c>true</c> if the image was converted and stored in <paramref name="destinationBuffer"/>.
        /// </returns>
        public override bool TryConvert
        (
            int nativeHandle,
            XRCpuImage.ConversionParams conversionParams,
            IntPtr destinationBuffer,
            int bufferLength
        )
        {
            if (!_images.TryGetValue(nativeHandle, out var image))
            {
                return false;
            }

            if (!s_SupportedConversions[image.Format].Contains(conversionParams.outputFormat))
            {
                return false;
            }

            if (_convertedTexture.width != conversionParams.outputDimensions.x
                || _convertedTexture.height != conversionParams.outputDimensions.y
                || _convertedTexture.format != conversionParams.outputFormat)
            {
                _convertedTexture.Reinitialize
                (
                    conversionParams.outputDimensions.x,
                    conversionParams.outputDimensions.y,
                    conversionParams.outputFormat,
                    false
                );
            }

            _pool.TryGetData(nativeHandle, out var sourceData);
            var sourceTexture = new Texture2D(image.Dimensions.x, image.Dimensions.y, image.Format.ConvertToTextureFormat(), false);
            sourceTexture.LoadRawTextureData(sourceData);
            sourceTexture.Apply();

            _convertedTexture.filterMode = FilterMode.Bilinear;
            Blit(sourceTexture, _convertedTexture, conversionParams.transformation);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(sourceTexture);  // Edit-mode tests need DestroyImmediate.
            }
            else
#endif
            {
                UnityEngine.Object.Destroy(sourceTexture);
            }

            var convertedData = _convertedTexture.GetRawTextureData<byte>();

            unsafe
            {
                UnsafeUtility.MemCpy(destinationBuffer.ToPointer(), convertedData.GetUnsafeReadOnlyPtr(), bufferLength);
            }

            return true;
        }

        private static void Blit
        (
            Texture sourceTexture,
            Texture2D destinationTexture,
            XRCpuImage.Transformation transformation = XRCpuImage.Transformation.None
        )
        {
            var tmp =
                RenderTexture.GetTemporary
                (
                    destinationTexture.width,
                    destinationTexture.height,
                    0,
                    destinationTexture.graphicsFormat
                );

            var cachedRenderTarget = RenderTexture.active;

            var (scale, offset) = CalculateBlitScale(transformation);

            // Separating scaling and non-scaling in case the implementation for no scaling is more efficient.
            if (scale != Vector2.one)
            {
                Graphics.Blit(sourceTexture, tmp, scale, offset);
            }
            else
            {
                Graphics.Blit(sourceTexture, tmp);
            }

            RenderTexture.ReleaseTemporary(tmp);

            // Reads all pixels from the current render target and writes them to a texture.
            var rect = new Rect(0, 0, destinationTexture.width, destinationTexture.height);
            destinationTexture.ReadPixels(rect, 0, 0, false);

            RenderTexture.active = cachedRenderTarget;
        }

        private static (Vector2 scale, Vector2 offset) CalculateBlitScale
        (
            XRCpuImage.Transformation transformation
        )
        {
            var scale = Vector2.one;
            var offset = Vector2.zero;

            switch (transformation)
            {
                case XRCpuImage.Transformation.MirrorY:
                    scale.x *= -1;
                    offset.x = 1;
                    break;

                case XRCpuImage.Transformation.MirrorX:
                    scale.y *= -1;
                    offset.y = 1;
                    break;

                case XRCpuImage.Transformation.None:
                    break;
            }

            return (scale, offset);
        }

        private static readonly Dictionary<XRCpuImage.Format, HashSet<TextureFormat>> s_SupportedConversions =
            new()
            {
                { XRCpuImage.Format.DepthFloat32, new HashSet<TextureFormat> { TextureFormat.RFloat } },
                { XRCpuImage.Format.OneComponent8, new HashSet<TextureFormat> { TextureFormat.R8, TextureFormat.Alpha8 } }
            };

        /// <summary>
        /// Determines whether a given
        /// [TextureFormat](https://docs.unity3d.com/ScriptReference/TextureFormat.html) is supported for image
        /// conversion.
        /// </summary>
        /// <param name="image">The <see cref="XRCpuImage"/> to convert.</param>
        /// <param name="format">The [TextureFormat](https://docs.unity3d.com/ScriptReference/TextureFormat.html)
        ///  to test.</param>
        /// <returns>Returns `true` if <paramref name="image"/> can be converted to <paramref name="format"/>.
        ///  Returns `false` otherwise.</returns>
        public override bool FormatSupported(XRCpuImage image, TextureFormat format)
        {
            switch (image.format)
            {
                case XRCpuImage.Format.OneComponent8:
                    return
                        format == TextureFormat.R8 ||
                        format == TextureFormat.Alpha8;
                case XRCpuImage.Format.DepthFloat32:
                    return format == TextureFormat.RFloat;
                default:
                    return false;
            }
        }
    }
}
