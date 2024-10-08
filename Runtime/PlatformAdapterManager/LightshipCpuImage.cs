// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PAM
{
    public struct LightshipCpuImagePlane
    {
        public LightshipCpuImagePlane(IntPtr dataPtr, uint dataSize, uint pixelStride, uint rowStride)
        {
            DataPtr = dataPtr;
            DataSize = dataSize;
            PixelStride = pixelStride;
            RowStride = rowStride;
        }

        // Pointer to data of image plane.
        public IntPtr DataPtr;

        // Size of data in DataPtr.
        public uint DataSize;

        // Bytes per pixel. 1 for YUV, 3 for RGB, 4 for RGBA.
        public uint PixelStride;

        // Bytes per row of pixels. Generally some multiple of width: PixelStride for RGB, or 1/2 for subsampled YUV.
        public uint RowStride;
    }

    /// <summary>
    /// A Niantic ARDK intermediate class to hold image data before sending down to native via ARDKFrameData.
    /// Should be able to hold references to XRCpuImage, MagicLeap and others' data. Does not own any of it.
    ///
    /// Is not related to LightshipCpuImageApi.
    /// </summary>
    public struct LightshipCpuImage
    {
        public const int MaxPlanes = 3;

        public static bool TryGetFromXRCpuImage(XRCpuImage xrCpuImage, out LightshipCpuImage lightshipCpuImage)
        {
            if (!xrCpuImage.valid)
            {
                lightshipCpuImage = default;
                return false;
            }

            LightshipCpuImagePlane[] planes =  new LightshipCpuImagePlane[MaxPlanes];
            for (int i = 0; i < xrCpuImage.planeCount; ++i)
            {
                unsafe
                {
                    var plane = xrCpuImage.GetPlane(i);
                    planes[i].DataPtr = (IntPtr)plane.data.GetUnsafeReadOnlyPtr();
                    planes[i].DataSize = (uint)plane.data.Length;
                    planes[i].PixelStride = (uint)plane.pixelStride;
                    planes[i].RowStride = (uint)plane.rowStride;
                }
            }

            // Passing plane 1 and 2 represents U and V on Android to determine type
            IntPtr plane1Ptr = IntPtr.Zero;
            IntPtr plane2Ptr = IntPtr.Zero;
            if (xrCpuImage.planeCount == 3) {
                plane1Ptr = planes[1].DataPtr;
                plane2Ptr = planes[2].DataPtr;
            }
            lightshipCpuImage = new LightshipCpuImage(xrCpuImage.format.FromUnityToArdk(plane1Ptr, plane2Ptr),
                (uint)xrCpuImage.width, (uint)xrCpuImage.height);
            lightshipCpuImage.Planes = planes;
            return true;
        }

        public static LightshipCpuImage Create()
        {
            return new LightshipCpuImage
            {
                Planes = new LightshipCpuImagePlane[MaxPlanes],
                Format = ImageFormatCEnum.Unknown,
                Width = 0,
                Height = 0,
            };
        }

        public LightshipCpuImage(ImageFormatCEnum format, uint width, uint height)
        {
            Planes = new LightshipCpuImagePlane[MaxPlanes];
            Format = format;
            Width = width;
            Height = height;
        }

        public LightshipCpuImagePlane[] Planes;
        public ImageFormatCEnum Format;
        public uint Width;
        public uint Height;
    }
}
