// Copyright 2022-2024 Niantic.

using System.ComponentModel;

namespace Niantic.Lightship.AR.PAM
{
    // This enum class need to precisely match C++ data_format.h
    internal enum DataFormat : uint // UInt32
    {
        kNone = 0,

        // Pose will embedded with pose timestamp, transform and tracking state
        kPose = 1,
        kDeviceOrientation = 2,
        kTrackingState = 3,

        // CPU Image with intrinsics – 256x144 R8G8B8A8
        kCpuRgba_256_144_Uint8 = 4,

        // CPU Image with intrinsics – 256x256 R8G8B8
        kCpuRgb_256_256_Uint8 = 5,

        // JPEG image with timestamp, intrinsics and data
        kJpeg_720_540_Uint8 = 6,

        // GPS location with timestamp, latitude/longitude, altitude, and vertical/horizontal accuracies
        kGpsLocation = 7,

        // Compass data
        kCompass = 8,

        // Full resolution JPEG, the resolution will vary across devices
        kJpeg_full_res_Uint8 = 9,

        // Platform depth data
        kPlatform_depth = 10,

        // CPU Image with intrinsics – 384x216 R8G8B8
        kCpuRgb_384_216_Uint8 = 11,
    }

    internal static class DataFormatNames
    {
        public static string GetName(DataFormat value)
        {
            switch (value)
            {
                case DataFormat.kNone:
                    return "None";
                case DataFormat.kPose:
                    return "Pose";
                case DataFormat.kDeviceOrientation:
                    return "Orientation";
                case DataFormat.kTrackingState:
                    return "Tracking";
                case DataFormat.kCpuRgba_256_144_Uint8:
                    return "Rgba256x144";
                case DataFormat.kCpuRgb_256_256_Uint8:
                    return "Rgb256x256";
                case DataFormat.kJpeg_720_540_Uint8:
                    return "Jpeg720x540";
                case DataFormat.kGpsLocation:
                    return "Gps";
                case DataFormat.kCompass:
                    return "Compass";
                case DataFormat.kJpeg_full_res_Uint8:
                    return "JpegFull";
                case DataFormat.kPlatform_depth:
                    return "Depth";
                case DataFormat.kCpuRgb_384_216_Uint8:
                    return "Rgb384x216";
                default:
                    return "NotImplemented";
            }
        }
    }
}
