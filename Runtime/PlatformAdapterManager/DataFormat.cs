// Copyright 2022-2024 Niantic.

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
        
        // CPU image will come with the image timestamp, image intrinsics, and its
        // data.
        kCpuRgb_256_256_Uint8 = 5,

        // JPEG image will come with the image timestamp, image intrinsics, and its
        // data.
        kJpeg_720_540_Uint8 = 6,

        // GPS location data will come with the timestamp in seconds, the latitude
        // and longitude, the altitude, and the vertical and horizontal accuracies
        kGpsLocation = 7,

        // Compass data
        kCompass = 8,

        // Full resolution JPEG images, the resolution might be different
        // on different devices.
        kJpeg_full_res_Uint8 = 9,

        // Platform depth data
        kPlatform_depth = 10,
    }
}
