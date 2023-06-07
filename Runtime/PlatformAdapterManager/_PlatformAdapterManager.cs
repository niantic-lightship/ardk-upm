// Copyright 2023 Niantic Labs. All rights reserved.

using System;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Niantic.Lightship.AR.Playback;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.CTrace;
using PlatformAdapterManager;
using Matrix4x4 = UnityEngine.Matrix4x4;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    // TODO [AR-15859]:
    //  Temporarily implemented _IPlaybackDatasetIngester to pass the dataset reader to the
    //  SubsystemDataAcquirer until pose input is implemented via playback. Remove once ticket is resolved.
    internal class _PlatformAdapterManager : IDisposable, _IPlaybackDatasetUser
    {
        public Action SentData;
        private readonly _FrameData _currentFrameData;

        private readonly _IApi _api;
        private readonly _PlatformDataAcquirer _platformDataAcquirer;

        private IntPtr _nativeHandle;
        private NativeArray<_DataFormat> _readyDataFormats;

        private ulong _frameCounter;
        private bool _alreadyDisposed;

        private readonly _ICTrace _ctrace;
        private const UInt64 CTRACE_PAM_ID = 743263; // A 64bit ID used to group ctrace events.

        internal enum ImageProcessingMode
        {
            CPU,
            GPU
        }

        private ImageProcessingMode _selectedImageProcessingMode;

        private ITexturesSetter _texturesSetter;

        // TODO [AR-16350]: Once linked ticket to use GPU path on device is complete, won't need imageProcessingMode param
        public static _PlatformAdapterManager Create<TApi, TXRDataAcquirer>
        (
            IntPtr contextHandle,
            ImageProcessingMode imageProcessingMode,
            _ICTrace cTrace
        )
            where TApi : _IApi, new()
            where TXRDataAcquirer : _PlatformDataAcquirer, new()
        {
            return new _PlatformAdapterManager(new TApi(), new TXRDataAcquirer(), contextHandle, cTrace,
                imageProcessingMode);
        }

        internal _PlatformAdapterManager
        (
            _IApi api,
            _PlatformDataAcquirer platformDataAcquirer,
            IntPtr unityContext,
            _ICTrace ctrace,
            ImageProcessingMode imageProcessingMode = ImageProcessingMode.CPU
        )
        {
            _api = api;
            _platformDataAcquirer = platformDataAcquirer;
            _ctrace = ctrace;
            _selectedImageProcessingMode = imageProcessingMode;

            _nativeHandle = _api.Lightship_ARDK_Unity_PAM_Create(unityContext);

            var numFormats = Enum.GetValues(typeof(_DataFormat)).Length;
            _readyDataFormats = new NativeArray<_DataFormat>(numFormats, Allocator.Persistent);

            _currentFrameData = new _FrameData(imageProcessingMode);
            if (imageProcessingMode == ImageProcessingMode.CPU)
            {
                _texturesSetter =
                    new CpuTexturesSetter(_platformDataAcquirer, _currentFrameData, _ctrace, CTRACE_PAM_ID);
            }
            else
            {
                _texturesSetter =
                    new GpuTexturesSetter(_platformDataAcquirer, _currentFrameData, _ctrace, CTRACE_PAM_ID);
            }

            Debug.Log
            (
                $"{nameof(_PlatformAdapterManager)}>{_api.GetType()}, <{_platformDataAcquirer.GetType()}> was created " +
                $"with nativeHandle ({_nativeHandle})"
            );

            _MonoBehaviourEventDispatcher.Updating += SendUpdatedFrameData;
        }

        ~_PlatformAdapterManager()
        {
            Dispose(false);
        }

        public void SetPlaybackDatasetReader(_PlaybackDatasetReader reader)
        {
            if (_platformDataAcquirer is _IPlaybackDatasetUser datasetUser)
                datasetUser.SetPlaybackDatasetReader(reader);
        }

        public void Dispose()
        {
            Debug.Log($"{nameof(_PlatformAdapterManager)} was disposed.");

            GC.SuppressFinalize(this);
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_alreadyDisposed)
            {
                return;
            }

            _alreadyDisposed = true;
            if (_nativeHandle != IntPtr.Zero)
            {
                _api.Lightship_ARDK_Unity_PAM_Release(_nativeHandle);
                _nativeHandle = IntPtr.Zero;
            }

            _readyDataFormats.Dispose();
            _platformDataAcquirer.Dispose();
            _currentFrameData.Dispose();

            if (_texturesSetter != null)
                _texturesSetter.Dispose();

            _MonoBehaviourEventDispatcher.Updating -= SendUpdatedFrameData;
        }

        private void SetRgba256x144CameraIntrinsics()
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
            {
                _ImageConverter.ConvertCameraIntrinsics
                (
                    cameraIntrinsics,
                    _currentFrameData.Rgba256x144ImageResolution,
                    _currentFrameData.Rgba256x144CameraIntrinsicsData
                );

                _currentFrameData.Rgba256x144CameraIntrinsicsLength = _DataFormatConstants.FLAT_MATRIX3x3_LENGTH;
            }
            else
            {
                _currentFrameData.Rgba256x144CameraIntrinsicsLength = 0;
            }
        }

        private void SetJpeg720x540CameraIntrinsics()
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsics(out XRCameraIntrinsics jpegCameraIntrinsics))
            {
                _ImageConverter.ConvertCameraIntrinsics
                (
                    jpegCameraIntrinsics,
                    _currentFrameData.Jpeg720x540ImageResolution,
                    _currentFrameData.Jpeg720x540CameraIntrinsicsData
                );
                _currentFrameData.Jpeg720x540CameraIntrinsicsLength = _DataFormatConstants.FLAT_MATRIX3x3_LENGTH;
            }
            else
            {
                _currentFrameData.Jpeg720x540CameraIntrinsicsLength = 0;
            }
        }

        private void SetPlatformDepthCameraIntrinsics(Vector2Int resolution)
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
            {
                // TODO (AR-16968): Collate calls to TryGetCameraIntrinsics
                _ImageConverter.ConvertCameraIntrinsics
                (
                    cameraIntrinsics,
                    resolution,
                    _currentFrameData.PlatformDepthCameraIntrinsicsData
                );

                _currentFrameData.PlatformDepthCameraIntrinsicsLength = _DataFormatConstants.FLAT_MATRIX3x3_LENGTH;
            }
            else
            {
                _currentFrameData.PlatformDepthCameraIntrinsicsLength = 0;
            }
        }

        private void SetGpsLocation()
        {
            if (_platformDataAcquirer.TryGetGpsLocation(out GpsLocation gps))
            {
                _currentFrameData.SetGpsData(gps);
                _currentFrameData.GpsLocationLength = (UInt32)UnsafeUtility.SizeOf<GpsLocation>();
            }
            else
            {
                _currentFrameData.GpsLocationLength = 0;
            }
        }

        private void SetCompass()
        {
            if (_platformDataAcquirer.TryGetCompass(out CompassData compass))
            {
                _currentFrameData.SetCompassData(compass);
                _currentFrameData.CompassDataLength = (UInt32)UnsafeUtility.SizeOf<CompassData>();
            }
            else
            {
                _currentFrameData.CompassDataLength = 0;
            }
        }

        private void SetOcclusionContext()
        {
            var success = _platformDataAcquirer.TryGetImageResolution(out var resolution);
            var aspect = success ? resolution.width / (float)resolution.height : 1920.0f / 1440.0f;
            var context = new OcclusionContext
            {
                OccludeeEyeDepth = ARFoundation.OcclusionContext.Shared.OccludeeEyeDepth,
                FullImageAspectRatio = aspect
            };

            _currentFrameData.SetOcclusionContext(context);
            _currentFrameData.OcclusionContextDataLength = (UInt32)UnsafeUtility.SizeOf<OcclusionContext>();
        }

        private void SendUpdatedFrameData()
        {
            const string cTraceName = "PAM::SendUpdatedFrameData";
            if (!_platformDataAcquirer.TryToBeReady())
                return;

            _api.Lightship_ARDK_Unity_PAM_GetDataFormatsReadyForNewFrame
            (
                _nativeHandle,
                _readyDataFormats,
                out int formatsSize
            );

            _ctrace.TraceEventInstance1("SAL", cTraceName, "formatsize", (UInt64)(formatsSize));

            if (formatsSize == 0)
                return;

            _ctrace.TraceEventAsyncBegin1("SAL", cTraceName, CTRACE_PAM_ID, "formatsize", (UInt64)(formatsSize));

            for (var i = 0; i < formatsSize; i++)
            {
                var dataFormat = _readyDataFormats[i];

                switch (dataFormat)
                {
                    case _DataFormat.kCpuRgba_256_144_Uint8:
                        SetRgba256x144CameraIntrinsics();

                        // Not going to bother checking if the resolution is large enough here, because presumably
                        // any AR camera image is going to be larger than 256 x 144
                        _texturesSetter.SetRgba256x144Image();
                        break;

                    case _DataFormat.kJpeg_720_540_Uint8:
                        SetJpeg720x540CameraIntrinsics();

                        _texturesSetter.SetJpeg720x540Image();
                        break;

                    case _DataFormat.kGpsLocation:
                        SetGpsLocation();
                        break;

                    case _DataFormat.kCompass:
                        SetCompass();
                        break;

                    case _DataFormat.kOcclusion_context:
                        SetOcclusionContext();
                        break;

                    case _DataFormat.kJpeg_full_res_Uint8:
                        _texturesSetter.SetJpegFullResImage();
                        break;

                    case _DataFormat.kPlatform_depth:
                        _texturesSetter.SetPlatformDepthBuffer();

                        // Need to acquire platform depth buffer first, so that resolution is known
                        if (_currentFrameData.PlatformDepthDataLength > 0)
                            SetPlatformDepthCameraIntrinsics(_currentFrameData.PlatformDepthResolution);

                        break;
                }
            }

            _ctrace.TraceEventAsyncStep0("SAL", cTraceName, CTRACE_PAM_ID, "SetCommonData");

            SetCameraPose();

            _currentFrameData.TimestampMs = (ulong)_texturesSetter.GetCurrentTimestampMs();

            _currentFrameData.DeviceOrientation = _platformDataAcquirer.GetDeviceOrientation().FromUnityToArdk();
            _currentFrameData.TrackingState = _platformDataAcquirer.GetTrackingState().FromUnityToArdk();
            _currentFrameData.FrameId = _frameCounter++;

            // SAH handles checking if all required data is in this frame
            _ctrace.TraceEventAsyncStep0("SAL", cTraceName, CTRACE_PAM_ID, "SendData");
            unsafe
            {
                fixed (void* pointerToFrameStruct = &_currentFrameData.frameCStruct)
                {
                    _api.Lightship_ARDK_Unity_PAM_OnFrame(_nativeHandle, (IntPtr)pointerToFrameStruct);
                }
            }

            _ctrace.TraceEventAsyncEnd0("SAL", cTraceName, CTRACE_PAM_ID);

            _texturesSetter.InvalidateCachedTextures();
            SentData?.Invoke();
        }

        private void SetCameraPose()
        {
            if (_platformDataAcquirer.TryGetCameraPose(out Matrix4x4 cameraToLocal))
            {
                _currentFrameData.SetPoseData(cameraToLocal.FromUnityToArdk().ToColumnMajorArray());
                _currentFrameData.CameraPoseLength = _DataFormatConstants.FLAT_MATRIX4x4_LENGTH;
            }
            else
            {
                _currentFrameData.CameraPoseLength = 0;
            }
        }
    }
}
