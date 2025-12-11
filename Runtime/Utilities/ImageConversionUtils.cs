// Copyright 2022-2025 Niantic.
using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.PAM;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Common
{
    internal static class ImageConversionUtils
    {
        /// <summary>
        /// Converts a raw camera image buffer on the CPU into a Unity-compatible
        /// <see cref="TextureFormat"/> layout, writing the converted pixel data into the provided destination buffer.
        /// </summary>
        /// <param name="plane0Ptr">A pointer to the first image plane. Must not be <see cref="IntPtr.Zero"/>.</param>
        /// <param name="plane0Stride">The row stride, in bytes, of the first plane.</param>
        /// <param name="plane1Ptr">
        /// A pointer to the second image plane, if present; otherwise <see cref="IntPtr.Zero"/> for single-plane images.
        /// </param>
        /// <param name="plane1Stride">The row stride, in bytes, of the second plane (ignored if <paramref name="plane1Ptr"/> is zero).</param>
        /// <param name="plane2Ptr">
        /// A pointer to the third image plane, if present; otherwise <see cref="IntPtr.Zero"/> for single- or bi-planar images.
        /// </param>
        /// <param name="plane2Stride">The row stride, in bytes, of the third plane (ignored if <paramref name="plane2Ptr"/> is zero).</param>
        /// <param name="planeCount">The number of planes contained in the image (typically 1, 2, or 3).</param>
        /// <param name="width">The width of the full source image, in pixels.</param>
        /// <param name="height">The height of the full source image, in pixels.</param>
        /// <param name="srcFormat">The source image format (<see cref="XRCpuImage.Format"/>).</param>
        /// <param name="dstBufferPtr">A pointer to the destination buffer where converted pixels will be written.</param>
        /// <param name="conversionParams">The conversion parameters specifying the target texture format.</param>
        /// <returns><c>true</c> if the conversion succeeded; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This method performs a CPU-based image conversion using the native image conversion pipeline.
        /// The source image may contain one or more planes; unused plane pointers must be set to <see cref="IntPtr.Zero"/>.
        /// Image transformations such as cropping, resizing, or mirroring are not supported and will throw a
        /// <see cref="NotSupportedException"/> if requested.
        /// </remarks>
        public static bool TryConvertOnCpu(IntPtr plane0Ptr, int plane0Stride,
            IntPtr plane1Ptr, int plane1Stride, IntPtr plane2Ptr, int plane2Stride,
            int planeCount, int width, int height, XRCpuImage.Format srcFormat, IntPtr dstBufferPtr,
            XRCpuImage.ConversionParams conversionParams)
        {

            if (conversionParams.transformation != XRCpuImage.Transformation.None ||
                conversionParams.inputRect.width != width ||
                conversionParams.inputRect.height != height ||
                conversionParams.outputDimensions.x != width ||
                conversionParams.outputDimensions.y != height)
            {
                throw new NotSupportedException("Image transformations are not supported when converting on CPU.");
            }

            var srcFmt = ToNativeImageFormat(srcFormat, planeCount, plane1Ptr, plane2Ptr);
            var dstFmt = ToNativeImageFormat(conversionParams.outputFormat.ToXRCpuImageFormat());

            return NativeApi.TryConvert(plane0Ptr, plane0Stride, plane1Ptr, plane1Stride, plane2Ptr, plane2Stride,
                width, height, srcFmt, dstBufferPtr, dstFmt);
        }

        /// <summary>
        /// Converts a single-plane image buffer on the CPU into another <see cref="XRCpuImage.Format"/>,
        /// allocating a new <see cref="NativeArray{T}"/> to store the converted data.
        /// </summary>
        /// <param name="sourceData">The source image data to convert.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="srcFormat">The source image format (<see cref="XRCpuImage.Format"/>).</param>
        /// <param name="outputFormat">The desired output image format (<see cref="XRCpuImage.Format"/>).</param>
        /// <param name="outputData">
        /// When this method returns <see langword="true"/>, contains a newly allocated
        /// <see cref="NativeArray{T}"/> holding the converted image data.
        /// The caller is responsible for disposing this array when it is no longer needed.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the conversion succeeded; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This is a convenience overload that supports only single-plane image formats as input.
        /// It automatically allocates a persistent <see cref="NativeArray{T}"/> for the converted output.
        /// For multi-plane images or when manual buffer management is required, use the
        /// <see cref="TryConvertOnCpu(IntPtr,int,IntPtr,int,IntPtr,int,int,int,int,XRCpuImage.Format,IntPtr,XRCpuImage.ConversionParams)"/> overload instead.
        /// </remarks>
        public static bool TryConvertOnCpu(NativeArray<byte> sourceData, int width, int height, XRCpuImage.Format srcFormat,
            XRCpuImage.Format outputFormat, out NativeArray<byte> outputData)
        {
            var length = GetConvertedDataSize(outputFormat, width, height);
            outputData = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            IntPtr srcPtr;
            IntPtr dstPtr;
            unsafe
            {
                srcPtr = (IntPtr)sourceData.GetUnsafeReadOnlyPtr();
                dstPtr = new IntPtr(NativeArrayUnsafeUtility.GetUnsafePtr(outputData));
            }

            var srcFmt = ToNativeImageFormat(srcFormat);
            var dstFmt = ToNativeImageFormat(outputFormat);

            return NativeApi.TryConvert(srcPtr, width * srcFormat.BytesPerPixel(),
                IntPtr.Zero, 0, IntPtr.Zero, 0,
                width, height, srcFmt, dstPtr, dstFmt);
        }

        /// <summary>
        /// Converts a raw camera image buffer into a Unity-compatible <see cref="TextureFormat"/> layout,
        /// writing the converted pixel data into the provided destination buffer.
        /// </summary>
        /// <param name="srcData"> The raw interleaved or planar source image data.</param>
        /// <param name="srcWidth">Width of the full source image, in pixels.</param>
        /// <param name="srcHeight">Height of the full source image, in pixels.</param>
        /// <param name="srcFormat">The ARFoundation source format (<see cref="XRCpuImage.Format"/>).</param>
        /// <param name="dstBufferPtr">A pointer to the destination buffer where converted pixels will be written.</param>
        /// <param name="conversionParams">
        /// The conversion configuration specifying the crop rectangle, output dimensions,
        /// target texture format, and transformation flags (mirror).
        /// </param>
        /// <remarks>This api variant is slow, due to gpu readback.</remarks>
        /// <returns><c>true</c> if the conversion succeeded; otherwise, <c>false</c>.</returns>
        public static bool TryConvertOnGpu(NativeArray<byte> srcData, int srcWidth, int srcHeight,
            XRCpuImage.Format srcFormat, IntPtr dstBufferPtr, XRCpuImage.ConversionParams conversionParams)
        {
            // Currently, this API uses the built-in blit shader,
            // which doesn't support biplanar input
            if (srcFormat
                is XRCpuImage.Format.IosYpCbCr420_8BiPlanarFullRange
                or XRCpuImage.Format.AndroidYuv420_888)
            {
                Log.Error("BpiPlanar image conversion is not supported on the GPU.");
                return false;
            }

            // Verify graphics format support on the platform
            InferGraphicsFormats(
                src: srcFormat.AsTextureFormat(),
                dst: conversionParams.outputFormat,
                out var inputGraphicsFormat,
                out var outputGraphicsFormat);

            if (!IsSampleFormatSupported(inputGraphicsFormat) ||
                !IsRenderFormatSupported(outputGraphicsFormat))
            {
                return false;
            }

            // Prepare the source image
            var sourceTexture = new Texture2D(srcWidth, srcHeight, inputGraphicsFormat,
                TextureCreationFlags.DontInitializePixels);
            if (srcFormat.AsTextureFormat() == TextureFormat.RGB24)
            {
                // To provide wider GPU support, we use RGBA32 instead of RGB24 input format
                // Here, we need to convert the original RGB24 input to RGBA32
                var rgbaData = new NativeArray<byte>(
                    srcWidth * srcHeight * 4,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
                BurstImageUtils.InvokeExpandRgbToRgba(srcData, rgbaData);

                try
                {
                    sourceTexture.LoadRawTextureData(rgbaData);
                }
                finally
                {
                    rgbaData.Dispose();
                }
            }
            else
            {
                sourceTexture.LoadRawTextureData(srcData);
            }
            sourceTexture.Apply(false, true);
            CalculateScaleAndOffset(sourceTexture, conversionParams, out var scale, out var offset);

            // Prepare the output texture
            var outputTexture = new Texture2D
            (
                conversionParams.outputDimensions.x,
                conversionParams.outputDimensions.y,
                outputGraphicsFormat,
                TextureCreationFlags.None
            ) { filterMode = FilterMode.Bilinear };

            // Grab an intermediate texture
            var intermediate = RenderTexture.GetTemporary(outputTexture.width, outputTexture.height, 0,
                outputTexture.graphicsFormat);

            // Cache and set active RT
            var cachedRenderTarget = RenderTexture.active;

            // Render the texture
            Graphics.Blit(sourceTexture, intermediate, scale, offset);

            // Copy the result of the blit to the output cpu texture
            RenderTexture.active = intermediate;
            var rect = new Rect(0, 0, outputTexture.width, outputTexture.height);
            outputTexture.ReadPixels(rect, 0, 0, false);

            // Revert state
            RenderTexture.active = cachedRenderTarget;
            RenderTexture.ReleaseTemporary(intermediate);

            unsafe
            {
                var cpuData = outputTexture.GetPixelData<byte>(0);

                // In case of TextureFormat.RGB24, the graphics format defers to RGBA32 (for wider support)
                // so we need to strip the alpha channel to produce the final output manually
                if (conversionParams.outputFormat == TextureFormat.RGB24)
                {
                    BurstImageUtils.InvokeReduceRgbaToRgb(cpuData, dstBufferPtr);
                }
                else
                {
                    UnsafeUtility.MemCpy(dstBufferPtr.ToPointer(),
                        NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(cpuData), cpuData.Length);
                }
            }

            // Release resources
            UnityObjectUtils.Destroy(sourceTexture);
            UnityObjectUtils.Destroy(outputTexture);

            return true;
        }

        /// <summary>
        /// Converts a raw camera image buffer into a Unity-compatible <see cref="TextureFormat"/> layout,
        /// delivering the converted pixel data via a temporary buffer, asynchronously.
        /// </summary>
        /// <param name="srcData"> The raw interleaved or planar source image data.</param>
        /// <param name="srcWidth">Width of the full source image, in pixels.</param>
        /// <param name="srcHeight">Height of the full source image, in pixels.</param>
        /// <param name="srcFormat">The ARFoundation source format (<see cref="XRCpuImage.Format"/>).</param>
        /// <param name="conversionParams">
        /// The conversion configuration specifying the crop rectangle, output dimensions,
        /// target texture format, and transformation flags (mirror).
        /// </param>
        /// <param name="onComplete">
        /// Completion callback with the data that represents the converted image.
        /// The data is only valid until the next Unity frame.
        /// </param>
        /// <returns><c>true</c> if the conversion succeeded; otherwise, <c>false</c>.</returns>
        public static void TryConvertOnGpuAsync(NativeArray<byte> srcData, int srcWidth, int srcHeight,
            XRCpuImage.Format srcFormat, XRCpuImage.ConversionParams conversionParams, Action<NativeArray<byte>?> onComplete)
        {
            // Currently, this API uses the built-in blit shader,
            // which doesn't support biplanar input
            if (srcFormat
                is XRCpuImage.Format.IosYpCbCr420_8BiPlanarFullRange
                or XRCpuImage.Format.AndroidYuv420_888)
            {
                Log.Error("BpiPlanar image conversion is not supported on the GPU.");
                onComplete?.Invoke(null);
                return;
            }

            // Verify graphics format support on the platform
            InferGraphicsFormats(
                src: srcFormat.AsTextureFormat(),
                dst: conversionParams.outputFormat,
                out var inputGraphicsFormat,
                out var outputGraphicsFormat);

            if (!IsSampleFormatSupported(inputGraphicsFormat) ||
                !IsRenderFormatSupported(outputGraphicsFormat))
            {
                onComplete?.Invoke(null);
                return;
            }

            // Prepare the source image
            var sourceTexture = new Texture2D(srcWidth, srcHeight, inputGraphicsFormat,
                TextureCreationFlags.DontInitializePixels);
            if (srcFormat.AsTextureFormat() == TextureFormat.RGB24)
            {
                // To provide wider GPU support, we use RGBA32 instead of RGB24 input format
                // Here, we need to convert the original RGB24 input to RGBA32
                var rgbaData = new NativeArray<byte>(
                    srcWidth * srcHeight * 4,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
                BurstImageUtils.InvokeExpandRgbToRgba(srcData, rgbaData);

                try
                {
                    sourceTexture.LoadRawTextureData(rgbaData);
                }
                finally
                {
                    rgbaData.Dispose();
                }
            }
            else
            {
                sourceTexture.LoadRawTextureData(srcData);
            }
            sourceTexture.Apply(false, true);
            CalculateScaleAndOffset(sourceTexture, conversionParams, out var scale, out var offset);

            // Grab an intermediate texture
            var intermediate = RenderTexture.GetTemporary(conversionParams.outputDimensions.x,
                conversionParams.outputDimensions.y, 0, outputGraphicsFormat);

            // Cache and set active RT
            var cachedRenderTarget = RenderTexture.active;
            RenderTexture.active = intermediate;

            // Render the texture
            Graphics.Blit(sourceTexture, intermediate, scale, offset);

            // Revert state
            RenderTexture.active = cachedRenderTarget;

            // Async GPU readback
            AsyncGPUReadback.Request(intermediate, 0, request =>
            {
                try
                {
                    if (request.hasError)
                    {
                        Log.Error("GPU readback failed");
                        onComplete?.Invoke(null);
                        return;
                    }

                    var cpuData = request.GetData<byte>();

                    // In case of TextureFormat.RGB24, the graphics format defers to RGBA32 (for wider support)
                    // so we need to strip the alpha channel to produce the final output manually
                    if (conversionParams.outputFormat == TextureFormat.RGB24)
                    {
                        var rgbData = new NativeArray<byte>(
                            conversionParams.outputDimensions.x * conversionParams.outputDimensions.y * 3,
                            Allocator.Temp,
                            NativeArrayOptions.UninitializedMemory);
                        BurstImageUtils.InvokeReduceRgbaToRgb(cpuData, rgbData);

                        try
                        {
                            onComplete?.Invoke(rgbData);
                        }
                        finally
                        {
                            rgbData.Dispose();
                        }
                    }
                    else
                    {
                        onComplete?.Invoke(cpuData);
                    }
                }
                finally
                {
                    // Release resources
                    UnityObjectUtils.Destroy(sourceTexture);
                    RenderTexture.ReleaseTemporary(intermediate);
                }
            });
        }

        /// <summary>
        /// To be used with Graphics.Blit();
        /// Calculates the scale and offset values for the source texture,
        /// based on the specified XRCpuImage.ConversionParams.
        /// </summary>
        private static void CalculateScaleAndOffset(Texture sourceTexture, XRCpuImage.ConversionParams conversionParams,
            out Vector2 scale, out Vector2 offset)
        {
            scale = new Vector2(
                (float)conversionParams.inputRect.width / sourceTexture.width,
                (float)conversionParams.inputRect.height / sourceTexture.height
            );
            offset = new Vector2(
                (float)conversionParams.inputRect.x / sourceTexture.width,
                (float)conversionParams.inputRect.y / sourceTexture.height
            );
            switch (conversionParams.transformation)
            {
                case XRCpuImage.Transformation.MirrorX: scale.y *= -1; break;
                case XRCpuImage.Transformation.MirrorY: scale.x *= -1; break;
            }
        }

        /// <summary>
        /// Infers the appropriate graphics formats to use for the input and output textures during GPU conversion.
        /// </summary>
        private static void InferGraphicsFormats(TextureFormat src, TextureFormat dst,
            out GraphicsFormat srcFormat, out GraphicsFormat dstFormat)
        {
            // Convert unsupported RGB24 to RGBA32 internally
            if (src == TextureFormat.RGB24)
            {
                src = TextureFormat.RGBA32;
            }

            if (dst == TextureFormat.RGB24)
            {
                dst = TextureFormat.RGBA32;
            }

            // Assuming R8 and RFloat are data formats, meaning 127/255 is 0.5f (linear).
            // While RFloat will almost always translate to a linear format, R8 will
            // often default to sRGB. Assuming we use R8 for depth confidence, we need
            // to explicitly specify the format as linear.
            var isDataFormatInput = src is TextureFormat.R8 or TextureFormat.RFloat;
            var isDataFormatOutput = dst is TextureFormat.R8 or TextureFormat.RFloat;
            srcFormat = GraphicsFormatUtility.GetGraphicsFormat(src, !isDataFormatInput);
            dstFormat = GraphicsFormatUtility.GetGraphicsFormat(dst, !isDataFormatOutput);
        }

        private static bool IsSampleFormatSupported(GraphicsFormat format)
        {
#if UNITY_6000_0_OR_NEWER
            if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample))
#else
            if (!SystemInfo.IsFormatSupported(format, FormatUsage.Sample))
#endif
            {
                Log.Error($"Graphics format {format} for sampling is not supported on this platform.");
                return false;
            }

            return true;
        }

        private static bool IsRenderFormatSupported(GraphicsFormat format)
        {
#if UNITY_6000_0_OR_NEWER
            if (!SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render))
#else
            if (!SystemInfo.IsFormatSupported(format, FormatUsage.Render))
#endif
            {
                Log.Error($"Graphics format {format} for rendering is not supported on this platform.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the conversion from one <see cref="XRCpuImage.Format"/> to a target
        /// <see cref="XRCpuImage.Format"/> is supported by the CPU image conversion pipeline.
        /// </summary>
        /// <param name="src">The source image format.</param>
        /// <param name="dst">The destination image format.</param>
        /// <returns><c>true</c> if the conversion is supported; otherwise, <c>false</c>.</returns>
        internal static bool IsCpuConversionSupported(XRCpuImage.Format src, XRCpuImage.Format dst)
        {
            switch (src)
            {
                // --- RGB / RGBA formats ---
                case XRCpuImage.Format.RGB24:
                    return dst is XRCpuImage.Format.AndroidYuv420_888;

                // --- Platform-specific YUV formats ---
                case XRCpuImage.Format.AndroidYuv420_888:
                case XRCpuImage.Format.IosYpCbCr420_8BiPlanarFullRange:
                    // YUV to RGBA conversion only
                    return dst == XRCpuImage.Format.RGBA32;

                // --- Unknown or unsupported ---
                case XRCpuImage.Format.Unknown:
                default:
                    return false;
            }
        }

        /// <summary>
        /// Checks if the conversion from one <see cref="XRCpuImage.Format"/> to a target
        /// <see cref="TextureFormat"/> is supported by the GPU image conversion pipeline.
        /// </summary>
        /// <param name="src">The source image format.</param>
        /// <param name="dst">The destination texture format.</param>
        /// <returns><c>true</c> if the conversion is supported; otherwise, <c>false</c>.</returns>
        internal static bool IsGpuConversionSupported(XRCpuImage.Format src, TextureFormat dst)
        {
            switch (src)
            {
                // --- Depth formats ---
                case XRCpuImage.Format.DepthFloat32:
                    return dst is TextureFormat.RFloat;

                // --- Single-channel grayscale formats ---
                case XRCpuImage.Format.OneComponent8:
                    return dst is TextureFormat.R8 or TextureFormat.Alpha8;

                // --- RGB / RGBA formats ---
                case XRCpuImage.Format.RGB24:
                case XRCpuImage.Format.RGBA32:
                case XRCpuImage.Format.BGRA32:
                case XRCpuImage.Format.ARGB32:
                    return dst is TextureFormat.RGBA32 or TextureFormat.RGB24;

                // --- Unknown or unsupported ---
                case XRCpuImage.Format.Unknown:
                default:
                    return false;
            }
        }

        /// <summary>
        /// Calculates the number of bytes required to store an image of the given dimensions
        /// in the specified <see cref="TextureFormat"/>.
        /// Returns <c>-1</c> for invalid dimensions or unsupported formats.
        /// </summary>
        internal static int GetConvertedDataSize(TextureFormat format, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return -1;
            }

            switch (format)
            {
                case TextureFormat.RFloat:
                    // 32 bits (4 bytes) per pixel
                    return width * height * sizeof(float);

                case TextureFormat.R8:
                case TextureFormat.Alpha8:
                    // 8 bits (1 byte) per pixel
                    return width * height;

                case TextureFormat.RGBA32:
                case TextureFormat.BGRA32:
                case TextureFormat.ARGB32:
                    // 32 bits (4 bytes) per pixel
                    return width * height * 4;

                case TextureFormat.RGB24:
                    // 24 bits (3 bytes) per pixel
                    return width * height * 3;

                default:
                    // Unsupported / unknown format
                    return -1;
            }
        }

        /// <summary>
        /// Retrieves the number of bytes required to store an image with the given dimensions
        /// and native <see cref="XRCpuImage.Format"/> by invoking the corresponding native API.
        ///
        /// Returns <c>-1</c> if the format is unsupported or the native function reports an error.
        /// </summary>
        internal static int GetConvertedDataSize(XRCpuImage.Format format, int width, int height) =>
            NativeApi.GetConvertedDataSize(ToNativeImageFormat(format), width, height);

        /// <summary>
        /// Determines the corresponding native <see cref="ImageFormatCEnum"/> for a given
        /// cpu image format by inspecting the image's memory layout.
        /// Returns <see cref="ImageFormatCEnum.Unknown"/> if the format cannot be determined.
        /// </summary>
        /// <remarks>
        /// This function includes a special case for android to determine the image format
        /// by inspecting the U and V planes.
        /// </remarks>>
        internal static ImageFormatCEnum ToNativeImageFormat(XRCpuImage.Format format, int planeCount, IntPtr plane1Ptr,
            IntPtr plane2Ptr)
        {
            // Inspect the image planes to determine the correct format for Android
            if (format == XRCpuImage.Format.AndroidYuv420_888)
            {
                switch (planeCount)
                {
                    case 2:
                        // TODO(ahegedus): Make a case for interleaved NV12
                        return ImageFormatCEnum.Yuv420_NV21;

                    case 3:
                    {
                        // Make a case for non-interleaved NV12 and NV21 formats
                        long distance = (long)plane1Ptr - (long)plane2Ptr;
                        return distance switch
                        {
                            // VU, U plane is larger than V by 1 byte
                            1 => ImageFormatCEnum.Yuv420_NV21,
                            // UV, V plane is larger than U by 1 byte
                            -1 => ImageFormatCEnum.Yuv420_NV12,
                            // I420
                            _ => ImageFormatCEnum.Yuv420_888
                        };
                    }

                    default:
                        return ImageFormatCEnum.Unknown;
                }
            }

            // Simple format conversions
            return ToNativeImageFormat(format);
        }

        /// <summary>
        /// Maps Unity's <see cref="XRCpuImage.Format"/> (the image format provided by ARFoundation)
        /// to the corresponding native <see cref="ImageFormatCEnum"/> used by the ARDK native layer.
        /// Returns <see cref="ImageFormatCEnum.Unknown"/> for unhandled or unknown formats.
        /// </summary>
        internal static ImageFormatCEnum ToNativeImageFormat(XRCpuImage.Format format)
        {
            switch (format)
            {
                case XRCpuImage.Format.AndroidYuv420_888:
                    return ImageFormatCEnum.Yuv420_888;

                case XRCpuImage.Format.IosYpCbCr420_8BiPlanarFullRange:
                    return ImageFormatCEnum.Yuv420_NV12;

                case XRCpuImage.Format.OneComponent8:
                    return ImageFormatCEnum.OneComponent8;

                case XRCpuImage.Format.DepthFloat32:
                    return ImageFormatCEnum.DepthFloat32;

                case XRCpuImage.Format.DepthUint16:
                    return ImageFormatCEnum.DepthUint16;

                case XRCpuImage.Format.OneComponent32:
                    return ImageFormatCEnum.OneComponent32;

                case XRCpuImage.Format.ARGB32:
                    return ImageFormatCEnum.ARGB32;

                case XRCpuImage.Format.RGBA32:
                    return ImageFormatCEnum.RGBA32;

                case XRCpuImage.Format.BGRA32:
                    return ImageFormatCEnum.BGRA32;

                case XRCpuImage.Format.RGB24:
                    return ImageFormatCEnum.RGB24;

                case XRCpuImage.Format.Unknown:
                default:
                    return ImageFormatCEnum.Unknown;
            }
        }

        /// <summary>
        /// Native bindings.
        /// </summary>
        private static class NativeApi
        {
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Image_TryConvert")]
            public static extern bool TryConvert(
                IntPtr srcPlane0, int plane0Stride,
                IntPtr srcPlane1, int plane1Stride,
                IntPtr srcPlane2, int plane2Stride,
                int srcWidth, int srcHeight,
                ImageFormatCEnum srcFormat,
                IntPtr dstData,
                ImageFormatCEnum dstFormat
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Image_GetConvertedDataSize")]
            public static extern int GetConvertedDataSize(ImageFormatCEnum format, int width, int height);
        }
    }
}
