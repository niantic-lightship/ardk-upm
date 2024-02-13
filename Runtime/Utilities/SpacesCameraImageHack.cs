// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Niantic.Lightship.Spaces
{
    internal static class SpacesCameraImageHack
    {
        private static IntPtr latestPixelData = IntPtr.Zero;

        public static IntPtr GetPixelDataToDisplay()
        {
            return latestPixelData;
        }

        public static void ImageUtilsConvertCameraImage_Added(IntPtr destination720X540)
        {
            latestPixelData = destination720X540;
        }
        public static void ImageUtilsConvertCameraImage(IntPtr source1280X720, IntPtr destination720X540)
        {
            NativeArray<UInt32> srcNativeArray;
            NativeArray<UInt32> dstNativeArray;
            unsafe
            {
                // Access the data through a native array
                srcNativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<UInt32>(
                    (void*)source1280X720, 1280 * 720, Allocator.None);

                dstNativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<UInt32>(
                    (void*)destination720X540, 720 * 540, Allocator.None);

            }

            var vScale = 720.0f / 540.0f;
            var hScale = 960.0f / 720.0f;
            for (int row = 0; row < 540; row++)
            {
                for (int col = 0; col < 720; col++)
                {
                    var x = Mathf.RoundToInt((col) * hScale) + 160;
                    var y = Mathf.RoundToInt(row * vScale);

                    dstNativeArray[(row * 720) + col] = srcNativeArray[(y * 1280) + x];
                }
            }


            latestPixelData = destination720X540;

        }
    }
}
