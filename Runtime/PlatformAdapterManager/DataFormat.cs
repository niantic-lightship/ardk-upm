// Copyright 2022-2023 Niantic.

namespace Niantic.Lightship.AR.PAM
{
    // This enum class need to precisely match C++ data_format.h
    internal enum DataFormat : uint // UInt32
    {
        kNone = 0,

        // Pose will embedded with pose timestamp, pose transform and tracking state
        // in its data.
        kPose = 1,
        kDeviceOrientation = 2,
        kTrackingState = 3,

        // CPU image will come with the image timestamp, image intrinsics, and its
        // data.
        kCpuRgba_256_144_Uint8 = 4,

        // JPEG image will come with the image timestamp, image intrinsics, and its
        // data.
        kJpeg_720_540_Uint8 = 5,

        // GPS location data will come with the timestamp in seconds, the latitude
        // and longitude, the altitude, and the vertical and horizontal accuracies
        kGpsLocation = 6,

        // Compass data
        kCompass = 7,

        // Full resolution JPEG images, the resolution might be different
        // on different devices.
        kJpeg_full_res_Uint8 = 8,

        // Platform depth data
        kPlatform_depth = 9,
    }
}
