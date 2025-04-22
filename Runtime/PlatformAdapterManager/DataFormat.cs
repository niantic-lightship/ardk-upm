// Copyright 2022-2025 Niantic.

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

        // Image data
        kImage = 1 << 3,

        // GPS location with timestamp, latitude/longitude, altitude, and vertical/horizontal accuracies
        kGpsLocation = 1 << 4,

        // Compass data
        kCompass = 1 << 5,

        // Platform depth data
        kPlatform_depth = 1 << 6,
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
