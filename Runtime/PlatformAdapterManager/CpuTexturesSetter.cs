// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.Lightship.AR.Utilities.CTrace;
using PlatformAdapterManager;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    internal class CpuTexturesSetter : ITexturesSetter
    {
        private readonly _PlatformDataAcquirer _platformDataAcquirer;
        private readonly _FrameData _currentFrameData;

        private readonly _ICTrace _ctrace;
        private readonly UInt64 _ctraceId;

        private double _currTimestampMs;

        // CPU conversion path
        private XRCpuImage _cpuImage = default(XRCpuImage);
        private XRCpuImage _depthCpuImage = default(XRCpuImage);
        private XRCpuImage _depthConfidenceCpuImage = default(XRCpuImage);

        private NativeArray<byte> _resizedJpegDataHolder;
        private IntPtr _resizedJpegDataHolderPtr;

        private NativeArray<byte> _fullResJpegDataHolder;
        private IntPtr _fullResJpegDataHolderPtr;

        private const int SecondToMillisecondFactor = 1000;
        private const int MillisecondToNanosecondFactor = 1000000;

        public CpuTexturesSetter(_PlatformDataAcquirer dataAcquirer, _FrameData frameData, _ICTrace ctrace, UInt64 ctraceId)
        {
            _ctrace = ctrace;
            _ctraceId = ctraceId;
            _platformDataAcquirer = dataAcquirer;
            _currentFrameData = frameData;

            _resizedJpegDataHolder =
                new NativeArray<byte>
                (
                    _DataFormatConstants.JPEG_720_540_IMG_WIDTH * _DataFormatConstants.JPEG_720_540_IMG_HEIGHT * 4,
                    Allocator.Persistent
                );

            unsafe
            {
                _resizedJpegDataHolderPtr = (IntPtr)_resizedJpegDataHolder.GetUnsafePtr();
            }
        }

        public void Dispose()
        {
            if (_resizedJpegDataHolder.IsCreated)
                _resizedJpegDataHolder.Dispose();

            if (_fullResJpegDataHolder.IsCreated)
                _fullResJpegDataHolder.Dispose();
        }

        public void InvalidateCachedTextures()
        {
            _cpuImage.Dispose();
            _depthCpuImage.Dispose();
            _depthConfidenceCpuImage.Dispose();

            _currTimestampMs = 0;
        }


        public double GetCurrentTimestampMs()
        {
            if (_currTimestampMs == 0)
            {
                if (_platformDataAcquirer.TryGetCameraFrame(out XRCameraFrame frame))
                    return frame.timestampNs / MillisecondToNanosecondFactor;

                return 0;
            }

            return _currTimestampMs;
        }

        public void SetRgba256x144Image()
        {
            const string cTraceName = "PAM::AwarenessCPUConversion";
            _ctrace.TraceEventAsyncBegin0("SAL", cTraceName, _ctraceId);
            _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "TryGetCpuImage");
            if (_cpuImage.valid || _platformDataAcquirer.TryGetCpuImage(out _cpuImage))
            {
                _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "ConvertOnCpuAndWriteToMemory");

                _cpuImage.ConvertOnCpuAndWriteToMemory
                (
                    _currentFrameData.Rgba256x144ImageResolution,
                    _currentFrameData.CpuRgba256x144ImageDataPtr
                );

                _currTimestampMs = _cpuImage.timestamp * SecondToMillisecondFactor;
                _currentFrameData.CpuRgba256x144ImageDataLength = _DataFormatConstants.RGBA_256_144_DATA_LENGTH;
            }
            else
            {
                _currentFrameData.TimestampMs = 0;
                _currentFrameData.CpuRgba256x144ImageDataLength = 0;
            }


            _ctrace.TraceEventAsyncEnd0("SAL", cTraceName, _ctraceId);
        }

        public void SetJpeg720x540Image()
        {
            const string cTraceName = "PAM::VpsCPUConversion";
            _ctrace.TraceEventAsyncBegin0("SAL",cTraceName, _ctraceId);
            _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "TryGetCpuImage");
            if (_cpuImage.valid || _platformDataAcquirer.TryGetCpuImage(out _cpuImage))
            {
                if (_cpuImage.width < _DataFormatConstants.JPEG_720_540_IMG_WIDTH ||
                    _cpuImage.height < _DataFormatConstants.JPEG_720_540_IMG_HEIGHT)
                {
                    Debug.LogWarning
                    (
                        $"XR camera image resolution ({_cpuImage.width}x{_cpuImage.height}) is too small to support " +
                        "all enabled Lightship features. Resolution must be at least " +
                        $"{_DataFormatConstants.JPEG_720_540_IMG_WIDTH}x{_DataFormatConstants.JPEG_720_540_IMG_HEIGHT}."
                    );

                    _currentFrameData.CpuJpeg720x540ImageDataLength = 0;
                }
                else
                {
                    _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "ConvertOnCpuAndWriteToMemory");

                    // Unity's EncodeToJPG method expects as input texture data formatted in Unity convention (first pixel
                    // bottom left), whereas the raw data of AR textures are in first pixel top left convention.
                    // Thus we invert the pixels in this step, so they are output correctly oriented in the encoding step.
                    _cpuImage.ConvertOnCpuAndWriteToMemory
                    (
                        _currentFrameData.Jpeg720x540ImageResolution,
                        _resizedJpegDataHolderPtr,
                        transformation: XRCpuImage.Transformation.MirrorX
                    );

                    _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "EncodeNativeArrayToJPG");
                    var jpegHolder =
                        ImageConversion.EncodeNativeArrayToJPG
                        (
                            _resizedJpegDataHolder,
                            GraphicsFormat.R8G8B8A8_UNorm,
                            _DataFormatConstants.JPEG_720_540_IMG_WIDTH,
                            _DataFormatConstants.JPEG_720_540_IMG_HEIGHT,
                            0,
                            _DataFormatConstants.JPEG_QUALITY
                        );

                    // Copy the JPEG byte array to the shared buffer in FrameCStruct
                    _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "Copy");
                    jpegHolder.CopyTo(_currentFrameData.CpuJpeg720x540ImageData);
                    jpegHolder.Dispose();

                    _currentFrameData.CpuJpeg720x540ImageDataLength = (uint)jpegHolder.Length;
                    _currTimestampMs = _cpuImage.timestamp * SecondToMillisecondFactor;
                }
            }
            else
            {
                _currentFrameData.CpuJpeg720x540ImageDataLength = 0;
            }

            _ctrace.TraceEventAsyncEnd0("SAL", cTraceName, _ctraceId);
        }

        // Return true when successfully get the camera intrinsics and reinitialize
        // correspondingly. Otherwise return false.
        private bool ReinitializeJpegFullResDataIfNeeded()
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsics(out XRCameraIntrinsics jpegCameraIntrinsics))
            {
                if (jpegCameraIntrinsics.resolution != _currentFrameData.JpegFullResImageResolution) {

                    // Need to call ReinitializeJpegFullResolutionData() to re-allocate memory
                    // for the full res JPEG image in |_currentFrameData|.
                    _currentFrameData.ReinitializeJpegFullResolutionData(jpegCameraIntrinsics.resolution);

                    // Need to reallocate the memory for |_fullResJpegDataHolder|.
                    // Dispose the memory before rellocate new ones, to avoid the leak.
                    if (_fullResJpegDataHolder.IsCreated) {
                        _fullResJpegDataHolder.Dispose();
                    }
                    _fullResJpegDataHolder = new NativeArray<byte>
                    (
                        jpegCameraIntrinsics.resolution.x * jpegCameraIntrinsics.resolution.y * 4,
                        Allocator.Persistent
                    );
                    unsafe
                    {
                        _fullResJpegDataHolderPtr = (IntPtr)_fullResJpegDataHolder.GetUnsafePtr();
                    }
                }

                _ImageConverter.ConvertCameraIntrinsics
                (
                    jpegCameraIntrinsics,
                    _currentFrameData.JpegFullResImageResolution,
                    _currentFrameData.JpegFullResCameraIntrinsicsData
                );
                _currentFrameData.JpegFullResCameraIntrinsicsLength = _DataFormatConstants.FLAT_MATRIX3x3_LENGTH;

                return true;
            }

            // Fail to get the camera image's intrinsics, simply return false and
            // not get the image.
            _currentFrameData.JpegFullResCameraIntrinsicsLength = 0;
            _currentFrameData.CpuJpegFullResImageDataLength = 0;
            _currentFrameData.CpuJpegFullResImageWidth = 0;
            _currentFrameData.CpuJpegFullResImageHeight = 0;
            return false;
        }

        // TODO(sxian): This task needs to be async.
        public void SetJpegFullResImage()
        {
            if (!ReinitializeJpegFullResDataIfNeeded())
                return;

            const string cTraceName = "PAM::JpegFullResImageWithCpu";
            _ctrace.TraceEventAsyncBegin0("SAL", cTraceName, _ctraceId);
            _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "TryGetCpuImage");
            if (_cpuImage.valid || _platformDataAcquirer.TryGetCpuImage(out _cpuImage))
            {
                if (_cpuImage.width != _currentFrameData.JpegFullResImageResolution.x ||
                    _cpuImage.height != _currentFrameData.JpegFullResImageResolution.y)
                {
                    Debug.LogError
                    (
                        $"XR camera image resolution ({_cpuImage.width}x{_cpuImage.height}) " +
                        " do not match _currentFrameData's resolution " +
                        $"{_currentFrameData.JpegFullResImageResolution.x}x{_currentFrameData.JpegFullResImageResolution.y}."
                    );

                    _currentFrameData.CpuJpegFullResImageDataLength = 0;
                    _currentFrameData.CpuJpegFullResImageWidth = 0;
                    _currentFrameData.CpuJpegFullResImageHeight = 0;
                }
                else
                {
                    // Buffer to hold the downscaled image (pre-JPEG formatting)
                    _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "ConvertOnCpuAndWriteToMemory");
                    // TODO(sxian): Any other way to do the mirroring more efficiently?
                    _cpuImage.ConvertOnCpuAndWriteToMemory
                    (
                        _currentFrameData.JpegFullResImageResolution,
                        _fullResJpegDataHolderPtr,
                        TextureFormat.RGBA32,
                        // Need to MirrorX because C++ expects image with (0,0) in top left corner,
                        // while Unity has (0,0) in the bottom left corner.
                        XRCpuImage.Transformation.MirrorX
                    );

                    _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "EncodeNativeArrayToJPG");
                    var jpegHolder =
                        ImageConversion.EncodeNativeArrayToJPG
                        (
                            _fullResJpegDataHolder,
                            GraphicsFormat.R8G8B8A8_UNorm,
                            (UInt32)_currentFrameData.JpegFullResImageResolution.x,
                            (UInt32)_currentFrameData.JpegFullResImageResolution.y,
                            0,
                            _DataFormatConstants.JPEG_QUALITY
                        );

                    // Copy the JPEG byte array to the shared buffer in FrameCStruct
                    _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "Copy");
                    jpegHolder.CopyTo(_currentFrameData.CpuJpegFullResImageData);
                    jpegHolder.Dispose();

                    _currentFrameData.CpuJpegFullResImageDataLength = (uint)jpegHolder.Length;
                    _currTimestampMs = _cpuImage.timestamp * SecondToMillisecondFactor;
                }
            }
            else
            {
                _currentFrameData.CpuJpegFullResImageDataLength = 0;
                _currentFrameData.CpuJpegFullResImageWidth = 0;
                _currentFrameData.CpuJpegFullResImageHeight = 0;
                // Set the intrinsics length to 0 as we fail to get the image.
                _currentFrameData.JpegFullResCameraIntrinsicsLength = 0;
            }

            _ctrace.TraceEventAsyncEnd0("SAL", cTraceName, _ctraceId);
        }

        public void SetPlatformDepthBuffer()
        {
            const string cTraceName = "PAM::DepthCPUConversion";
            _ctrace.TraceEventAsyncBegin0("SAL", cTraceName, _ctraceId);
            _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "TryGetDepthImage");
            if (_platformDataAcquirer.TryGetCpuDepthImage(out _depthCpuImage, out _depthConfidenceCpuImage))
            {
                _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "ConvertOnCpuAndWriteToMemory");

                unsafe
                {
                    _currentFrameData.PlatformDepthDataPtr =
                        (IntPtr)_depthCpuImage.GetPlane(0).data.GetUnsafeReadOnlyPtr();

                    _currentFrameData.PlatformDepthConfidencesDataPtr =
                        (IntPtr)_depthConfidenceCpuImage.GetPlane(0).data.GetUnsafeReadOnlyPtr();
                }

                _currTimestampMs = _depthCpuImage.timestamp * SecondToMillisecondFactor;
                _currentFrameData.PlatformDepthResolution = _depthCpuImage.dimensions;
                _currentFrameData.PlatformDepthDataLength =
                    (UInt32)(_depthCpuImage.dimensions.x * _depthCpuImage.dimensions.y);
            }
            else
            {
                _currentFrameData.PlatformDepthResolution = Vector2Int.zero;
                _currentFrameData.PlatformDepthDataLength = 0;
            }

            _ctrace.TraceEventAsyncEnd0("SAL", cTraceName, _ctraceId);
        }
    }
}
