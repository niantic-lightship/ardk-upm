// Copyright 2022-2024 Niantic.

using System;
using System.Text;

namespace Niantic.Lightship.AR.PAM
{
    // This enum class need to precisely match C++ data_format.h
    [Flags]
    internal enum DataFormatFlags : uint
    {
        kNone = 0,

        // Pose will embedded with pose timestamp, transform and tracking state
        kPose = 1 << 0,
        kDeviceOrientation = 1 << 1,
        kTrackingState = 1 << 2,

        // CPU Image with intrinsics – 256x144 R8G8B8A8
        kCpuRgba_256_144_Uint8 = 1 << 3,

        // CPU Image with intrinsics – 256x256 R8G8B8
        kCpuRgb_256_256_Uint8 = 1 << 4,

        // JPEG image with timestamp, intrinsics and data
        kJpeg_720_540_Uint8 = 1 << 5,

        // GPS location with timestamp, latitude/longitude, altitude, and vertical/horizontal accuracies
        kGpsLocation = 1 << 6,

        // Compass data
        kCompass = 1 << 7,

        // Full resolution JPEG, the resolution will vary across devices
        kJpeg_full_res_Uint8 = 1 << 8,

        // Platform depth data
        kPlatform_depth = 1 << 9,

        // CPU Image with intrinsics – 384x216 R8G8B8
        kCpuRgb_384x216_Uint8 = 1 << 10,
    }

    internal static class DataFormatUtils
    {

        public static string FlagsToString(DataFormatFlags flags)
        {
            StringBuilder flagNames = new StringBuilder("_");

            foreach (DataFormatFlags flag in Enum.GetValues(typeof(DataFormatFlags)))
            {
                if ((flags & flag) == flag)
                {
                    if (flagNames.Length > 1)
                    {
                        flagNames.Append('_');
                    }
                    flagNames.Append(flag.ToString());
                }
            }

            return flagNames.ToString();
        }
    }
}
