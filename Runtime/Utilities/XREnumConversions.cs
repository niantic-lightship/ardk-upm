// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.PAM;
using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    internal static class XREnumConversions
    {
        // Translates ARFoundation's TrackingState enum into the corresponding Lightship tracking state values
        // defined in tracking_state.h
        // Note: ARFoundation has no "Failed" state corresponding to Lightship's
        public static byte FromUnityToArdk(this TrackingState state)
        {
            switch (state)
            {
                case TrackingState.None:
                    return 0; // Unknown
                case TrackingState.Limited:
                    return 2; // Poor
                case TrackingState.Tracking:
                    return 3; // Normal
                default:
                    return 0;
            }
        }

        // Translates Unity's ScreenOrientation enum into the corresponding Lightship values
        // defined in orientation.h
        public static byte FromUnityToArdk(this ScreenOrientation orientation)
        {
            switch (orientation)
            {
                case ScreenOrientation.Portrait:
                    return 1; // Portrait
                case ScreenOrientation.PortraitUpsideDown:
                    return 2; // PortraitUpsideDown
                case ScreenOrientation.LandscapeLeft:
                    return 4; // LandscapeLeft
                case ScreenOrientation.LandscapeRight:
                    return 3; // LandscapeRight
                default:
                    return 0;
            }
        }

        // Unity to ARDK Cpu Image Format
        public static ImageFormatCEnum FromUnityToArdk(this XRCpuImage.Format format, IntPtr plane1Ptr, IntPtr plane2Ptr)
        {
            switch (format)
            {
                case XRCpuImage.Format.Unknown:
                    return ImageFormatCEnum.Unknown;

                case XRCpuImage.Format.AndroidYuv420_888:
                    long distance = (long) plane1Ptr - (long) plane2Ptr;
                    // VU, U plane is larger than V by 1 byte
                    if (distance == 1) {
                        return ImageFormatCEnum.Yuv420_NV21;
                    }
                    // UV, V plane is larger than U by 1 byte
                    else if (distance == -1) {
                        return ImageFormatCEnum.Yuv420_NV12;
                    }
                    // I420
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

                default:
                    Debug.Assert(false, "Did XRCpuImage got updated? Unhandled value: " + format);
                    return ImageFormatCEnum.Unknown;
            }
        }
    }
}
