// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Core;
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
        private IntPtr _nativeJpegHandle;

#if !UNITY_EDITOR && UNITY_ANDROID && NIANTIC_LIGHTSHIP_SPACES_ENABLED
        // TODO(ARDK-2304): Remove this when Snapdragon Spaces fixes their timestamp.
        // Snapdragon Spaces erroniously gives us XRCpuImage timestamps in ns, not s.
        // Since XRCpuImage timestamp is readonly we just set a 10^6 conversion factor here.
        private const int SecondToMillisecondFactor = 1 / 1000;
#else
        private const int SecondToMillisecondFactor = 1000;
#endif

        // Trace Event Strings
        private const string TraceCategory = "CpuTexturesSetter";
        private const string TryGetCpuImageEventName = "TryGetCpuImageDeprecated";
        private const string ConvertOnCpuAndWriteToMemoryEventName = "ConvertOnCpuAndWriteToMemory";
        private const string EncodeNativeArrayToJPGEventName = "EncodeNativeArrayToJPG";

        private void EnsureJpegInitialized()
        {
            if (_nativeJpegHandle.IsValidHandle())
            {
                return;
            }
            _nativeJpegHandle = Native.Lightship_ARDK_Unity_CreateJpeg();

        }

        public CpuTexturesSetter(PlatformDataAcquirer dataAcquirer, DeprecatedFrameData deprecatedFrameData) : base(dataAcquirer, deprecatedFrameData)
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

            if (_nativeJpegHandle != IntPtr.Zero)
            {
                Native.Lightship_ARDK_Unity_ReleaseJpeg(_nativeJpegHandle);
                _nativeJpegHandle = IntPtr.Zero;
            }
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
                return base.GetCurrentTimestampMs();
            }

            return _currTimestampMs;
        }

        public override void SetRgba256x144Image()
        {
            const string traceEvent = "SetRgba256x144Image (CPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventBegin(TraceCategory, TryGetCpuImageEventName);
            bool canConvert = _cpuImage.valid || PlatformDataAcquirer.TryGetCpuImageDeprecated(out _cpuImage);
            ProfilerUtility.EventEnd(TraceCategory, TryGetCpuImageEventName, "CPUImageValid", _cpuImage.valid.ToString(), "GotCPUImage", canConvert.ToString());

            if (canConvert)
            {
                ProfilerUtility.EventBegin(TraceCategory, ConvertOnCpuAndWriteToMemoryEventName);
                _cpuImage.ConvertOnCpuAndWriteToMemory
                (
                    CurrentDeprecatedFrameData.Rgba256x144ImageResolution,
                    CurrentDeprecatedFrameData.CpuRgba256x144ImageDataPtr
                );
                ProfilerUtility.EventEnd(TraceCategory, ConvertOnCpuAndWriteToMemoryEventName);

                _currTimestampMs = _cpuImage.timestamp * SecondToMillisecondFactor;
                CurrentDeprecatedFrameData.CpuRgba256x144ImageDataLength = DataFormatConstants.Rgba_256_144_DataLength;
            }
            else
            {
                CurrentDeprecatedFrameData.TimestampMs = 0;
                CurrentDeprecatedFrameData.CpuRgba256x144ImageDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        public override void SetRgb256x256Image()
        {
            const string traceEvent = "SetRgb256x256Image (CPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventBegin(TraceCategory, TryGetCpuImageEventName);
            bool canConvert = _cpuImage.valid || PlatformDataAcquirer.TryGetCpuImageDeprecated(out _cpuImage);
            ProfilerUtility.EventEnd(TraceCategory, TryGetCpuImageEventName, "CPUImageValid", _cpuImage.valid.ToString(), "GotCPUImage", canConvert.ToString());

            if (canConvert)
            {
                ProfilerUtility.EventBegin(TraceCategory, ConvertOnCpuAndWriteToMemoryEventName);
                _cpuImage.ConvertOnCpuAndWriteToMemory
                (
                    CurrentDeprecatedFrameData.Rgb256x256ImageResolution,
                    CurrentDeprecatedFrameData.CpuRgb256x256ImageDataPtr,
                    TextureFormat.RGB24
                );
                ProfilerUtility.EventEnd(TraceCategory, ConvertOnCpuAndWriteToMemoryEventName);

                _currTimestampMs = _cpuImage.timestamp * SecondToMillisecondFactor;
                CurrentDeprecatedFrameData.CpuRgb256x256ImageDataLength = DataFormatConstants.Rgb_256_256_DataLength;
            }
            else
            {
                CurrentDeprecatedFrameData.TimestampMs = 0;
                CurrentDeprecatedFrameData.CpuRgb256x256ImageDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        public override void SetJpeg720x540Image()
        {
            EnsureJpegInitialized();

            const string traceEvent = "SetJpeg720x540Image (CPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventBegin(TraceCategory, TryGetCpuImageEventName);
            bool canConvert = _cpuImage.valid || PlatformDataAcquirer.TryGetCpuImageDeprecated(out _cpuImage);
            ProfilerUtility.EventEnd(TraceCategory, TryGetCpuImageEventName, "CPUImageValid", _cpuImage.valid.ToString(), "GotCPUImage", canConvert.ToString());

            if (canConvert)
            {
                if (_cpuImage.width < DataFormatConstants.Jpeg_720_540_ImgWidth ||
                    _cpuImage.height < DataFormatConstants.Jpeg_720_540_ImgHeight)
                {
                    Log.Warning
                    (
                        $"XR camera image resolution ({_cpuImage.width}x{_cpuImage.height}) is too small to support " +
                        "all enabled Lightship features. Resolution must be at least " +
                        $"{DataFormatConstants.Jpeg_720_540_ImgWidth}x{DataFormatConstants.Jpeg_720_540_ImgHeight}."
                    );

                    CurrentDeprecatedFrameData.CpuJpeg720x540ImageDataLength = 0;
                }
                else
                {
                    ProfilerUtility.EventBegin(TraceCategory, ConvertOnCpuAndWriteToMemoryEventName);
                    // With Lightship's jpeg encoding, Unity's default pixel coordinate convention
                    // (first pixel bottom left) can be passed straight to the Native code.
                    _cpuImage.ConvertOnCpuAndWriteToMemory
                    (
                        CurrentDeprecatedFrameData.Jpeg720x540ImageResolution,
                        _resizedJpegDataHolderPtr
                    );
                    ProfilerUtility.EventEnd(TraceCategory, ConvertOnCpuAndWriteToMemoryEventName);

                    ProfilerUtility.EventBegin(TraceCategory, EncodeNativeArrayToJPGEventName);
                    UInt32 outSize;
                    unsafe
                    {
                        Native.Lightship_ARDK_Unity_CompressJpegRgba(_nativeJpegHandle, _resizedJpegDataHolderPtr,
                            DataFormatConstants.Jpeg_720_540_ImgWidth, DataFormatConstants.Jpeg_720_540_ImgHeight, DataFormatConstants.JpegQuality,
                            (IntPtr)CurrentDeprecatedFrameData.CpuJpeg720x540ImageData.GetUnsafePtr(), out outSize);
                    }
                    ProfilerUtility.EventEnd(TraceCategory, EncodeNativeArrayToJPGEventName);

                    CurrentDeprecatedFrameData.CpuJpeg720x540ImageDataLength = outSize;

                    _currTimestampMs = _cpuImage.timestamp * SecondToMillisecondFactor;
                }
            }
            else
            {
                CurrentDeprecatedFrameData.CpuJpeg720x540ImageDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        // Return true when successfully get the camera intrinsics and reinitialize
        // correspondingly. Otherwise return false.
        protected override bool ReinitializeJpegFullResDataIfNeeded()
        {
            if (PlatformDataAcquirer.TryGetCameraIntrinsicsDeprecated(out XRCameraIntrinsics jpegCameraIntrinsics))
            {
                if (jpegCameraIntrinsics.resolution != CurrentDeprecatedFrameData.JpegFullResImageResolution)
                {

                    // Need to call ReinitializeJpegFullResolutionData() to re-allocate memory
                    // for the full res JPEG image in |_currentFrameData|.
                    CurrentDeprecatedFrameData.ReinitializeJpegFullResolutionData(jpegCameraIntrinsics.resolution);

                    // Need to reallocate the memory for |_fullResJpegDataHolder|.
                    // Dispose the memory before rellocate new ones, to avoid the leak.
                    if (_fullResJpegDataHolder.IsCreated)
                    {
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
                    CurrentDeprecatedFrameData.JpegFullResImageResolution,
                    CurrentDeprecatedFrameData.JpegFullResCameraIntrinsicsData
                );
                CurrentDeprecatedFrameData.JpegFullResCameraIntrinsicsLength = DataFormatConstants.FlatMatrix3x3Length;

                return true;
            }

            // Fail to get the camera image's intrinsics, simply return false and
            // not get the image.
            CurrentDeprecatedFrameData.JpegFullResCameraIntrinsicsLength = 0;
            CurrentDeprecatedFrameData.CpuJpegFullResImageDataLength = 0;
            CurrentDeprecatedFrameData.CpuJpegFullResImageWidth = 0;
            CurrentDeprecatedFrameData.CpuJpegFullResImageHeight = 0;
            return false;
        }

        // TODO(sxian): This task needs to be async.
        public override void SetJpegFullResImage()
        {
            if (!ReinitializeJpegFullResDataIfNeeded())
            {
                return;
            }

            EnsureJpegInitialized();

            const string traceEvent = "SetJpegFullResImage (CPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventBegin(TraceCategory, TryGetCpuImageEventName);
            var canConvert = _cpuImage.valid || PlatformDataAcquirer.TryGetCpuImageDeprecated(out _cpuImage);
            ProfilerUtility.EventEnd(TraceCategory, TryGetCpuImageEventName, "CPUImageValid", _cpuImage.valid.ToString(), "GotCPUImage", canConvert.ToString());

            if (canConvert)
            {
                if (_cpuImage.width != CurrentDeprecatedFrameData.JpegFullResImageResolution.x ||
                    _cpuImage.height != CurrentDeprecatedFrameData.JpegFullResImageResolution.y)
                {
                    Log.Error
                    (
                        $"XR camera image resolution ({_cpuImage.width}x{_cpuImage.height}) " +
                        " do not match _currentFrameData's resolution " +
                        $"{CurrentDeprecatedFrameData.JpegFullResImageResolution.x}x{CurrentDeprecatedFrameData.JpegFullResImageResolution.y}."
                    );

                    CurrentDeprecatedFrameData.CpuJpegFullResImageDataLength = 0;
                    CurrentDeprecatedFrameData.CpuJpegFullResImageWidth = 0;
                    CurrentDeprecatedFrameData.CpuJpegFullResImageHeight = 0;
                }
                else
                {
                    ProfilerUtility.EventBegin(TraceCategory, ConvertOnCpuAndWriteToMemoryEventName);
                    // Buffer to hold the downscaled image (pre-JPEG formatting)
                    _cpuImage.ConvertOnCpuAndWriteToMemory
                    (
                        CurrentDeprecatedFrameData.JpegFullResImageResolution,
                        _fullResJpegDataHolderPtr,
                        TextureFormat.RGBA32
                    );
                    ProfilerUtility.EventEnd(TraceCategory, ConvertOnCpuAndWriteToMemoryEventName);

                    ProfilerUtility.EventBegin(TraceCategory, EncodeNativeArrayToJPGEventName);
                    UInt32 outSize;
                    unsafe
                    {
                        Native.Lightship_ARDK_Unity_CompressJpegRgba(_nativeJpegHandle, _resizedJpegDataHolderPtr,
                            (UInt32)_cpuImage.width, (UInt32)_cpuImage.height, DataFormatConstants.JpegQuality,
                            (IntPtr)CurrentDeprecatedFrameData.CpuJpeg720x540ImageData.GetUnsafePtr(), out outSize);
                    }
                    ProfilerUtility.EventEnd(TraceCategory, EncodeNativeArrayToJPGEventName);

                    CurrentDeprecatedFrameData.CpuJpeg720x540ImageDataLength = outSize;
                    _currTimestampMs = _cpuImage.timestamp * SecondToMillisecondFactor;
                }
            }
            else
            {
                CurrentDeprecatedFrameData.CpuJpegFullResImageDataLength = 0;
                CurrentDeprecatedFrameData.CpuJpegFullResImageWidth = 0;
                CurrentDeprecatedFrameData.CpuJpegFullResImageHeight = 0;
                // Set the intrinsics length to 0 as we fail to get the image.
                CurrentDeprecatedFrameData.JpegFullResCameraIntrinsicsLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        public override void SetPlatformDepthBuffer()
        {
            const string traceEvent = "SetPlatformDepthBuffer (CPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            const string tryGetCpuDepthImageEventName = "TryGetCpuDepthImageDeprecated";
            ProfilerUtility.EventBegin(TraceCategory, tryGetCpuDepthImageEventName);
            bool gotDepthImage = PlatformDataAcquirer.TryGetDepthCpuImageDeprecated(out _depthCpuImage, out _depthConfidenceCpuImage);
            ProfilerUtility.EventEnd(TraceCategory, tryGetCpuDepthImageEventName, "GotCpuDepthImage", gotDepthImage.ToString());

            if (gotDepthImage)
            {
                ProfilerUtility.EventBegin(TraceCategory, ConvertOnCpuAndWriteToMemoryEventName);
                unsafe
                {
                    CurrentDeprecatedFrameData.PlatformDepthDataPtr =
                        (IntPtr)_depthCpuImage.GetPlane(0).data.GetUnsafeReadOnlyPtr();

                    CurrentDeprecatedFrameData.PlatformDepthConfidencesDataPtr =
                        (IntPtr)_depthConfidenceCpuImage.GetPlane(0).data.GetUnsafeReadOnlyPtr();
                }
                ProfilerUtility.EventEnd(TraceCategory, ConvertOnCpuAndWriteToMemoryEventName);

                _currTimestampMs = _depthCpuImage.timestamp * SecondToMillisecondFactor;
                CurrentDeprecatedFrameData.PlatformDepthResolution = _depthCpuImage.dimensions;
                CurrentDeprecatedFrameData.PlatformDepthDataLength =
                    (UInt32)(_depthCpuImage.dimensions.x * _depthCpuImage.dimensions.y);
            }
            else
            {
                CurrentDeprecatedFrameData.PlatformDepthResolution = Vector2Int.zero;
                CurrentDeprecatedFrameData.PlatformDepthDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_CreateJpeg();

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_ReleaseJpeg(IntPtr handle);

            [DllImport(LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_CompressJpegRgba(
                IntPtr handle, IntPtr data, UInt32 width, UInt32 height, int quality,
                IntPtr out_data, out UInt32 out_size);

        }
    }
}
