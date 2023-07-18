// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.CTrace;
using PlatformAdapterManager;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    internal class GpuTexturesSetter: AbstractTexturesSetter
    {
        // GPU conversion path
        private Texture2D _gpuImage;
        private Texture2D _gpuDepthImage;
        private Texture2D _gpuConfidenceImage;

        private Texture2D _awarenessOutputTexture;
        private Rect _awarenessOutputRect;
        private Texture2D _jpegOutputTexture;
        private Rect _jpegOutputRect;
        private Texture2D _jpegFullResOutputTexture;
        private Rect _jpegFullResOutputRect;
        private Texture2D _depthOutputTexture;
        private Texture2D _depthConfidenceOutputTexture;
        private Rect _depthOutputRect;

        private bool _isGpuImageValid;

        public GpuTexturesSetter(_PlatformDataAcquirer dataAcquirer, _FrameData frameData, _ICTrace ctrace, ulong ctraceId) : base(dataAcquirer, frameData, ctrace, ctraceId) { }

        public override void Dispose()
        {
            DestroyTextureIfNotNull(_awarenessOutputTexture);
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
                    GameObject.Destroy(tex);
                else
                    GameObject.DestroyImmediate(tex);
            }
        }

        public override void SetRgba256x144Image()
        {
            const string cTraceName = "PAM::AwarenessGPUConversion";
            _ctrace.TraceEventAsyncBegin0("SAL", cTraceName, _ctraceId);
            _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "TryGetGpuImage");
            if (_isGpuImageValid || _platformDataAcquirer.TryGetGpuImage(out _gpuImage))
            {
                _isGpuImageValid = true;

                _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "ConvertOnGpuAndWriteToMemory");

                if (_awarenessOutputTexture == null)
                {
                    var resolution = _currentFrameData.Rgba256x144ImageResolution;
                    _awarenessOutputTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
                    _awarenessOutputRect = new Rect(0, 0, resolution.x, resolution.y);
                }

                _gpuImage.ConvertOnGpuAndCopy(_awarenessOutputTexture, _awarenessOutputRect);

                unsafe
                {
                    _currentFrameData.CpuRgba256x144ImageDataPtr =
                        (IntPtr)_awarenessOutputTexture.GetRawTextureData<byte>().GetUnsafeReadOnlyPtr();
                }

                _currentFrameData.CpuRgba256x144ImageDataLength = _DataFormatConstants.RGBA_256_144_DATA_LENGTH;
            }
            else
            {
                _currentFrameData.CpuRgba256x144ImageDataLength = 0;
            }

            _ctrace.TraceEventAsyncEnd0("SAL", cTraceName, _ctraceId);
        }

        public override void SetJpeg720x540Image()
        {
            const string cTraceName = "PAM::JpegGPUConversion";
            _ctrace.TraceEventAsyncBegin0("SAL", cTraceName, _ctraceId);
            _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "TryGetGpuImage");
            if (_isGpuImageValid || _platformDataAcquirer.TryGetGpuImage(out _gpuImage))
            {
                _isGpuImageValid = true;
                _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "ConvertOnGpuAndWriteToMemory");
                if (_jpegOutputTexture == null)
                {
                    var resolution = _currentFrameData.Jpeg720x540ImageResolution;
                    _jpegOutputTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);

                    _jpegOutputRect = new Rect(0, 0, resolution.x, resolution.y);
                }

                // Unity's EncodeToJPG method expects as input texture data formatted in Unity convention (first pixel
                // bottom left), whereas the raw data of AR textures are in first pixel top left convention.
                // Thus we invert the pixels in this step, so they are output correctly oriented in the encoding step.
                _gpuImage.ConvertOnGpuAndCopy(_jpegOutputTexture, _jpegOutputRect, mirrorX: true);

                // New buffer from Unity that contains the JPEG data
                _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "EncodeToJPG");
                var jpegArray = _jpegOutputTexture.EncodeToJPG(_DataFormatConstants.JPEG_QUALITY);

                // Copy the JPEG byte array to the shared buffer in FrameCStruct
                _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "Copy");
                unsafe
                {
                    void* managedJpeg = UnsafeUtility.AddressOf(ref jpegArray[0]);
                    void* nativeJpeg = (void*)_currentFrameData.CpuJpeg720x540ImageDataPtr;
                    UnsafeUtility.MemCpy(nativeJpeg, managedJpeg, jpegArray.Length);
                }

                _currentFrameData.CpuJpeg720x540ImageDataLength = (uint)jpegArray.Length;
            }
            else
            {
                _currentFrameData.CpuJpeg720x540ImageDataLength = 0;
            }

            _ctrace.TraceEventAsyncEnd0("SAL", cTraceName, _ctraceId);
        }

        public override void SetJpegFullResImage()
        {
            if (!ReinitializeJpegFullResDataIfNeeded())
                return;

            const string cTraceName = "PAM::JpegFullResImageWithGpu";
            _ctrace.TraceEventAsyncBegin0("SAL", cTraceName, _ctraceId);
            _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "TryGetGpuImage");

            if (_isGpuImageValid || _platformDataAcquirer.TryGetGpuImage(out _gpuImage))
            {
                _isGpuImageValid = true;
                _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "ConvertOnGpuAndWriteToMemory");
                if (_jpegFullResOutputTexture == null)
                {
                    var resolution = _currentFrameData.JpegFullResImageResolution;
                    _jpegFullResOutputTexture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);

                    _jpegFullResOutputRect = new Rect(0, 0, resolution.x, resolution.y);
                }

                // Unity's EncodeToJPG method expects as input texture data formatted in Unity convention (first pixel
                // bottom left), whereas the raw data of AR textures are in first pixel top left convention.
                // Thus we invert the pixels in this step, so they are output correctly oriented in the encoding step.
                _gpuImage.ConvertOnGpuAndCopy(_jpegFullResOutputTexture, _jpegFullResOutputRect, mirrorX: true);

                // New buffer from Unity that contains the JPEG data
                _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "EncodeToJPG");
                var jpegArray = _jpegFullResOutputTexture.EncodeToJPG(_DataFormatConstants.JPEG_QUALITY);

                // Copy the JPEG byte array to the shared buffer in FrameCStruct
                _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "Copy");
                unsafe
                {
                    void* managedJpeg = UnsafeUtility.AddressOf(ref jpegArray[0]);
                    void* nativeJpeg = (void*)_currentFrameData.CpuJpegFullResImageDataPtr;
                    UnsafeUtility.MemCpy(nativeJpeg, managedJpeg, jpegArray.Length);
                }

                _currentFrameData.CpuJpegFullResImageDataLength = (uint)jpegArray.Length;
            }
            else
            {
                _currentFrameData.CpuJpegFullResImageDataLength = 0;
            }

            _ctrace.TraceEventAsyncEnd0("SAL", cTraceName, _ctraceId);
        }

        public override void SetPlatformDepthBuffer()
        {
            const string cTraceName = "PAM::DepthGPUAcquirement";
            _ctrace.TraceEventAsyncBegin0("SAL", cTraceName, _ctraceId);
            _ctrace.TraceEventAsyncStep0("SAL", cTraceName, _ctraceId, "TryGetDepthImage");
            if (_platformDataAcquirer.TryGetGpuDepthImage(out _gpuDepthImage, out _gpuConfidenceImage))
            {
                if (_depthOutputTexture == null)
                {
                    _depthOutputTexture =
                        new Texture2D(_gpuDepthImage.width, _gpuDepthImage.height, TextureFormat.RFloat, false);

                    _depthConfidenceOutputTexture =
                        new Texture2D(_gpuConfidenceImage.width, _gpuConfidenceImage.height, TextureFormat.R8, false);

                    _depthOutputRect = new Rect(0, 0, _gpuDepthImage.width, _gpuDepthImage.height);
                }

                _gpuDepthImage.ReadFromExternalTexture(_depthOutputTexture, _depthOutputRect);
                _gpuConfidenceImage.ReadFromExternalTexture(_depthConfidenceOutputTexture, _depthOutputRect);

                unsafe
                {
                    _currentFrameData.PlatformDepthDataPtr =
                        (IntPtr)_depthOutputTexture.GetRawTextureData<byte>().GetUnsafeReadOnlyPtr();

                    _currentFrameData.PlatformDepthConfidencesDataPtr =
                        (IntPtr)_depthConfidenceOutputTexture.GetRawTextureData<byte>().GetUnsafeReadOnlyPtr();
                }

                _currentFrameData.PlatformDepthResolution = new Vector2Int(_gpuDepthImage.width, _gpuDepthImage.height);
                _currentFrameData.PlatformDepthDataLength = (uint)(_gpuDepthImage.width * _gpuDepthImage.height);
            }
            else
            {
                _currentFrameData.PlatformDepthResolution = Vector2Int.zero;
                _currentFrameData.PlatformDepthDataLength = 0;
            }

            _ctrace.TraceEventAsyncEnd0("SAL", cTraceName, _ctraceId);
        }

        protected override bool ReinitializeJpegFullResDataIfNeeded()
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsics(out XRCameraIntrinsics jpegCameraIntrinsics))
            {
                if (jpegCameraIntrinsics.resolution != _currentFrameData.JpegFullResImageResolution)
                {
                    // Need to call ReinitializeJpegFullResolutionData() to re-allocate memory
                    // for the full res JPEG image in |_currentFrameData|.
                    _currentFrameData.ReinitializeJpegFullResolutionData(jpegCameraIntrinsics.resolution);
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
    }
}
