// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.Lightship.AR.Utilities.Profiling;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PAM
{
    internal class CpuTexturesSetter : AbstractTexturesSetter
    {
        private double _currTimestampMs;

        // CPU conversion path
        private XRCpuImage _cpuImage;
        private XRCpuImage _depthCpuImage;
        private XRCpuImage _depthConfidenceCpuImage;

        private NativeArray<byte> _resizedJpegDataHolder;
        private IntPtr _resizedJpegDataHolderPtr;

        private NativeArray<byte> _fullResJpegDataHolder;
        private IntPtr _fullResJpegDataHolderPtr;

        private const int SecondToMillisecondFactor = 1000;

        private const string TraceCategory = "CpuTexturesSetter";

        public CpuTexturesSetter(PlatformDataAcquirer dataAcquirer, FrameData frameData) : base(dataAcquirer, frameData)
        {
            _resizedJpegDataHolder =
                new NativeArray<byte>
                (
                    DataFormatConstants.Jpeg_720_540_ImgWidth * DataFormatConstants.Jpeg_720_540_ImgHeight * 4,
                    Allocator.Persistent
                );

            unsafe
            {
                _resizedJpegDataHolderPtr = (IntPtr)_resizedJpegDataHolder.GetUnsafePtr();
            }
        }

        public override void Dispose()
        {
            if (_resizedJpegDataHolder.IsCreated)
                _resizedJpegDataHolder.Dispose();

            if (_fullResJpegDataHolder.IsCreated)
                _fullResJpegDataHolder.Dispose();
        }

        public override void InvalidateCachedTextures()
        {
            _cpuImage.Dispose();
            _depthCpuImage.Dispose();
            _depthConfidenceCpuImage.Dispose();

            _currTimestampMs = 0;
        }


        public override double GetCurrentTimestampMs()
        {
            if (_currTimestampMs == 0)
            {
                base.GetCurrentTimestampMs();
            }

            return _currTimestampMs;
        }

        public override void SetRgba256x144Image()
        {
            const string traceEvent = "SetRgba256x144Image (CPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventStep(TraceCategory, traceEvent, "TryGetCpuImage");
            if (_cpuImage.valid || PlatformDataAcquirer.TryGetCpuImage(out _cpuImage))
            {
                ProfilerUtility.EventStep(TraceCategory, traceEvent, "ConvertOnCpuAndWriteToMemory");

                _cpuImage.ConvertOnCpuAndWriteToMemory
                (
                    CurrentFrameData.Rgba256x144ImageResolution,
                    CurrentFrameData.CpuRgba256x144ImageDataPtr
                );

                _currTimestampMs = _cpuImage.timestamp * SecondToMillisecondFactor;
                CurrentFrameData.CpuRgba256x144ImageDataLength = DataFormatConstants.Rgba_256_144_DataLength;
            }
            else
            {
                CurrentFrameData.TimestampMs = 0;
                CurrentFrameData.CpuRgba256x144ImageDataLength = 0;
            }


            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        public override void SetJpeg720x540Image()
        {
            const string traceEvent = "SetJpeg720x540Image (CPU)";
            ProfilerUtility.EventBegin(TraceCategory,traceEvent);

            ProfilerUtility.EventStep(TraceCategory, traceEvent, "TryGetCpuImage");
            if (_cpuImage.valid || PlatformDataAcquirer.TryGetCpuImage(out _cpuImage))
            {
                if (_cpuImage.width < DataFormatConstants.Jpeg_720_540_ImgWidth ||
                    _cpuImage.height < DataFormatConstants.Jpeg_720_540_ImgHeight)
                {
                    Debug.LogWarning
                    (
                        $"XR camera image resolution ({_cpuImage.width}x{_cpuImage.height}) is too small to support " +
                        "all enabled Lightship features. Resolution must be at least " +
                        $"{DataFormatConstants.Jpeg_720_540_ImgWidth}x{DataFormatConstants.Jpeg_720_540_ImgHeight}."
                    );

                    CurrentFrameData.CpuJpeg720x540ImageDataLength = 0;
                }
                else
                {
                    ProfilerUtility.EventStep(TraceCategory, traceEvent, "ConvertOnCpuAndWriteToMemory");

                    // Unity's EncodeToJPG method expects as input texture data formatted in Unity convention (first pixel
                    // bottom left), whereas the raw data of AR textures are in first pixel top left convention.
                    // Thus we invert the pixels in this step, so they are output correctly oriented in the encoding step.
                    _cpuImage.ConvertOnCpuAndWriteToMemory
                    (
                        CurrentFrameData.Jpeg720x540ImageResolution,
                        _resizedJpegDataHolderPtr,
                        transformation: XRCpuImage.Transformation.MirrorX
                    );

                    ProfilerUtility.EventStep(TraceCategory, traceEvent, "EncodeNativeArrayToJPG");
                    var jpegHolder =
                        ImageConversion.EncodeNativeArrayToJPG
                        (
                            _resizedJpegDataHolder,
                            GraphicsFormat.R8G8B8A8_UNorm,
                            DataFormatConstants.Jpeg_720_540_ImgWidth,
                            DataFormatConstants.Jpeg_720_540_ImgHeight,
                            0,
                            DataFormatConstants.JpegQuality
                        );

                    // Copy the JPEG byte array to the shared buffer in FrameCStruct
                    ProfilerUtility.EventStep(TraceCategory, traceEvent, "Copy JPG to frame mem");
                    jpegHolder.CopyTo(CurrentFrameData.CpuJpeg720x540ImageData);
                    jpegHolder.Dispose();

                    CurrentFrameData.CpuJpeg720x540ImageDataLength = (uint)jpegHolder.Length;
                    _currTimestampMs = _cpuImage.timestamp * SecondToMillisecondFactor;
                }
            }
            else
            {
                CurrentFrameData.CpuJpeg720x540ImageDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        // Return true when successfully get the camera intrinsics and reinitialize
        // correspondingly. Otherwise return false.
        protected override bool ReinitializeJpegFullResDataIfNeeded()
        {
            if (PlatformDataAcquirer.TryGetCameraIntrinsics(out XRCameraIntrinsics jpegCameraIntrinsics))
            {
                if (jpegCameraIntrinsics.resolution != CurrentFrameData.JpegFullResImageResolution) {

                    // Need to call ReinitializeJpegFullResolutionData() to re-allocate memory
                    // for the full res JPEG image in |_currentFrameData|.
                    CurrentFrameData.ReinitializeJpegFullResolutionData(jpegCameraIntrinsics.resolution);

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

                ImageConverter.ConvertCameraIntrinsics
                (
                    jpegCameraIntrinsics,
                    CurrentFrameData.JpegFullResImageResolution,
                    CurrentFrameData.JpegFullResCameraIntrinsicsData
                );
                CurrentFrameData.JpegFullResCameraIntrinsicsLength = DataFormatConstants.FlatMatrix3x3Length;

                return true;
            }

            // Fail to get the camera image's intrinsics, simply return false and
            // not get the image.
            CurrentFrameData.JpegFullResCameraIntrinsicsLength = 0;
            CurrentFrameData.CpuJpegFullResImageDataLength = 0;
            CurrentFrameData.CpuJpegFullResImageWidth = 0;
            CurrentFrameData.CpuJpegFullResImageHeight = 0;
            return false;
        }

        // TODO(sxian): This task needs to be async.
        public override void SetJpegFullResImage()
        {
            if (!ReinitializeJpegFullResDataIfNeeded())
                return;

            const string traceEvent = "SetJpegFullResImage (CPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventStep(TraceCategory, traceEvent, "TryGetCpuImage");
            if (_cpuImage.valid || PlatformDataAcquirer.TryGetCpuImage(out _cpuImage))
            {
                if (_cpuImage.width != CurrentFrameData.JpegFullResImageResolution.x ||
                    _cpuImage.height != CurrentFrameData.JpegFullResImageResolution.y)
                {
                    Debug.LogError
                    (
                        $"XR camera image resolution ({_cpuImage.width}x{_cpuImage.height}) " +
                        " do not match _currentFrameData's resolution " +
                        $"{CurrentFrameData.JpegFullResImageResolution.x}x{CurrentFrameData.JpegFullResImageResolution.y}."
                    );

                    CurrentFrameData.CpuJpegFullResImageDataLength = 0;
                    CurrentFrameData.CpuJpegFullResImageWidth = 0;
                    CurrentFrameData.CpuJpegFullResImageHeight = 0;
                }
                else
                {
                    // Buffer to hold the downscaled image (pre-JPEG formatting)
                    ProfilerUtility.EventStep(TraceCategory, traceEvent, "ConvertOnCpuAndWriteToMemory");
                    // TODO(sxian): Any other way to do the mirroring more efficiently?
                    _cpuImage.ConvertOnCpuAndWriteToMemory
                    (
                        CurrentFrameData.JpegFullResImageResolution,
                        _fullResJpegDataHolderPtr,
                        TextureFormat.RGBA32,
                        // Need to MirrorX because C++ expects image with (0,0) in top left corner,
                        // while Unity has (0,0) in the bottom left corner.
                        XRCpuImage.Transformation.MirrorX
                    );

                    ProfilerUtility.EventStep(TraceCategory, traceEvent, "EncodeNativeArrayToJPG");
                    var jpegHolder =
                        ImageConversion.EncodeNativeArrayToJPG
                        (
                            _fullResJpegDataHolder,
                            GraphicsFormat.R8G8B8A8_UNorm,
                            (UInt32)CurrentFrameData.JpegFullResImageResolution.x,
                            (UInt32)CurrentFrameData.JpegFullResImageResolution.y,
                            0,
                            DataFormatConstants.JpegQuality
                        );

                    // Copy the JPEG byte array to the shared buffer in FrameCStruct
                    ProfilerUtility.EventStep(TraceCategory, traceEvent, "Copy JPG to mem");
                    jpegHolder.CopyTo(CurrentFrameData.CpuJpegFullResImageData);
                    jpegHolder.Dispose();

                    CurrentFrameData.CpuJpegFullResImageDataLength = (uint)jpegHolder.Length;
                    _currTimestampMs = _cpuImage.timestamp * SecondToMillisecondFactor;
                }
            }
            else
            {
                CurrentFrameData.CpuJpegFullResImageDataLength = 0;
                CurrentFrameData.CpuJpegFullResImageWidth = 0;
                CurrentFrameData.CpuJpegFullResImageHeight = 0;
                // Set the intrinsics length to 0 as we fail to get the image.
                CurrentFrameData.JpegFullResCameraIntrinsicsLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        public override void SetPlatformDepthBuffer()
        {
            const string traceEvent = "SetPlatformDepthBuffer (CPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventStep(TraceCategory, traceEvent, "TryGetCpuDepthImage");
            if (PlatformDataAcquirer.TryGetCpuDepthImage(out _depthCpuImage, out _depthConfidenceCpuImage))
            {
                ProfilerUtility.EventStep(TraceCategory, traceEvent, "ConvertOnCpuAndWriteToMemory");

                unsafe
                {
                    CurrentFrameData.PlatformDepthDataPtr =
                        (IntPtr)_depthCpuImage.GetPlane(0).data.GetUnsafeReadOnlyPtr();

                    CurrentFrameData.PlatformDepthConfidencesDataPtr =
                        (IntPtr)_depthConfidenceCpuImage.GetPlane(0).data.GetUnsafeReadOnlyPtr();
                }

                _currTimestampMs = _depthCpuImage.timestamp * SecondToMillisecondFactor;
                CurrentFrameData.PlatformDepthResolution = _depthCpuImage.dimensions;
                CurrentFrameData.PlatformDepthDataLength =
                    (UInt32)(_depthCpuImage.dimensions.x * _depthCpuImage.dimensions.y);
            }
            else
            {
                CurrentFrameData.PlatformDepthResolution = Vector2Int.zero;
                CurrentFrameData.PlatformDepthDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }
    }
}
