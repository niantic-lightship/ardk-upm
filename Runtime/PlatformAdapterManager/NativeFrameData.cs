// Copyright 2022-2024 Niantic.
using System;
using System.Runtime.InteropServices;

namespace Niantic.Lightship.AR.PAM
{
    // IMPORTANT
    // If you change this file, please add multiple Unity and NativeC reviewers

    // C# to C struct. Must match frame_data.h
    public enum ImageFormatCEnum : uint
    {
        Unknown = 0,

        // Bi-planar Y'CbCr 4:2:0 format
        // NV12, UV are interleaved into one plane.
        Yuv420_NV12 = 1,

        // Bi-planar Y'CbCr 4:2:0 format
        // NV21, VU are interleaved into one plane.
        Yuv420_NV21 = 2,

        // Tri-planar YUV 8-bit 4:2:0 separate planes
        Yuv420_888 = 3,

        // Single channel with 8 bits per pixel
        OneComponent8 = 4,

        // IEEE754-2008 binary32 float, describing the depth (distance to an object)
        DepthFloat32 = 5,

        // 16-bit unsigned integer, describing the depth (distance to an object) in
        DepthUint16 = 6,

        // Single channel image format with 32 bits per pixel
        OneComponent32 = 7,

        // 4 channel image with 8 bits per channel, describing the color in ARGB order
        ARGB32 = 8,

        // 4 channel image with 8 bits per channel, describing the color in RGBA order
        RGBA32 = 9,

        // 4 channel image with 8 bits per channel, describing the color in BGRA order
        BGRA32 = 10,

        // 3 8-bit unsigned integer channels, describing the color in RGB order
        RGB24 = 11,
    }

    // IMPORTANT – This struct has explicit matching C# and pure-C alignment/padding requirements
    // C# to C struct. Must match compass.h
    [StructLayout(LayoutKind.Sequential)]
    internal struct CompassDataCStruct
    {
        // Compass data timestamp in milliseconds since epoch
        // Zero, if this data was not captured
        public ulong TimestampMs;

        // Accuracy of heading reading in degrees.
        public float HeadingAccuracy;

        // The heading in degrees relative to the magnetic North Pole.
        public float MagneticHeading;

        // The raw geomagnetic data measured in microteslas.
        public float RawDataX;
        public float RawDataY;
        public float RawDataZ;

        // The heading in degrees relative to the geographic North Pole.
        public float TrueHeading;
    }

    // IMPORTANT – This struct has explicit matching C# and pure-C alignment/padding requirements
    // C# to C struct. Must match gps_position.h
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpsLocationCStruct
    {
        // GPS data timestamp in milliseconds since epoch
        // Zero, if this data was not captured
        public ulong TimestampMs;

        // GPS location latitude
        public float Latitude;
        // GPS location longitude
        public float Longitude;
        // GPS location altitude
        public float Altitude;

        // GPS vertical accuracy
        public float VerticalAccuracy;
        // GPS horizontal accuracy
        public float HorizontalAccuracy;
    }

    // IMPORTANT – This struct has explicit matching C# and pure-C alignment/padding requirements
    // C# to C struct. Must match frame_data.h
    [StructLayout(LayoutKind.Sequential)]
    internal struct CameraIntrinsicsCStruct
    {
        public float FocalLengthX;
        public float FocalLengthY;
        public float PrincipalPointX;
        public float PrincipalPointY;
        public uint ResolutionX;
        public uint ResolutionY;

        public CameraIntrinsicsCStruct
        (
            UnityEngine.Vector2 focalLength,
            UnityEngine.Vector2 principalPoint,
            UnityEngine.Vector2Int resolution
        )
        {
            FocalLengthX = focalLength.x;
            FocalLengthY = focalLength.y;
            PrincipalPointX = principalPoint.x;
            PrincipalPointY = principalPoint.y;
            ResolutionX = (uint)resolution.x;
            ResolutionY = (uint)resolution.y;
        }

        public void SetIntrinsics
        (
            UnityEngine.Vector2 focalLength,
            UnityEngine.Vector2 principalPoint,
            UnityEngine.Vector2Int resolution
        )
        {
            FocalLengthX = focalLength.x;
            FocalLengthY = focalLength.y;
            PrincipalPointX = principalPoint.x;
            PrincipalPointY = principalPoint.y;
            ResolutionX = (uint)resolution.x;
            ResolutionY = (uint)resolution.y;
        }
    }

    // IMPORTANT – This struct has explicit matching C# and pure-C alignment/padding requirements
    // C# to C struct. Must match frame_data.h
    [StructLayout(LayoutKind.Sequential)]
    internal struct TransformCStruct
    {
        public float TranslationX;
        public float TranslationY;
        public float TranslationZ;
        public float ScaleXYZ;
        public float OrientationX;
        public float OrientationY;
        public float OrientationZ;
        public float OrientationW;

        public void SetTransform(UnityEngine.Matrix4x4 ardkMatrix)
        {
            UnityEngine.Vector3 translation = ardkMatrix.GetPosition();
            TranslationX = translation.x;
            TranslationY = translation.y;
            TranslationZ = translation.z;
            // We use the uniform scale from any of the components
            ScaleXYZ = ardkMatrix.lossyScale.x;

            var orientation = ardkMatrix.rotation;
            OrientationX = orientation.x;
            OrientationY = orientation.y;
            OrientationZ = orientation.z;
            OrientationW = orientation.w;
        }
    }

    // IMPORTANT – This struct has explicit matching C# and pure-C alignment/padding requirements
    // C# to C struct. Must match frame_data.h
    [StructLayout(LayoutKind.Sequential)]
    internal struct CameraPlaneCStruct
    {
        public void SetImagePlane(LightshipCpuImagePlane plane)
        {
            DataPtr = plane.DataPtr;
            DataSize = plane.DataSize;
            PixelStride = plane.PixelStride;
            RowStride = plane.RowStride;
        }

        public IntPtr DataPtr;
        public uint DataSize;
        public uint PixelStride;
        public uint RowStride;
        private uint Padding;
    }

    // IMPORTANT – This struct has explicit matching C# and pure-C alignment/padding requirements
    // C# to C struct. Must match frame_data.h
    [StructLayout(LayoutKind.Sequential)]
    internal struct ARDKFrameData
    {
        // Most recent Compass data from the device
        // 64b aligned struct
        public CompassDataCStruct CompassData;

        // Most recent GPS data from the device
        // 64b aligned struct
        public GpsLocationCStruct GpsLocation;

        // Camera pose and image timestamp in milliseconds since epoch
        // Note, this is not strictly the exact timestamp for all devices as of May 2024 (e.g. Magic Leap)
        public ulong CameraTimestampMs;

        // Camera image plane data
        public CameraPlaneCStruct CameraImagePlane0;
        public CameraPlaneCStruct CameraImagePlane1;
        public CameraPlaneCStruct CameraImagePlane2;

        // Platform depth data
        public IntPtr DepthDataPtr;
        // Platform depth confidence data
        public IntPtr DepthConfidencesDataPtr;
        // Platform Depth image intrinsics with image resolution
        public CameraIntrinsicsCStruct DepthCameraIntrinsics;

        // An unique Id to identify the current frame across frames within the same run
        public uint FrameId;

        // Camera pose of the current frame as a 4x4 float matrix.
        public TransformCStruct CameraPose;

        // Camera intrinsics
        public CameraIntrinsicsCStruct CameraIntrinsics;

        // The width of the raw camera image.
        public uint CameraImageWidth;
        // The height of the raw camera image.
        public uint CameraImageHeight;
        // Camera image format
        public ImageFormatCEnum CameraImageFormat;

        // Length of the depth float buffer (same value as depth confidence buffer as well)
        public uint DepthAndConfidenceDataLength;

        // Width of the depth float buffer
        public uint DepthDataWidth;
        // Height of the depth float buffer
        public uint DepthDataHeight;

        // Orientation of current frame. See orientation.h for definitions
        // Use XREnumConversions.FromUnityToArdk() to calculate the correct value
        public uint ScreenOrientation;

        // Tracking state of current frame. See tracking_state.h for definitions.
        // Use XREnumConversions.FromUnityToArdk() to calculate the correct value
        public uint TrackingState;
    }
}
