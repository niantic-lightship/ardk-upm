// Copyright 2022-2025 Niantic.
using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.PAM;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Niantic.Lightship.AR.Common
{
    internal static class ImageConversionUtils
    {
        /// <summary>
        /// Converts the image data from one format to another.
        /// </summary>
        /// <param name="sourceData">The source image data.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="sourceFormat">The format of the source image data.</param>
        /// <param name="convertedFormat">The format to convert the image data to.</param>
        /// <param name="convertedData">The converted image data.</param>
        /// <returns>True if the conversion was successful; otherwise, false.</returns>
        /// <exception cref="NotSupportedException">Thrown if the conversion is not supported.</exception>
        public static bool Convert(NativeArray<byte> sourceData, int width, int height, ImageFormatCEnum sourceFormat,
            ImageFormatCEnum convertedFormat, out NativeArray<byte> convertedData)
        {
            // Check if the conversion is supported
            if (!IsSupported(sourceFormat, convertedFormat))
            {
                throw new NotSupportedException(
                    $"Conversion from {sourceFormat} to {convertedFormat} is not supported.");
            }

            // Allocate the buffer for the converted data
            var length = NativeApi.GetConvertedDataSize(convertedFormat, width, height);
            convertedData = new NativeArray<byte>(length, Allocator.Persistent);

            // Get the data pointers
            IntPtr src, dst;
            unsafe
            {
                src = new IntPtr(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceData));
                dst = new IntPtr(NativeArrayUnsafeUtility.GetUnsafePtr(convertedData));
            }

            // Convert
            return NativeApi.Convert(src, dst, sourceFormat, convertedFormat, width, height);
        }

        /// <summary>
        /// Checks if the conversion from one format to another is supported.
        /// </summary>
        /// <param name="src">The source image format.</param>
        /// <param name="dst">The destination image format.</param>
        /// <returns>>True if the conversion is supported; otherwise, false.</returns>
        private static bool IsSupported(ImageFormatCEnum src, ImageFormatCEnum dst)
        {
            // Only RGB24 to YUV420_888 (as kYuvI420) is supported for now
            return src == ImageFormatCEnum.RGB24 && dst == ImageFormatCEnum.Yuv420_888;
        }

        private static class NativeApi
        {
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Image_Convert")]
            public static extern bool Convert(IntPtr srcData, IntPtr dstData, ImageFormatCEnum srcFormat,
                ImageFormatCEnum dstFormat, int width, int height);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Image_GetConvertedDataSize")]
            public static extern int GetConvertedDataSize(ImageFormatCEnum format, int width, int height);
        }
    }
}
