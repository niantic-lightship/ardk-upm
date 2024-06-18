// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PAM
{
    /// <summary>
    /// A Niantic ARDK intermediate class to hold image data before sending down to native via FrameDataCStruct.
    /// Should be able to hold references to XRCpuImage, MagicLeap and others' data. Does not own any of it.
    ///
    /// Is not related to LightshipCpuImageApi.
    /// </summary>
    internal struct LightshipCpuImage
    {
        LightshipCpuImage(XRCpuImage cpuImage)
        {
            Plane0DataPtr = IntPtr.Zero;
            Plane1DataPtr = IntPtr.Zero;
            Plane2DataPtr = IntPtr.Zero;

            Format = ImageFormatCEnum.Unknown;
            Width = 0;
            Height = 0;

            FromXRCpuImage(cpuImage);
        }

        public bool FromXRCpuImage(XRCpuImage cpuImage)
        {
            if (!cpuImage.valid)
            {
                return false;
            }

            unsafe
            {
                Plane0DataPtr = (IntPtr)cpuImage.GetPlane(0).data.GetUnsafeReadOnlyPtr();
                Plane1DataPtr = (cpuImage.planeCount > 1)?
                    (IntPtr)cpuImage.GetPlane(1).data.GetUnsafeReadOnlyPtr() : IntPtr.Zero;
                Plane2DataPtr = (cpuImage.planeCount > 2)?
                    (IntPtr)cpuImage.GetPlane(2).data.GetUnsafeReadOnlyPtr() : IntPtr.Zero;
            }

            Format = cpuImage.format.FromUnityToArdk();
            Width = (uint)cpuImage.width;
            Height = (uint)cpuImage.height;
            return true;
        }

        // Camera image plane data
        public IntPtr Plane0DataPtr;
        public IntPtr Plane1DataPtr;
        public IntPtr Plane2DataPtr;

        public ImageFormatCEnum Format;

        public uint Width;
        public uint Height;
    }
}
