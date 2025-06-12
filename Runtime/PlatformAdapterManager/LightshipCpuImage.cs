// Copyright 2022-2025 Niantic.

using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
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

        // Helper function to get a COPY of the image plane data in a byte array. Useful for tests.
        public byte[] GetByteArrayCopy()
        {
            var bytes = new byte[DataSize];
            Marshal.Copy(DataPtr, bytes, 0, (int)DataSize);
            return bytes;
        }
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

            // Convert Unity format to ARDK format
            var format = GetFormatCEnum(xrCpuImage);
            if (format == ImageFormatCEnum.Unknown)
            {
                Debug.LogError($"LightshipCpuImage: Unsupported XRCpuImage format: {xrCpuImage.format}");
                lightshipCpuImage = default;
                return false;
            }

            // Convert the plane data to fit the format of what native expects
            // If NV12 or NV21, make sure that the UV/VU plane is in the second slot and remove the third slot
            if (format == ImageFormatCEnum.Yuv420_NV21 && xrCpuImage.planeCount == 3)
            {
                planes[1] = planes[2];
                planes[2] = new LightshipCpuImagePlane(IntPtr.Zero, 0, 0, 0);
            }
            else if (format == ImageFormatCEnum.Yuv420_NV12 && xrCpuImage.planeCount == 3)
            {
                planes[2] = new LightshipCpuImagePlane(IntPtr.Zero, 0, 0, 0);
            }

            lightshipCpuImage = new LightshipCpuImage
            (
                format,
                (uint)xrCpuImage.width,
                (uint)xrCpuImage.height
            ) {Planes = planes};

            return true;
        }

        // Unity to ARDK Cpu Image Format
        private static ImageFormatCEnum GetFormatCEnum(XRCpuImage image)
        {
            switch (image.format)
            {
                case XRCpuImage.Format.AndroidYuv420_888:
                {
                    switch (image.planeCount)
                    {
                        case 2:
                            // TODO(ahegedus): Make a case for interleaved NV12
                            return ImageFormatCEnum.Yuv420_NV21;

                        case 3:
                        {
                            // Make a case for non-interleaved NV12 and NV21 formats
                            long distance;
                            unsafe
                            {
                                var plane1Ptr = (IntPtr)image.GetPlane(1).data.GetUnsafeReadOnlyPtr();
                                var plane2Ptr = (IntPtr)image.GetPlane(2).data.GetUnsafeReadOnlyPtr();
                                distance = (long) plane1Ptr - (long) plane2Ptr;
                            }
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
                    return ImageFormatCEnum.Unknown;
                default:
                    Debug.Assert(false, "Did XRCpuImage got updated? Unhandled value: " + image.format);
                    return ImageFormatCEnum.Unknown;
            }
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
