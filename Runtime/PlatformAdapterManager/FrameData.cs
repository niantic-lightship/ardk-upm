// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.PAM;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Niantic.Lightship.AR.PAM
{
    internal class FrameData
    {
        internal FrameCStruct _frameCStruct;

        private NativeArray<float> _poseData;

        // Location data
        private NativeArray<GpsLocation> _gpsData;
        private NativeArray<CompassData> _compassData;

        // RGBA 256x144 image data
        private NativeArray<byte> _rgba256x144ImageData;
        private NativeArray<float> _rgba256x144CameraIntrinsicsData;
        private readonly Vector2Int _rgba256x144ImageResolution =
            new(DataFormatConstants.Rgba_256_144_ImgWidth, DataFormatConstants.Rgba_256_144_ImgHeight);

        // RGB 256x256 image data
        private NativeArray<byte> _rgb256x256ImageData;
        private NativeArray<float> _rgb256x256CameraIntrinsicsData;
        private readonly Vector2Int _rgb256x256ImageResolution =
            new(DataFormatConstants.Rgb_256_256_ImgWidth, DataFormatConstants.Rgb_256_256_ImgHeight);

        // JPEG 720x540 image data
        private NativeArray<byte> _cpuJpeg720X540ImageData;
        private NativeArray<float> _jpeg720X540CameraIntrinsicsData;
        private readonly Vector2Int _jpeg720X540ImageResolution =
            new(DataFormatConstants.Jpeg_720_540_ImgWidth, DataFormatConstants.Jpeg_720_540_ImgHeight);

        // JPEG full resolution data.
        private NativeArray<byte> _cpuJpegFullResImageData;
        private UInt32 _jpegFullResImageLength = 0;
        private NativeArray<float> _jpegFullResCameraIntrinsicsData;
        private Vector2Int _jpegFullResImageResolution = new(0, 0);

        // Platform depth data
        private NativeArray<float> _platformDepthData;
        private NativeArray<byte> _platformDepthConfidences;
        private Vector2Int _platformDepthResolution;
        private NativeArray<float> _platformDepthCameraIntrinsicsData;

        public UInt64 TimestampMs
        {
            set => _frameCStruct.TimestampMs = value;
        }

        public void SetPoseData(float[] pose)
        {
            _poseData.CopyFrom(pose);
        }

        public void SetGpsData(GpsLocation location)
        {
            _gpsData[0] = location;
        }

        public void SetCompassData(CompassData compass)
        {
            _compassData[0] = compass;
        }

        public Vector2Int Rgba256x144ImageResolution => _rgba256x144ImageResolution;

        public NativeArray<float> Rgba256x144CameraIntrinsicsData => _rgba256x144CameraIntrinsicsData;

        public UInt32 Rgba256x144CameraIntrinsicsLength
        {
            set => _frameCStruct.Rgba256x144CameraIntrinsicsLength = value;
        }

        public IntPtr CpuRgba256x144ImageDataPtr
        {
            get => _frameCStruct.CpuRgba256x144ImageData;
            set => _frameCStruct.CpuRgba256x144ImageData = value;
        }

        public UInt32 CpuRgba256x144ImageDataLength
        {
            set => _frameCStruct.CpuRgba256x144ImageDataLength = value;
        }

        public Vector2Int Rgb256x256ImageResolution => _rgb256x256ImageResolution;

        public NativeArray<float> Rgb256x256CameraIntrinsicsData => _rgb256x256CameraIntrinsicsData;

        public UInt32 Rgb256x256CameraIntrinsicsLength
        {
            set => _frameCStruct.Rgb256x256CameraIntrinsicsLength = value;
        }

        public IntPtr CpuRgb256x256ImageDataPtr
        {
            get => _frameCStruct.CpuRgb256x256ImageData;
            set => _frameCStruct.CpuRgb256x256ImageData = value;
        }

        public UInt32 CpuRgb256x256ImageDataLength
        {
            set => _frameCStruct.CpuRgb256x256ImageDataLength = value;
        }

        public Vector2Int Jpeg720x540ImageResolution => _jpeg720X540ImageResolution;

        public NativeArray<float> Jpeg720x540CameraIntrinsicsData => _jpeg720X540CameraIntrinsicsData;

        public UInt32 Jpeg720x540CameraIntrinsicsLength
        {
            set => _frameCStruct.Jpeg720x540CameraIntrinsicsLength = value;
        }

        public NativeArray<byte> CpuJpeg720x540ImageData => _cpuJpeg720X540ImageData;

        public IntPtr CpuJpeg720x540ImageDataPtr => _frameCStruct.CpuJpeg720x540ImageData;

        public UInt32 CpuJpeg720x540ImageDataLength
        {
            set => _frameCStruct.CpuJpeg720x540ImageDataLength = value;
        }

        public Vector2Int JpegFullResImageResolution => _jpegFullResImageResolution;

        public NativeArray<float> JpegFullResCameraIntrinsicsData => _jpegFullResCameraIntrinsicsData;

        public UInt32 JpegFullResCameraIntrinsicsLength
        {
            set => _frameCStruct.JpegFullResCameraIntrinsicsLength = value;
        }

        public NativeArray<byte> CpuJpegFullResImageData => _cpuJpegFullResImageData;

        public IntPtr CpuJpegFullResImageDataPtr => _frameCStruct.CpuJpegFullResImageData;

        public UInt32 CpuJpegFullResImageWidth
        {
            set => _frameCStruct.CpuJpegFullResImageWidth = value;
        }

        public UInt32 CpuJpegFullResImageHeight
        {
            set => _frameCStruct.CpuJpegFullResImageHeight = value;
        }

        public UInt32 CpuJpegFullResImageDataLength
        {
            set => _frameCStruct.CpuJpegFullResImageDataLength = value;
        }

        public IntPtr PlatformDepthDataPtr
        {
            set => _frameCStruct.PlatformDepthData = value;
        }

        public IntPtr PlatformDepthConfidencesDataPtr
        {
            set => _frameCStruct.PlatformDepthConfidencesData = value;
        }

        public Vector2Int PlatformDepthResolution
        {
            get => _platformDepthResolution;
            set
            {
                _platformDepthResolution = value;
                _frameCStruct.PlatformDepthDataWidth = (uint)value.x;
                _frameCStruct.PlatformDepthDataHeight = (uint)value.y;
            }
        }

        public UInt32 PlatformDepthDataLength
        {
            get => _frameCStruct.PlatformDepthDataLength;
            set => _frameCStruct.PlatformDepthDataLength = value;
        }

        public NativeArray<float> PlatformDepthCameraIntrinsicsData => _platformDepthCameraIntrinsicsData;

        public UInt32 PlatformDepthCameraIntrinsicsLength
        {
            set => _frameCStruct.PlatformDepthCameraIntrinsicsLength = value;
        }

        public UInt32 GpsLocationLength
        {
            set => _frameCStruct.GpsLocationLength = value;
        }

        public UInt32 CompassDataLength
        {
            set => _frameCStruct.CompassDataLength = value;
        }

        public UInt32 CameraPoseLength
        {
            set => _frameCStruct.CameraPoseLength = value;
        }

        public UInt32 ScreenOrientation
        {
            set => _frameCStruct.ScreenOrientation = value;
        }

        public UInt32 TrackingState
        {
            set => _frameCStruct.TrackingState = value;
        }

        public Vector2Int CameraImageResolution
        {
            set
            {
                _frameCStruct.CameraImageWidth = (UInt32)value.x;
                _frameCStruct.CameraImageHeight = (UInt32)value.y;
            }
        }

        public UInt32 FrameId
        {
            set => _frameCStruct.FrameId = value;
        }

        internal void Dispose()
        {
            _poseData.Dispose();

            if (_rgba256x144ImageData.IsCreated)
                _rgba256x144ImageData.Dispose();

            _rgba256x144CameraIntrinsicsData.Dispose();

            if (_rgb256x256ImageData.IsCreated)
                _rgb256x256ImageData.Dispose();

            _rgb256x256CameraIntrinsicsData.Dispose();

            _cpuJpeg720X540ImageData.Dispose();
            _jpeg720X540CameraIntrinsicsData.Dispose();

            _gpsData.Dispose();
            _compassData.Dispose();

            if (_cpuJpegFullResImageData.IsCreated)
                _cpuJpegFullResImageData.Dispose();

            _jpegFullResCameraIntrinsicsData.Dispose();

            if (_platformDepthData.IsCreated)
                _platformDepthData.Dispose();

            if (_platformDepthConfidences.IsCreated)
                _platformDepthConfidences.Dispose();

            _platformDepthCameraIntrinsicsData.Dispose();
        }

        internal FrameData(PlatformAdapterManager.ImageProcessingMode imageProcessingMode)
        {
            _poseData = new NativeArray<float>((int)DataFormatConstants.FlatMatrix4x4Length, Allocator.Persistent);
            _frameCStruct.CameraPoseLength = 0; // Length is assigned when data is ready

            _rgba256x144CameraIntrinsicsData =
                new NativeArray<float>((int)DataFormatConstants.FlatMatrix3x3Length, Allocator.Persistent);

            _rgb256x256CameraIntrinsicsData =
                new NativeArray<float>((int)DataFormatConstants.FlatMatrix3x3Length, Allocator.Persistent);

            if (imageProcessingMode == PlatformAdapterManager.ImageProcessingMode.CPU)
            {
                _rgba256x144ImageData =
                    new NativeArray<byte>((int)DataFormatConstants.Rgba_256_144_DataLength, Allocator.Persistent);

                _rgb256x256ImageData =
                    new NativeArray<byte>((int)DataFormatConstants.Rgb_256_256_DataLength, Allocator.Persistent);
            }

            _cpuJpeg720X540ImageData =
                new NativeArray<byte>((int)DataFormatConstants.Jpeg_720_540_MaxJpegDataLength,
                    Allocator.Persistent);
            _frameCStruct.CpuJpeg720x540ImageDataLength = 0; // Length is assigned when data is ready

            _jpeg720X540CameraIntrinsicsData =
                new NativeArray<float>((int)DataFormatConstants.FlatMatrix3x3Length, Allocator.Persistent);
            _frameCStruct.Jpeg720x540CameraIntrinsicsLength = 0;

            // Full res JPEG image data will be reinitialized when getting the first image.
            _frameCStruct.CpuJpegFullResImageDataLength = 0; // Length is assigned when data is ready
            _frameCStruct.CpuJpegFullResImageWidth = 0;
            _frameCStruct.CpuJpegFullResImageHeight = 0;
            _jpegFullResCameraIntrinsicsData =
                new NativeArray<float>((int)DataFormatConstants.FlatMatrix3x3Length, Allocator.Persistent);
            _frameCStruct.JpegFullResCameraIntrinsicsLength = 0;

            _gpsData = new NativeArray<GpsLocation>(1, Allocator.Persistent);
            _frameCStruct.GpsLocationLength = 0; // Length is assigned when data is ready

            _compassData = new NativeArray<CompassData>(1, Allocator.Persistent);
            _frameCStruct.CompassDataLength = 0; // Length is assigned when data is ready

            // Depth buffer data will be reinitialized when getting the first platform depth image.
            _frameCStruct.PlatformDepthDataLength = 0;

            _platformDepthCameraIntrinsicsData =
                new NativeArray<float>((int)DataFormatConstants.FlatMatrix3x3Length, Allocator.Persistent);
            _frameCStruct.PlatformDepthCameraIntrinsicsLength = 0;

            // Set up pointers for native array data, since these values stay constant
            unsafe
            {
                _frameCStruct.CameraPose = (IntPtr)_poseData.GetUnsafeReadOnlyPtr();

                _frameCStruct.CpuRgba256x144CameraIntrinsics =
                    (IntPtr)_rgba256x144CameraIntrinsicsData.GetUnsafeReadOnlyPtr();

                _frameCStruct.CpuRgb256x256CameraIntrinsics =
                    (IntPtr)_rgb256x256CameraIntrinsicsData.GetUnsafeReadOnlyPtr();

                if (imageProcessingMode == PlatformAdapterManager.ImageProcessingMode.CPU)
                {
                    _frameCStruct.CpuRgba256x144ImageData = (IntPtr)_rgba256x144ImageData.GetUnsafeReadOnlyPtr();
                    _frameCStruct.CpuRgb256x256ImageData = (IntPtr)_rgb256x256ImageData.GetUnsafeReadOnlyPtr();
                }

                _frameCStruct.CpuJpeg720x540ImageData = (IntPtr)_cpuJpeg720X540ImageData.GetUnsafeReadOnlyPtr();
                _frameCStruct.CpuJpeg720x540CameraIntrinsics =
                    (IntPtr)_jpeg720X540CameraIntrinsicsData.GetUnsafeReadOnlyPtr();

                _frameCStruct.GpsLocationData = (IntPtr)_gpsData.GetUnsafeReadOnlyPtr();
                _frameCStruct.CompassData = (IntPtr)_compassData.GetUnsafeReadOnlyPtr();

                // We can't set the full resolution Jpeg data until getting the data.
                // But we can do so for the camera intrinsics.
                _frameCStruct.CpuJpegFullResCameraIntrinsics =
                    (IntPtr)_jpegFullResCameraIntrinsicsData.GetUnsafeReadOnlyPtr();

                _frameCStruct.PlatformDepthCameraIntrinsics =
                    (IntPtr)_platformDepthCameraIntrinsicsData.GetUnsafeReadOnlyPtr();
            }
        }

        public void ReinitializeJpegFullResolutionData(Vector2Int jpegFullResImageResolution)
        {
            if (_jpegFullResImageResolution == jpegFullResImageResolution) {
                // Do nothing if the resolution matches.
                return;
            }

            _jpegFullResImageResolution = jpegFullResImageResolution;
            _jpegFullResImageLength =
                (UInt32)(_jpegFullResImageResolution.x * _jpegFullResImageResolution.y * 12);
            if (_cpuJpegFullResImageData.IsCreated)
            {
                _cpuJpegFullResImageData.Dispose();
            }
            _cpuJpegFullResImageData =
                new NativeArray<byte>((int)_jpegFullResImageLength, Allocator.Persistent);

            _frameCStruct.CpuJpegFullResImageWidth = (UInt32)_jpegFullResImageResolution.x;
            _frameCStruct.CpuJpegFullResImageHeight = (UInt32)_jpegFullResImageResolution.y;
            _frameCStruct.CpuJpegFullResImageDataLength = _jpegFullResImageLength;
            // Set up pointers for native array data, the values stay constant after
            // being set.
            unsafe
            {
                _frameCStruct.CpuJpegFullResImageData = (IntPtr)_cpuJpegFullResImageData.GetUnsafeReadOnlyPtr();
            }
        }
    }
}
