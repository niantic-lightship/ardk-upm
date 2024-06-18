// Copyright 2022-2024 Niantic.
using System;
using System.Runtime.InteropServices;

namespace Niantic.Lightship.AR.PAM
{
    // C struct for C# to send frame data to C++. Defined in system_adapter_handler_api.h file.
    // Note:
    //   The lengths of color image data are sent as the length in bytes (i.e. sizeof(pixel) * width * height),
    //   while the lengths of primitive data buffers (intrinsics, platform depth) are equal to the number of elements.
    [StructLayout(LayoutKind.Sequential)]
    internal struct DeprecatedFrameCStruct
    {
        // An Id to identify the current frame, this Id is promised to be different
        // across frames within the same run.
        public UInt32 FrameId;

        // Timestamp in milliseconds that the frame's pose and camera images were generated
        public UInt64 TimestampMs;

        // Camera pose of the current frame as a 4x4 float matrix.
        public IntPtr CameraPose;

        // Length of the pose array.
        public UInt32 CameraPoseLength;

        // Orientation of current frame. See orientation.h for definitions, and use the method
        // XREnumConversions.FromUnityToArdk(orientation) to calculate the correct int value.
        // Note: It's called "kDeviceOrientation" in "data_format.h", but based on how the ARDK_Orientation enum is
        // defined in C++, it more aptly corresponds to Unity's "ScreenOrientation," which excludes FaceDown and FaceUp
        // as possible values, instead returning the last known screen orientation when those device orientations occur.
        public UInt32 ScreenOrientation;

        // Tracking state of current frame. See tracking_state.h for definitions.
        public UInt32 TrackingState;

        // The width of the raw camera image.
        public UInt32 CameraImageWidth;

        // The height of the raw camera image.
        public UInt32 CameraImageHeight;

        // CPU RGBA image data with resolution [256, 144], in format of uint8.
        public IntPtr CpuRgba256x144ImageData;

        // Length of the CPU RGBA image with resolution [256, 144]
        public UInt32 CpuRgba256x144ImageDataLength;

        // Camera intrinsics of the CPU RGBA image with resolution [256, 144]
        public IntPtr CpuRgba256x144CameraIntrinsics;

        // Length of the awareness camera intrinsics
        public UInt32 Rgba256x144CameraIntrinsicsLength;

        // CPU RGB image data with resolution [256, 256], in format of uint8.
        public IntPtr CpuRgb256x256ImageData;

        // Length of the CPU RGB image with resolution [256, 256]
        public UInt32 CpuRgb256x256ImageDataLength;

        // Camera intrinsics of the CPU RGB image with resolution [256, 256]
        public IntPtr CpuRgb256x256CameraIntrinsics;

        // Length of the awareness camera intrinsics
        public UInt32 Rgb256x256CameraIntrinsicsLength;

        // JPEG image data with resolution [720, 540] and compression quality 90%, in uint8 format.
        public IntPtr CpuJpeg720x540ImageData;

        // Length of the JPEG image with resolution [720, 540]
        public UInt32 CpuJpeg720x540ImageDataLength;

        // Camera intrinsics of the JPEG image with resolution [720, 540]
        public IntPtr CpuJpeg720x540CameraIntrinsics;

        // Length of the VPS camera intrinsics
        public UInt32 Jpeg720x540CameraIntrinsicsLength;

        // Most recent GPS data from the device
        public IntPtr GpsLocationData;

        // Size of the GPS data buffer, in bytes
        public UInt32 GpsLocationLength;

        // Most recent Compass data from the device
        public IntPtr CompassData;

        // Length of the Compass data buffer.
        public UInt32 CompassDataLength;

        // JPEG image data with full resolution and compression quality 90%, in uint8 format.
        public IntPtr CpuJpegFullResImageData;

        // Width of the full resolution JPEG image.
        public UInt32 CpuJpegFullResImageWidth;

        // Height of the full resolution JPEG image.
        public UInt32 CpuJpegFullResImageHeight;

       // Length of the JPEG image with full resolution.
        public UInt32 CpuJpegFullResImageDataLength;

        // Camera intrinsics of the JPEG image with full resolution.
        public IntPtr CpuJpegFullResCameraIntrinsics;

        // Length of the full resolution camera image's intrinsics
        public UInt32 JpegFullResCameraIntrinsicsLength;

        // Depth float buffer
        public IntPtr PlatformDepthData;

        // Depth confidence uint8 buffer
        public IntPtr PlatformDepthConfidencesData;

        // Width of the depth float buffer
        public UInt32 PlatformDepthDataWidth;

        // Height of the depth float buffer
        public UInt32 PlatformDepthDataHeight;

        // Length of the depth float buffer (same value as depth confidence buffer as well)
        public UInt32 PlatformDepthDataLength;

        // Camera intrinsics of the platform depth buffer
        public IntPtr PlatformDepthCameraIntrinsics;

        // Length of the platform depth buffer camera intrinsics
        public UInt32 PlatformDepthCameraIntrinsicsLength;
    }
}
