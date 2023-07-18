using System;
using Niantic.Lightship.AR.PlatformAdapterManager;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace PlatformAdapterManager
{
    internal class _FrameData
    {
        internal _FrameCStruct frameCStruct;

        private NativeArray<float> _poseData;

        // Location data
        private NativeArray<GpsLocation> _gpsData;
        private NativeArray<CompassData> _compassData;

        // RGBA 256x144 image data
        private NativeArray<byte> _rgba256x144ImageData;
        private NativeArray<float> _rgba256x144CameraIntrinsicsData;
        private readonly Vector2Int _rgba256x144ImageResolution =
            new(_DataFormatConstants.RGBA_256_144_IMG_WIDTH, _DataFormatConstants.RGBA_256_144_IMG_HEIGHT);

        // JPEG 720x540 image data
        private NativeArray<byte> _cpuJpeg720X540ImageData;
        private NativeArray<float> _jpeg720X540CameraIntrinsicsData;
        private readonly Vector2Int _jpeg720X540ImageResolution =
            new(_DataFormatConstants.JPEG_720_540_IMG_WIDTH, _DataFormatConstants.JPEG_720_540_IMG_HEIGHT);

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
            set => frameCStruct.TimestampMs = value;
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
            set => frameCStruct.Rgba256x144CameraIntrinsicsLength = value;
        }

        public IntPtr CpuRgba256x144ImageDataPtr
        {
            get => frameCStruct.CpuRgba256x144ImageData;
            set => frameCStruct.CpuRgba256x144ImageData = value;
        }

        public UInt32 CpuRgba256x144ImageDataLength
        {
            set => frameCStruct.CpuRgba256x144ImageDataLength = value;
        }

        public Vector2Int Jpeg720x540ImageResolution => _jpeg720X540ImageResolution;

        public NativeArray<float> Jpeg720x540CameraIntrinsicsData => _jpeg720X540CameraIntrinsicsData;

        public UInt32 Jpeg720x540CameraIntrinsicsLength
        {
            set => frameCStruct.Jpeg720x540CameraIntrinsicsLength = value;
        }

        public NativeArray<byte> CpuJpeg720x540ImageData => _cpuJpeg720X540ImageData;

        public IntPtr CpuJpeg720x540ImageDataPtr => frameCStruct.CpuJpeg720x540ImageData;

        public UInt32 CpuJpeg720x540ImageDataLength
        {
            set => frameCStruct.CpuJpeg720x540ImageDataLength = value;
        }

        public Vector2Int JpegFullResImageResolution => _jpegFullResImageResolution;

        public NativeArray<float> JpegFullResCameraIntrinsicsData => _jpegFullResCameraIntrinsicsData;

        public UInt32 JpegFullResCameraIntrinsicsLength
        {
            set => frameCStruct.JpegFullResCameraIntrinsicsLength = value;
        }

        public NativeArray<byte> CpuJpegFullResImageData => _cpuJpegFullResImageData;

        public IntPtr CpuJpegFullResImageDataPtr => frameCStruct.CpuJpegFullResImageData;

        public UInt32 CpuJpegFullResImageWidth
        {
            set => frameCStruct.CpuJpegFullResImageWidth = value;
        }

        public UInt32 CpuJpegFullResImageHeight
        {
            set => frameCStruct.CpuJpegFullResImageHeight = value;
        }

        public UInt32 CpuJpegFullResImageDataLength
        {
            set => frameCStruct.CpuJpegFullResImageDataLength = value;
        }

        public IntPtr PlatformDepthDataPtr
        {
            set => frameCStruct.PlatformDepthData = value;
        }

        public IntPtr PlatformDepthConfidencesDataPtr
        {
            set => frameCStruct.PlatformDepthConfidencesData = value;
        }

        public Vector2Int PlatformDepthResolution
        {
            get => _platformDepthResolution;
            set
            {
                _platformDepthResolution = value;
                frameCStruct.PlatformDepthDataWidth = (uint)value.x;
                frameCStruct.PlatformDepthDataHeight = (uint)value.y;
            }
        }

        public UInt32 PlatformDepthDataLength
        {
            get => frameCStruct.PlatformDepthDataLength;
            set => frameCStruct.PlatformDepthDataLength = value;
        }

        public NativeArray<float> PlatformDepthCameraIntrinsicsData => _platformDepthCameraIntrinsicsData;

        public UInt32 PlatformDepthCameraIntrinsicsLength
        {
            set => frameCStruct.PlatformDepthCameraIntrinsicsLength = value;
        }

        public UInt32 GpsLocationLength
        {
            set => frameCStruct.GpsLocationLength = value;
        }

        public UInt32 CompassDataLength
        {
            set => frameCStruct.CompassDataLength = value;
        }

        public UInt32 CameraPoseLength
        {
            set => frameCStruct.CameraPoseLength = value;
        }

        public UInt32 DeviceOrientation
        {
            set => frameCStruct.DeviceOrientation = value;
        }

        public UInt32 TrackingState
        {
            set => frameCStruct.TrackingState = value;
        }

        public UInt64 FrameId
        {
            set => frameCStruct.FrameId = value;
        }

        internal void Dispose()
        {
            _poseData.Dispose();

            if (_rgba256x144ImageData.IsCreated)
                _rgba256x144ImageData.Dispose();

            _rgba256x144CameraIntrinsicsData.Dispose();


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

        internal _FrameData(_PlatformAdapterManager.ImageProcessingMode imageProcessingMode)
        {
            _poseData = new NativeArray<float>((int)_DataFormatConstants.FLAT_MATRIX4x4_LENGTH, Allocator.Persistent);
            frameCStruct.CameraPoseLength = 0; // Length is assigned when data is ready

            _rgba256x144CameraIntrinsicsData =
                new NativeArray<float>((int)_DataFormatConstants.FLAT_MATRIX3x3_LENGTH, Allocator.Persistent);

            if (imageProcessingMode == _PlatformAdapterManager.ImageProcessingMode.CPU)
            {
                _rgba256x144ImageData =
                    new NativeArray<byte>((int)_DataFormatConstants.RGBA_256_144_DATA_LENGTH, Allocator.Persistent);
            }

            _cpuJpeg720X540ImageData =
                new NativeArray<byte>((int)_DataFormatConstants.JPEG_720_540_MAX_JPEG_DATA_LENGTH,
                    Allocator.Persistent);
            frameCStruct.CpuJpeg720x540ImageDataLength = 0; // Length is assigned when data is ready

            _jpeg720X540CameraIntrinsicsData =
                new NativeArray<float>((int)_DataFormatConstants.FLAT_MATRIX3x3_LENGTH, Allocator.Persistent);
            frameCStruct.Jpeg720x540CameraIntrinsicsLength = 0;

            // Full res JPEG image data will be reinitialized when getting the first image.
            frameCStruct.CpuJpegFullResImageDataLength = 0; // Length is assigned when data is ready
            frameCStruct.CpuJpegFullResImageWidth = 0;
            frameCStruct.CpuJpegFullResImageHeight = 0;
            _jpegFullResCameraIntrinsicsData =
                new NativeArray<float>((int)_DataFormatConstants.FLAT_MATRIX3x3_LENGTH, Allocator.Persistent);
            frameCStruct.JpegFullResCameraIntrinsicsLength = 0;

            _gpsData = new NativeArray<GpsLocation>(1, Allocator.Persistent);
            frameCStruct.GpsLocationLength = 0; // Length is assigned when data is ready

            _compassData = new NativeArray<CompassData>(1, Allocator.Persistent);
            frameCStruct.CompassDataLength = 0; // Length is assigned when data is ready

            // Depth buffer data will be reinitialized when getting the first platform depth image.
            frameCStruct.PlatformDepthDataLength = 0;

            _platformDepthCameraIntrinsicsData =
                new NativeArray<float>((int)_DataFormatConstants.FLAT_MATRIX3x3_LENGTH, Allocator.Persistent);
            frameCStruct.PlatformDepthCameraIntrinsicsLength = 0;

            // Set up pointers for native array data, since these values stay constant
            unsafe
            {
                frameCStruct.CameraPose = (IntPtr)_poseData.GetUnsafeReadOnlyPtr();

                frameCStruct.CpuRgba256x144CameraIntrinsics =
                    (IntPtr)_rgba256x144CameraIntrinsicsData.GetUnsafeReadOnlyPtr();

                if (imageProcessingMode == _PlatformAdapterManager.ImageProcessingMode.CPU)
                    frameCStruct.CpuRgba256x144ImageData = (IntPtr)_rgba256x144ImageData.GetUnsafeReadOnlyPtr();

                frameCStruct.CpuJpeg720x540ImageData = (IntPtr)_cpuJpeg720X540ImageData.GetUnsafeReadOnlyPtr();
                frameCStruct.CpuJpeg720x540CameraIntrinsics =
                    (IntPtr)_jpeg720X540CameraIntrinsicsData.GetUnsafeReadOnlyPtr();

                frameCStruct.GpsLocationData = (IntPtr)_gpsData.GetUnsafeReadOnlyPtr();
                frameCStruct.CompassData = (IntPtr)_compassData.GetUnsafeReadOnlyPtr();

                // We can't set the full resolution Jpeg data until getting the data.
                // But we can do so for the camera intrinsics.
                frameCStruct.CpuJpegFullResCameraIntrinsics =
                    (IntPtr)_jpegFullResCameraIntrinsicsData.GetUnsafeReadOnlyPtr();

                frameCStruct.PlatformDepthCameraIntrinsics =
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

            frameCStruct.CpuJpegFullResImageWidth = (UInt32)_jpegFullResImageResolution.x;
            frameCStruct.CpuJpegFullResImageHeight = (UInt32)_jpegFullResImageResolution.y;
            frameCStruct.CpuJpegFullResImageDataLength = _jpegFullResImageLength;
            // Set up pointers for native array data, the values stay constant after
            // being set.
            unsafe
            {
                frameCStruct.CpuJpegFullResImageData = (IntPtr)_cpuJpegFullResImageData.GetUnsafeReadOnlyPtr();
            }
        }
    }
}
