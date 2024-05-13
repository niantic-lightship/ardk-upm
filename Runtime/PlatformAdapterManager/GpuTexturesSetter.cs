// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities.Profiling;
using Niantic.Lightship.AR.Utilities.Textures;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PAM
{
    internal class GpuTexturesSetter: AbstractTexturesSetter
    {
        // GPU conversion path
        private Texture2D _gpuImage;
        private Texture2D _gpuDepthImage;
        private Texture2D _gpuConfidenceImage;

        private Texture2D _awarenessOutputTexture;
        private Texture2D _squareOutputTexture;
        private Texture2D _jpegOutputTexture;
        private Texture2D _jpegFullResOutputTexture;
        private Texture2D _depthOutputTexture;
        private Texture2D _depthConfidenceOutputTexture;

        private bool _isGpuImageValid;

        // Trace Event Strings
        private const string TraceCategory = "GpuTexturesSetter";
        private const string TryGetGpuImageEventName = "TryGetGpuImage";
        private const string AllocateTextureEventName = "AllocateTexture";
        private const string ConvertOnGpuAndCopyEventName = "ConvertOnGpuAndCopy";
        private const string EncodeToJPGEventName = "EncodeToJPG";
        private const string CopyJPGToFrameMemEventName = "CopyJPGToFrameMem";
        private const string ReadFromExternalTextureEventName = "ReadFromExternalTexture";

        public GpuTexturesSetter(PlatformDataAcquirer dataAcquirer, FrameData frameData) : base(dataAcquirer, frameData)
        {
        }

        public override void Dispose()
        {
            DestroyTextureIfNotNull(_awarenessOutputTexture);
            DestroyTextureIfNotNull(_squareOutputTexture);
            DestroyTextureIfNotNull(_jpegOutputTexture);
            DestroyTextureIfNotNull(_jpegFullResOutputTexture);
            DestroyTextureIfNotNull(_depthOutputTexture);
            DestroyTextureIfNotNull(_depthConfidenceOutputTexture);
        }

        public override void InvalidateCachedTextures()
        {
            _isGpuImageValid = false;
        }

        private void DestroyTextureIfNotNull(Texture2D tex)
        {
            if (tex != null)
            {
                if (Application.isPlaying)
                {
                    GameObject.Destroy(tex);
                }
                else
                {
                    GameObject.DestroyImmediate(tex);
                }
            }
        }

        public override void SetRgba256x144Image()
        {
            const string traceEvent = "SetRgba256x144Image (GPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventBegin(TraceCategory, TryGetGpuImageEventName);
            bool canConvert = _isGpuImageValid || PlatformDataAcquirer.TryGetGpuImage(out _gpuImage);
            ProfilerUtility.EventEnd(TraceCategory, TryGetGpuImageEventName, "GPUImageAlreadyValid", _isGpuImageValid.ToString(), "GotGPUImage", canConvert.ToString());

            if (canConvert)
            {
                _isGpuImageValid = true;

                if (_awarenessOutputTexture == null)
                {
                    ProfilerUtility.EventBegin(TraceCategory, AllocateTextureEventName);
                    var resolution = CurrentFrameData.Rgba256x144ImageResolution;
                    _awarenessOutputTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
                    ProfilerUtility.EventEnd(TraceCategory, AllocateTextureEventName);
                }

                ProfilerUtility.EventBegin(TraceCategory, ConvertOnGpuAndCopyEventName);
                _gpuImage.ConvertOnGpuAndCopy(_awarenessOutputTexture);
                ProfilerUtility.EventEnd(TraceCategory, ConvertOnGpuAndCopyEventName);

                unsafe
                {
                    CurrentFrameData.CpuRgba256x144ImageDataPtr =
                        (IntPtr)_awarenessOutputTexture.GetRawTextureData<byte>().GetUnsafeReadOnlyPtr();
                }

                CurrentFrameData.CpuRgba256x144ImageDataLength = DataFormatConstants.Rgba_256_144_DataLength;
            }
            else
            {
                CurrentFrameData.CpuRgba256x144ImageDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        public override void SetRgb256x256Image()
        {
            const string traceEvent = "SetRgb256x256Image (GPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventBegin(TraceCategory, TryGetGpuImageEventName);
            bool canConvert = _isGpuImageValid || PlatformDataAcquirer.TryGetGpuImage(out _gpuImage);
            ProfilerUtility.EventEnd(TraceCategory, TryGetGpuImageEventName, "GPUImageAlreadyValid", _isGpuImageValid.ToString(), "GotGPUImage", canConvert.ToString());

            if (canConvert)
            {
                _isGpuImageValid = true;

                if (_squareOutputTexture == null)
                {
                    ProfilerUtility.EventBegin(TraceCategory, AllocateTextureEventName);
                    var resolution = CurrentFrameData.Rgb256x256ImageResolution;
                    _squareOutputTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
                    ProfilerUtility.EventEnd(TraceCategory, AllocateTextureEventName);
                }

                ProfilerUtility.EventBegin(TraceCategory, ConvertOnGpuAndCopyEventName);
                _gpuImage.ConvertOnGpuAndCopy(_squareOutputTexture);
                ProfilerUtility.EventEnd(TraceCategory, ConvertOnGpuAndCopyEventName);

                unsafe
                {
                    CurrentFrameData.CpuRgb256x256ImageDataPtr =
                        (IntPtr)_squareOutputTexture.GetRawTextureData<byte>().GetUnsafeReadOnlyPtr();
                }

                CurrentFrameData.CpuRgb256x256ImageDataLength = DataFormatConstants.Rgb_256_256_DataLength;
            }
            else
            {
                CurrentFrameData.CpuRgb256x256ImageDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        public override void SetJpeg720x540Image()
        {
            const string traceEvent = "SetJpeg720x540Image (GPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventBegin(TraceCategory, TryGetGpuImageEventName);
            bool canConvert = _isGpuImageValid || PlatformDataAcquirer.TryGetGpuImage(out _gpuImage);
            ProfilerUtility.EventEnd(TraceCategory, TryGetGpuImageEventName, "GPUImageAlreadyValid", _isGpuImageValid.ToString(), "GotGPUImage", canConvert.ToString());

            if (canConvert)
            {
                _isGpuImageValid = true;
                if (_jpegOutputTexture == null)
                {
                    ProfilerUtility.EventBegin(TraceCategory, AllocateTextureEventName);
                    var resolution = CurrentFrameData.Jpeg720x540ImageResolution;
                    _jpegOutputTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
                    ProfilerUtility.EventEnd(TraceCategory, AllocateTextureEventName);
                }

                // Unity's EncodeToJPG method expects as input texture data formatted in Unity convention (first pixel
                // bottom left), whereas the raw data of AR textures are in first pixel top left convention.
                // Thus we invert the pixels in this step, so they are output correctly oriented in the encoding step.
                ProfilerUtility.EventBegin(TraceCategory, ConvertOnGpuAndCopyEventName);
                _gpuImage.ConvertOnGpuAndCopy(_jpegOutputTexture, mirrorX: true);
                ProfilerUtility.EventEnd(TraceCategory, ConvertOnGpuAndCopyEventName);

                // New buffer from Unity that contains the JPEG data
                ProfilerUtility.EventBegin(TraceCategory, EncodeToJPGEventName);
                var jpegArray = _jpegOutputTexture.EncodeToJPG(DataFormatConstants.JpegQuality);
                ProfilerUtility.EventEnd(TraceCategory, EncodeToJPGEventName);

                // Copy the JPEG byte array to the shared buffer in FrameCStruct
                ProfilerUtility.EventBegin(TraceCategory, CopyJPGToFrameMemEventName);
                unsafe
                {
                    void* managedJpeg = UnsafeUtility.AddressOf(ref jpegArray[0]);
                    void* nativeJpeg = (void*)CurrentFrameData.CpuJpeg720x540ImageDataPtr;
                    UnsafeUtility.MemCpy(nativeJpeg, managedJpeg, jpegArray.Length);
                }
                ProfilerUtility.EventEnd(TraceCategory, CopyJPGToFrameMemEventName);

                CurrentFrameData.CpuJpeg720x540ImageDataLength = (uint)jpegArray.Length;
            }
            else
            {
                CurrentFrameData.CpuJpeg720x540ImageDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        public override void SetJpegFullResImage()
        {
            if (!ReinitializeJpegFullResDataIfNeeded())
                return;

            const string traceEvent = "PAM::JpegFullResImageWithGpu";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventBegin(TraceCategory, TryGetGpuImageEventName);
            bool canConvert = _isGpuImageValid || PlatformDataAcquirer.TryGetGpuImage(out _gpuImage);
            ProfilerUtility.EventEnd(TraceCategory, TryGetGpuImageEventName, "GPUImageAlreadyValid", _isGpuImageValid.ToString(), "GotGPUImage", canConvert.ToString());

            if (canConvert)
            {
                _isGpuImageValid = true;
                if (_jpegFullResOutputTexture == null)
                {
                    ProfilerUtility.EventBegin(TraceCategory, AllocateTextureEventName);
                    var resolution = CurrentFrameData.JpegFullResImageResolution;
                    _jpegFullResOutputTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
                    ProfilerUtility.EventEnd(TraceCategory, AllocateTextureEventName);
                }

                // Unity's EncodeToJPG method expects as input texture data formatted in Unity convention (first pixel
                // bottom left), whereas the raw data of AR textures are in first pixel top left convention.
                // Thus we invert the pixels in this step, so they are output correctly oriented in the encoding step.
                ProfilerUtility.EventBegin(TraceCategory, ConvertOnGpuAndCopyEventName);
                _gpuImage.ConvertOnGpuAndCopy(_jpegFullResOutputTexture, mirrorX: true);
                ProfilerUtility.EventEnd(TraceCategory, ConvertOnGpuAndCopyEventName);

                // New buffer from Unity that contains the JPEG data
                ProfilerUtility.EventBegin(TraceCategory, EncodeToJPGEventName);
                var jpegArray = _jpegFullResOutputTexture.EncodeToJPG(DataFormatConstants.JpegQuality);
                ProfilerUtility.EventEnd(TraceCategory, EncodeToJPGEventName);

                // Copy the JPEG byte array to the shared buffer in FrameCStruct
                ProfilerUtility.EventBegin(TraceCategory, CopyJPGToFrameMemEventName);
                unsafe
                {
                    void* managedJpeg = UnsafeUtility.AddressOf(ref jpegArray[0]);
                    void* nativeJpeg = (void*)CurrentFrameData.CpuJpegFullResImageDataPtr;
                    UnsafeUtility.MemCpy(nativeJpeg, managedJpeg, jpegArray.Length);
                }
                ProfilerUtility.EventEnd(TraceCategory, CopyJPGToFrameMemEventName);

                CurrentFrameData.CpuJpegFullResImageDataLength = (uint)jpegArray.Length;
            }
            else
            {
                CurrentFrameData.CpuJpegFullResImageDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        public override void SetPlatformDepthBuffer()
        {
            const string traceEvent = "SetPlatformDepthBuffer (GPU)";
            ProfilerUtility.EventBegin(TraceCategory, traceEvent);

            ProfilerUtility.EventBegin(TraceCategory, TryGetGpuImageEventName);
            bool gotDepthImage = PlatformDataAcquirer.TryGetGpuDepthImage(out _gpuDepthImage, out _gpuConfidenceImage);
            ProfilerUtility.EventEnd(TraceCategory, TryGetGpuImageEventName, "GotGpuDepthImage", gotDepthImage.ToString());

            if (gotDepthImage)
            {
                if (_depthOutputTexture == null)
                {
                    ProfilerUtility.EventBegin(TraceCategory, AllocateTextureEventName);

                    _depthOutputTexture =
                        new Texture2D(_gpuDepthImage.width, _gpuDepthImage.height, TextureFormat.RFloat, false);

                    _depthConfidenceOutputTexture =
                        new Texture2D(_gpuConfidenceImage.width, _gpuConfidenceImage.height, TextureFormat.R8, false);

                    ProfilerUtility.EventEnd(TraceCategory, AllocateTextureEventName);
                }

                ProfilerUtility.EventBegin(TraceCategory, ReadFromExternalTextureEventName);
                _gpuDepthImage.ReadFromExternalTexture(_depthOutputTexture);
                _gpuConfidenceImage.ReadFromExternalTexture(_depthConfidenceOutputTexture);
                ProfilerUtility.EventEnd(TraceCategory, ReadFromExternalTextureEventName);

                unsafe
                {
                    CurrentFrameData.PlatformDepthDataPtr =
                        (IntPtr)_depthOutputTexture.GetRawTextureData<byte>().GetUnsafeReadOnlyPtr();

                    CurrentFrameData.PlatformDepthConfidencesDataPtr =
                        (IntPtr)_depthConfidenceOutputTexture.GetRawTextureData<byte>().GetUnsafeReadOnlyPtr();
                }

                CurrentFrameData.PlatformDepthResolution = new Vector2Int(_gpuDepthImage.width, _gpuDepthImage.height);
                CurrentFrameData.PlatformDepthDataLength = (uint)(_gpuDepthImage.width * _gpuDepthImage.height);
            }
            else
            {
                CurrentFrameData.PlatformDepthResolution = Vector2Int.zero;
                CurrentFrameData.PlatformDepthDataLength = 0;
            }

            ProfilerUtility.EventEnd(TraceCategory, traceEvent);
        }

        protected override bool ReinitializeJpegFullResDataIfNeeded()
        {
            if (PlatformDataAcquirer.TryGetCameraIntrinsics(out XRCameraIntrinsics jpegCameraIntrinsics))
            {
                if (jpegCameraIntrinsics.resolution != CurrentFrameData.JpegFullResImageResolution)
                {
                    // Need to call ReinitializeJpegFullResolutionData() to re-allocate memory
                    // for the full res JPEG image in |_currentFrameData|.
                    CurrentFrameData.ReinitializeJpegFullResolutionData(jpegCameraIntrinsics.resolution);
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
    }
}
