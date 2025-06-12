// Copyright 2022-2025 Niantic.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Profiling;
using Niantic.Lightship.AR.Utilities.Textures;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Common
{
    internal class LightshipCpuImageApi : XRCpuImage.Api
    {
        /// <summary>
        /// The shared API instance.
        /// </summary>
        public static LightshipCpuImageApi Instance { get; } = new LightshipCpuImageApi();

        private const string TraceCategory = "LightshipCpuImageApi";

        private Dictionary<int, NativeImage> _images;
        private NativeDataRepository _pool;
        private Texture2D _convertedTexture;

        private struct NativeImage
        {
            public readonly int Handle;
            public readonly XRCpuImage.Format Format;
            public readonly Vector2Int Dimensions;
            public readonly int PlaneCount;
            public readonly double TimestampS;

            public int GetWidth(int planeIndex) => Mathf.Clamp(planeIndex, 0, 3) == 0
                ? Dimensions.x
                : (Dimensions.x + 1) / 2;

            public int GetHeight(int planeIndex) => Mathf.Clamp(planeIndex, 0, 3) == 0
                ? Dimensions.y
                : (Dimensions.y + 1) / 2;

            public int GetPixelStride(int planeIndex) => Mathf.Clamp(planeIndex, 0, 3) == 0
                ? Format.BytesPerPixel()
                : PlaneCount == 2 ? 2 : 1;

            public int GetRowStride(int planeIndex) => GetWidth(planeIndex) * GetPixelStride(planeIndex);

            public NativeImage(int handle, TextureFormat format, int width, int height, ulong timestampMs,
                int planeCount = 1)
            {
                Handle = handle;
                Format = format.ConvertToXRCpuImageFormat();
                Dimensions = new Vector2Int(width, height);
                TimestampS = timestampMs / 1000.0;
                PlaneCount = planeCount;
            }

            public NativeImage(int handle, XRCpuImage.Format format, int width, int height, ulong timestampMs,
                int planeCount = 1)
            {
                Handle = handle;
                Format = format;
                Dimensions = new Vector2Int(width, height);
                TimestampS = timestampMs / 1000.0;
                PlaneCount = planeCount;
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
                Log.Warning("Failed to dispose memory allocated in the NativeDataRepository.");
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
                cinfo = new XRCpuImage.Cinfo(handle, image.Dimensions, image.PlaneCount, image.TimestampS,
                    image.Format);

                return true;
            }

            cinfo = default;
            return false;
        }

        public bool TryAddManagedXRCpuImage
        (
            IntPtr[] dataPtrs,
            int[] dataSizes,
            int width,
            int height,
            XRCpuImage.Format format,
            ulong timestampMs,
            out XRCpuImage.Cinfo cinfo
        )
        {
            if (_pool.TryCopyFrom(dataPtrs, dataSizes, out var handle))
            {
                var image = new NativeImage(handle, format, width, height, timestampMs, planeCount: dataPtrs.Length);
                _images.Add(handle, image);
                cinfo = new XRCpuImage.Cinfo(handle, image.Dimensions, image.PlaneCount, image.TimestampS,
                    image.Format);

                return true;
            }

            cinfo = default;
            return false;
        }

        /// <summary>
        /// Copies the specified data to the cpu image data store and creates a new <see cref="XRCpuImage.Cinfo"/>.
        /// </summary>
        /// <remarks>The resulting image will contain a single plane.</remarks>>
        /// <param name="data">The data to copy into the cpu image data store.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="format">The format of the image.</param>
        /// <param name="timestampMs">The timestamp of the image in milliseconds.</param>
        /// <param name="cinfo">The resulting <see cref="XRCpuImage.Cinfo"/>.</param>
        /// <returns>Whether the data was successfully copied.</returns>
        public bool TryAddManagedXRCpuImage
        (
            byte[] data,
            int width,
            int height,
            XRCpuImage.Format format,
            ulong timestampMs,
            out XRCpuImage.Cinfo cinfo
        )
        {
            if (_pool.TryCopyFrom(data, out var handle))
            {
                var image = new NativeImage(handle, format, width, height, timestampMs, planeCount: 1);
                _images.Add(handle, image);
                cinfo = new XRCpuImage.Cinfo(handle, image.Dimensions, image.PlaneCount, image.TimestampS,
                    image.Format);

                return true;
            }

            cinfo = default;
            return false;
        }

        /// <summary>
        /// Copies the specified data to the cpu image data store and creates a new <see cref="XRCpuImage.Cinfo"/>.
        /// </summary>
        /// <remarks>The resulting image will contain a single plane, using RGBA32 color format.</remarks>>
        /// <param name="data">The data to copy into the cpu image data store.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="timestampMs">The timestamp of the image in milliseconds.</param>
        /// <param name="cinfo">The resulting <see cref="XRCpuImage.Cinfo"/>.</param>
        /// <returns>Whether the data was successfully copied.</returns>
        public bool TryAddManagedXRCpuImage
        (
            Color32[] data,
            int width,
            int height,
            ulong timestampMs,
            out XRCpuImage.Cinfo cinfo
        )
        {
            if (_pool.TryCopyFrom(data, out var handle))
            {
                var image = new NativeImage(handle, XRCpuImage.Format.RGBA32, width, height, timestampMs);
                _images.Add(handle, image);
                cinfo = new XRCpuImage.Cinfo(handle, image.Dimensions, image.PlaneCount, image.TimestampS, image.Format);

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
            // Does the image exist?
            var imageAvailable = _images.TryGetValue(nativeHandle, out var image);
            if (!imageAvailable)
            {
                planeCinfo = default;
                return false;
            }

            // Is the plane index valid?
            if (planeIndex < 0 || planeIndex >= image.PlaneCount)
            {
                planeCinfo = default;
                return false;
            }

            // Can access the image data from the handle?
            var dataAvailable = _pool.TryGetData(nativeHandle, out var nativeArray);
            if (!dataAvailable)
            {
                planeCinfo = default;
                return false;
            }

            // Get strides
            int pixelStride = image.GetPixelStride(planeIndex);
            int rowStride = image.GetRowStride(planeIndex);
            int dataLength = rowStride * image.GetHeight(planeIndex);;

            // Get the offset to the start of the plane
            var bytesOffset = 0;
            for (var i = 0; i < planeIndex; i++)
            {
                bytesOffset += image.GetRowStride(i) * image.GetHeight(i);
            }

            // Get the pointer to the data
            IntPtr ptr;
            unsafe { ptr = (IntPtr)nativeArray.GetUnsafePtr() + bytesOffset; }

            planeCinfo = new XRCpuImage.Plane.Cinfo(ptr, dataLength, rowStride, pixelStride);
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

            if (!s_supportedConversions[image.Format].Contains(conversionParams.outputFormat))
            {
                return false;
            }

            var finalOutputFormat = conversionParams.outputFormat;
            if (conversionParams.outputFormat == TextureFormat.RGB24)
            {
                conversionParams.outputFormat = TextureFormat.RGBA32;
            }

            // Assuming R8 and RFloat are data formats, meaning 127/255 is 0.5f (linear).
            // While RFloat will almost always translate to a linear format, R8 will
            // often default to sRGB. Assuming we use R8 for depth confidence, we need
            // to explicitly specify the format as linear.
            var isDataFormat = conversionParams.outputFormat == TextureFormat.R8 ||
                conversionParams.outputFormat == TextureFormat.RFloat;

            if (_convertedTexture.width != conversionParams.outputDimensions.x
                || _convertedTexture.height != conversionParams.outputDimensions.y
                || _convertedTexture.format != conversionParams.outputFormat)
            {
                _convertedTexture.Reinitialize
                (
                    conversionParams.outputDimensions.x,
                    conversionParams.outputDimensions.y,
#if UNITY_6000_0_OR_NEWER
                    GraphicsFormatUtility.GetGraphicsFormat(conversionParams.outputFormat, !isDataFormat),
#else
                    conversionParams.outputFormat,
#endif
                    false
                );

                _convertedTexture.filterMode = FilterMode.Bilinear;
            }

            _pool.TryGetData(nativeHandle, out var sourceData);
#if UNITY_6000_0_OR_NEWER
            var sourceTexture = new Texture2D(image.Dimensions.x, image.Dimensions.y,
                GraphicsFormatUtility.GetGraphicsFormat(image.Format.AsTextureFormat(), !isDataFormat),
                TextureCreationFlags.DontInitializePixels);
#else
            var sourceTexture = new Texture2D(image.Dimensions.x, image.Dimensions.y, image.Format.AsTextureFormat(),
                false);
#endif
            sourceTexture.LoadRawTextureData(sourceData);
            sourceTexture.Apply();

            Blit(sourceTexture, _convertedTexture, conversionParams);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(sourceTexture); // Edit-mode tests need DestroyImmediate.
            }
            else
#endif
            {
                UnityEngine.Object.Destroy(sourceTexture);
            }

            unsafe
            {
                if (finalOutputFormat == TextureFormat.RGB24 && _convertedTexture.format == TextureFormat.RGBA32)
                {
                    // SLOW. But that's OK because it's only ever used in Playback + Object Detection.
                    // The channel data for both RGBA32 and RGB24 textures is ordered: R, G, B, A bytes one after another.
                    UnsafeFastRgbaToRgbCopy(_convertedTexture, destinationBuffer, bufferLength);
                }
                else
                {
                    var convertedData = _convertedTexture.GetRawTextureData<byte>();
                    UnsafeUtility.MemCpy(destinationBuffer.ToPointer(), convertedData.GetUnsafeReadOnlyPtr(),
                        bufferLength);

                    convertedData.Dispose();
                }
            }

            return true;
        }

        private void UnsafeFastRgbaToRgbCopy(Texture2D sourceTexture, IntPtr destBuffer, int destBufferLength)
        {
            // Note: move destRgbData allocation out (keep it allocated) to avoid a lot of garbage collection
            // 3 because RGB = 3 bytes
            byte[] destRgbData = new byte[sourceTexture.width * sourceTexture.height * 3];
            var sourceRgbaData = sourceTexture.GetRawTextureData<byte>();

            int srcIndex = 0;
            int destIndex = 0;

            while (srcIndex < sourceRgbaData.Length)
            {
                // Copy RGB, Skip A
                destRgbData[destIndex++] = sourceRgbaData[srcIndex++];
                destRgbData[destIndex++] = sourceRgbaData[srcIndex++];
                destRgbData[destIndex++] = sourceRgbaData[srcIndex++];
                srcIndex++;
            }

            Marshal.Copy(destRgbData, 0, destBuffer, Math.Min(destRgbData.Length, destBufferLength));
            sourceRgbaData.Dispose();
        }

        private static void Blit
        (
            Texture sourceTexture,
            Texture2D destinationTexture,
            XRCpuImage.ConversionParams conversionParams
        )
        {

#if UNITY_6000_0_OR_NEWER
            if (!SystemInfo.IsFormatSupported(destinationTexture.graphicsFormat, GraphicsFormatUsage.Render))
            {
                Log.Error($"Texture format: {destinationTexture.graphicsFormat} not supported on this platform.");
                return;
            }
#else
            if (!SystemInfo.IsFormatSupported(destinationTexture.graphicsFormat, FormatUsage.Render))
            {
                Log.Error($"Texture format: {destinationTexture.graphicsFormat} not supported on this platform.");
                return;
            }
#endif

            var tmp =
                RenderTexture.GetTemporary
                (
                    destinationTexture.width,
                    destinationTexture.height,
                    0,
                    destinationTexture.graphicsFormat
                );

            var cachedRenderTarget = RenderTexture.active;

            var (scale, offset) = CalculateBlitScale(conversionParams.transformation);

            // Separating scaling and non-scaling in case the implementation for no scaling is more efficient.
            if (scale != Vector2.one)
            {
                Graphics.Blit(sourceTexture, tmp, scale, offset);
            }
            else
            {
                var newScale = new Vector2(
                    (float)conversionParams.inputRect.width / sourceTexture.width,
                    (float)conversionParams.inputRect.height / sourceTexture.height
                );
                var newOffset = new Vector2(
                    (float)conversionParams.inputRect.x / sourceTexture.width,
                    (float)conversionParams.inputRect.y / sourceTexture.height
                );
                Graphics.Blit(sourceTexture, tmp, newScale, newOffset);
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

        private static readonly Dictionary<XRCpuImage.Format, HashSet<TextureFormat>> s_supportedConversions =
            new()
            {
                { XRCpuImage.Format.DepthFloat32, new HashSet<TextureFormat> { TextureFormat.RFloat } },
                {
                    XRCpuImage.Format.OneComponent8,
                    new HashSet<TextureFormat> { TextureFormat.R8, TextureFormat.Alpha8 }
                },
                {
                    XRCpuImage.Format.RGBA32,
                    new HashSet<TextureFormat> { TextureFormat.RGBA32, TextureFormat.RGB24 }
                },
                {
                    XRCpuImage.Format.BGRA32,
                    new HashSet<TextureFormat> { TextureFormat.RGBA32, TextureFormat.RGB24 }
                },
                {
                    XRCpuImage.Format.RGB24,
                    new HashSet<TextureFormat> { TextureFormat.RGB24, TextureFormat.RGBA32 }
                }
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
            return s_supportedConversions[image.format].Contains(format);
        }
    }
}
