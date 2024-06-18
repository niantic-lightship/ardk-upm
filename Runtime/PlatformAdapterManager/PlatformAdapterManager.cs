//#define NIANTIC_ARDK_USE_FAST_LIGHTWEIGHT_PAM
// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Profiling;
using Matrix4x4 = UnityEngine.Matrix4x4;

namespace Niantic.Lightship.AR.PAM
{
    internal class PlatformAdapterManager : IDisposable
    {
        public Action<PamEventArgs> SentData;
        private readonly DeprecatedFrameData _currentDeprecatedFrameDataDeprecated;

        private readonly IApi _api;
        private readonly PlatformDataAcquirer _platformDataAcquirer;

        private IntPtr _nativeHandle;
        private NativeArray<DataFormat> _addedDataFormats;
        private NativeArray<DataFormat> _readyDataFormats;
        private NativeArray<DataFormat> _removedDataFormats;
        private int _readyDataFormatsSize;

        private uint _frameCounter;
        private bool _alreadyDisposed;

        private readonly AbstractTexturesSetter _texturesSetter;

        private const string TraceCategory = "PlatformAdapterManager";

        public static PlatformAdapterManager Create<TApi, TXRDataAcquirer>(IntPtr contextHandle, bool isLidarDepthEnabled, bool trySendOnUpdate)
            where TApi : IApi, new()
            where TXRDataAcquirer : PlatformDataAcquirer, new()
        {
            return new PlatformAdapterManager(new TApi(), new TXRDataAcquirer(), contextHandle, isLidarDepthEnabled, trySendOnUpdate);
        }

        public PlatformAdapterManager
        (
            IApi api,
            PlatformDataAcquirer platformDataAcquirer,
            IntPtr unityContext,
            bool isLidarDepthEnabled,
            bool trySendOnUpdate = true
        )
        {
            _api = api;
            _platformDataAcquirer = platformDataAcquirer;
            _nativeHandle = _api.Lightship_ARDK_Unity_PAM_Create(unityContext, isLidarDepthEnabled);

            var numFormats = Enum.GetValues(typeof(DataFormat)).Length;

            _addedDataFormats = new NativeArray<DataFormat>(numFormats, Allocator.Persistent);
            _readyDataFormats = new NativeArray<DataFormat>(numFormats, Allocator.Persistent);
            _removedDataFormats = new NativeArray<DataFormat>(numFormats, Allocator.Persistent);

            _currentDeprecatedFrameDataDeprecated = new DeprecatedFrameData();
            _texturesSetter = new CpuTexturesSetter(_platformDataAcquirer, _currentDeprecatedFrameDataDeprecated);

            Log.Info
            (
                $"{nameof(PlatformAdapterManager)}>{_api.GetType()}, <{_platformDataAcquirer.GetType()}> was created " +
                $"with nativeHandle ({_nativeHandle})"
            );

            Application.onBeforeRender += OnBeforeRender;
            if (trySendOnUpdate)
            {
#if NIANTIC_ARDK_USE_FAST_LIGHTWEIGHT_PAM
                MonoBehaviourEventDispatcher.Updating.AddListener(SendUpdatedFrameData);
#else
                MonoBehaviourEventDispatcher.Updating.AddListener(DeprecatedSendUpdatedFrameData);
#endif
            }
        }

        ~PlatformAdapterManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Log.Info($"{nameof(PlatformAdapterManager)} was disposed.");

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

            _addedDataFormats.Dispose();
            _readyDataFormats.Dispose();
            _removedDataFormats.Dispose();

            _platformDataAcquirer.Dispose();
            _currentDeprecatedFrameDataDeprecated.Dispose();

            if (_texturesSetter != null)
                _texturesSetter.Dispose();

#if NIANTIC_ARDK_USE_FAST_LIGHTWEIGHT_PAM
                MonoBehaviourEventDispatcher.Updating.RemoveListener(SendUpdatedFrameData);
#else
            MonoBehaviourEventDispatcher.Updating.RemoveListener(DeprecatedSendUpdatedFrameData);
#endif

            Application.onBeforeRender -= OnBeforeRender;
        }

        private void SetRgba256x144CameraIntrinsics()
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsicsDeprecated(out XRCameraIntrinsics cameraIntrinsics))
            {
                ImageConverter.ConvertCameraIntrinsics
                (
                    cameraIntrinsics,
                    _currentDeprecatedFrameDataDeprecated.Rgba256x144ImageResolution,
                    _currentDeprecatedFrameDataDeprecated.Rgba256x144CameraIntrinsicsData
                );

                _currentDeprecatedFrameDataDeprecated.Rgba256x144CameraIntrinsicsLength = DataFormatConstants.FlatMatrix3x3Length;
            }
            else
            {
                _currentDeprecatedFrameDataDeprecated.Rgba256x144CameraIntrinsicsLength = 0;
            }
        }

        private void SetRgb256x256CameraIntrinsics()
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsicsDeprecated(out XRCameraIntrinsics cameraIntrinsics))
            {
                ImageConverter.ConvertCameraIntrinsics
                (
                    cameraIntrinsics,
                    _currentDeprecatedFrameDataDeprecated.Rgb256x256ImageResolution,
                    _currentDeprecatedFrameDataDeprecated.Rgb256x256CameraIntrinsicsData
                );

                _currentDeprecatedFrameDataDeprecated.Rgb256x256CameraIntrinsicsLength = DataFormatConstants.FlatMatrix3x3Length;
            }
            else
            {
                _currentDeprecatedFrameDataDeprecated.Rgb256x256CameraIntrinsicsLength = 0;
            }
        }

        private void SetJpeg720x540CameraIntrinsics()
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsicsDeprecated(out XRCameraIntrinsics jpegCameraIntrinsics))
            {
                ImageConverter.ConvertCameraIntrinsics
                (
                    jpegCameraIntrinsics,
                    _currentDeprecatedFrameDataDeprecated.Jpeg720x540ImageResolution,
                    _currentDeprecatedFrameDataDeprecated.Jpeg720x540CameraIntrinsicsData
                );
                _currentDeprecatedFrameDataDeprecated.Jpeg720x540CameraIntrinsicsLength = DataFormatConstants.FlatMatrix3x3Length;
            }
            else
            {
                _currentDeprecatedFrameDataDeprecated.Jpeg720x540CameraIntrinsicsLength = 0;
            }
        }

        private void SetPlatformDepthCameraIntrinsics(Vector2Int resolution)
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsicsDeprecated(out XRCameraIntrinsics cameraIntrinsics))
            {
                // TODO (AR-16968): Collate calls to TryGetCameraIntrinsicsDeprecated
                ImageConverter.ConvertCameraIntrinsics
                (
                    cameraIntrinsics,
                    resolution,
                    _currentDeprecatedFrameDataDeprecated.PlatformDepthCameraIntrinsicsData
                );

                _currentDeprecatedFrameDataDeprecated.PlatformDepthCameraIntrinsicsLength = DataFormatConstants.FlatMatrix3x3Length;
            }
            else
            {
                _currentDeprecatedFrameDataDeprecated.PlatformDepthCameraIntrinsicsLength = 0;
            }
        }

        private void SetGpsLocation()
        {
            if (_platformDataAcquirer.TryGetGpsLocation(out GpsLocationCStruct gps))
            {
                _currentDeprecatedFrameDataDeprecated.SetGpsData(gps);
                _currentDeprecatedFrameDataDeprecated.GpsLocationLength = (UInt32)UnsafeUtility.SizeOf<GpsLocationCStruct>();
            }
            else
            {
                _currentDeprecatedFrameDataDeprecated.GpsLocationLength = 0;
            }
        }

        private void SetCompass()
        {
            if (_platformDataAcquirer.TryGetCompass(out CompassDataCStruct compass))
            {
                _currentDeprecatedFrameDataDeprecated.SetCompassData(compass);
                _currentDeprecatedFrameDataDeprecated.CompassDataLength = (UInt32)UnsafeUtility.SizeOf<CompassDataCStruct>();
            }
            else
            {
                _currentDeprecatedFrameDataDeprecated.CompassDataLength = 0;
            }
        }

        private void SetCameraPose()
        {
            if (_platformDataAcquirer.TryGetCameraPose(out Matrix4x4 cameraToLocal))
            {
                _currentDeprecatedFrameDataDeprecated.SetPoseData(cameraToLocal.FromUnityToArdk().ToColumnMajorArray());
                _currentDeprecatedFrameDataDeprecated.CameraPoseLength = DataFormatConstants.FlatMatrix4x4Length;
            }
            else
            {
                _currentDeprecatedFrameDataDeprecated.CameraPoseLength = 0;
            }
        }

        // Note, all the data lengths needs to be invalidated after sending the frame to SAH,
        // otherwise the same data will be assumed being valid and used again.
        private void InvalidateFrameData()
        {
            _currentDeprecatedFrameDataDeprecated.CompassDataLength = 0;
            _currentDeprecatedFrameDataDeprecated.CameraPoseLength = 0;
            _currentDeprecatedFrameDataDeprecated.GpsLocationLength = 0;
            _currentDeprecatedFrameDataDeprecated.Jpeg720x540CameraIntrinsicsLength = 0;
            _currentDeprecatedFrameDataDeprecated.PlatformDepthDataLength = 0;
            _currentDeprecatedFrameDataDeprecated.Rgba256x144CameraIntrinsicsLength = 0;
            _currentDeprecatedFrameDataDeprecated.CpuJpeg720x540ImageDataLength = 0;
            _currentDeprecatedFrameDataDeprecated.CpuRgba256x144ImageDataLength = 0;
            _currentDeprecatedFrameDataDeprecated.JpegFullResCameraIntrinsicsLength = 0;
            _currentDeprecatedFrameDataDeprecated.CpuJpegFullResImageHeight = 0;
            _currentDeprecatedFrameDataDeprecated.CpuJpegFullResImageWidth = 0;
            _currentDeprecatedFrameDataDeprecated.CpuJpegFullResImageDataLength = 0;
        }

        private void OnBeforeRender()
        {
            ProfilerUtility.EventInstance("Rendering", "FrameUpdate", new CustomProcessingOptions
            {
                ProcessingType = CustomProcessingOptions.Type.TIME_UNTIL_NEXT
            });
        }

        public void DeprecatedSendUpdatedFrameData()
        {
            if (!_platformDataAcquirer.TryToBeReady())
            {
                return;
            }

            const string getDataFormatUpdatesForNewFrameEventName = "PAM_GetDataFormatUpdatesForNewFrame";
            ProfilerUtility.EventBegin(TraceCategory, getDataFormatUpdatesForNewFrameEventName);

            _api.Lightship_ARDK_Unity_PAM_GetDataFormatUpdatesForNewFrame
            (
                _nativeHandle,
                _addedDataFormats,
                out var addedDataFormatsSize,
                _readyDataFormats,
                out _readyDataFormatsSize,
                _removedDataFormats,
                out var removedDataFormatsSize
            );

            for (int i = 0; i < addedDataFormatsSize; ++i)
            {
                _platformDataAcquirer.OnFormatAdded(_addedDataFormats[i]);
            }

            for (int i = 0; i < removedDataFormatsSize; ++i)
            {
                _platformDataAcquirer.OnFormatRemoved(_removedDataFormats[i]);
            }

            ProfilerUtility.EventEnd(TraceCategory, getDataFormatUpdatesForNewFrameEventName);

            if (_readyDataFormatsSize == 0)
            {
                return;
            }

            // Profile the avg cost across requested and ready formats
            const string setCurrentFrameDataDeprecatedEventName = "SetCurrentFrameDataDeprecated";
            ProfilerUtility.EventBegin(TraceCategory, setCurrentFrameDataDeprecatedEventName);

            for (int i = 0; i < _readyDataFormatsSize; i++)
            {
                var dataFormat = _readyDataFormats[i];

                switch (dataFormat)
                {
                    case DataFormat.kCpuRgba_256_144_Uint8:
                        SetRgba256x144CameraIntrinsics();

                        // Not going to bother checking if the resolution is large enough here, because presumably
                        // any AR camera image is going to be larger than 256 x 144
                        _texturesSetter.SetRgba256x144Image();
                        break;

                    case DataFormat.kCpuRgb_256_256_Uint8:
                        SetRgb256x256CameraIntrinsics();

                        _texturesSetter.SetRgb256x256Image();
                        break;

                    case DataFormat.kJpeg_720_540_Uint8:
                        SetJpeg720x540CameraIntrinsics();

                        _texturesSetter.SetJpeg720x540Image();
                        break;

                    case DataFormat.kGpsLocation:
                        SetGpsLocation();
                        break;

                    case DataFormat.kCompass:
                        SetCompass();
                        break;

                    case DataFormat.kJpeg_full_res_Uint8:
                        _texturesSetter.SetJpegFullResImage();
                        break;

                    case DataFormat.kPlatform_depth:
                        _texturesSetter.SetPlatformDepthBuffer();

                        // Need to acquire platform depth buffer first, so that resolution is known
                        if (_currentDeprecatedFrameDataDeprecated.PlatformDepthDataLength > 0)
                        {
                            SetPlatformDepthCameraIntrinsics(_currentDeprecatedFrameDataDeprecated.PlatformDepthResolution);
                        }
                        break;
                }
            }

            SetCameraPose();
            _currentDeprecatedFrameDataDeprecated.FrameId = _frameCounter++;
            _currentDeprecatedFrameDataDeprecated.TimestampMs = (ulong)_texturesSetter.GetCurrentTimestampMs();
            _currentDeprecatedFrameDataDeprecated.TrackingState = _platformDataAcquirer.GetTrackingState().FromUnityToArdk();
            _currentDeprecatedFrameDataDeprecated.ScreenOrientation = _platformDataAcquirer.GetScreenOrientation().FromUnityToArdk();
            _currentDeprecatedFrameDataDeprecated.CameraImageResolution = _platformDataAcquirer.TryGetCameraIntrinsicsDeprecated(out var intrinsics)
                ? intrinsics.resolution
                : Vector2Int.zero;

            ProfilerUtility.EventEnd(TraceCategory, setCurrentFrameDataDeprecatedEventName);

            // Deprecated PAM codepath
            // SAH handles checking if all required data is in this frame
            const string nativePamOnFrameDeprecatedEventName = "Lightship_ARDK_Unity_PAM_OnFrame_Deprecated";
            ProfilerUtility.EventBegin(TraceCategory, nativePamOnFrameDeprecatedEventName);
            {
                unsafe
                {
                    fixed (void* pointerToFrameStruct = &_currentDeprecatedFrameDataDeprecated._frameCStruct)
                    {
                        _api.Lightship_ARDK_Unity_PAM_OnFrame_Deprecated(_nativeHandle, (IntPtr)pointerToFrameStruct);
                    }
                }
            }
            ProfilerUtility.EventEnd(TraceCategory, nativePamOnFrameDeprecatedEventName);

            if (SentData != null)
            {
                const string sentDataEventName = "SentData";
                ProfilerUtility.EventBegin(TraceCategory, sentDataEventName);

                DataFormat[] sentDataFormats = CollateSentDataFormats();

                if (sentDataFormats.Length > 0)
                {
                    Log.Debug
                        (
                            $"PAM sending data: {string.Join(',', sentDataFormats)}, " +
                            $"orientation: {_platformDataAcquirer.GetScreenOrientation()}, " +
                            $"time: ulong={(ulong)_texturesSetter.GetCurrentTimestampMs()}"
                        );
                }

                SentData?.Invoke(new PamEventArgs(sentDataFormats));
                ProfilerUtility.EventEnd(TraceCategory, sentDataEventName);
            }

            // Invalidate the data lengths so that SAH won't pick them up in the next frame.
            InvalidateFrameData();
            _texturesSetter.InvalidateCachedTextures();
        }

        private void SendUpdatedFrameData()
        {
            if (!_platformDataAcquirer.TryToBeReady())
            {
                return;
            }

            const string setCurrentFrameDataEventName = "SetCurrentFrameData";
            ProfilerUtility.EventBegin(TraceCategory, setCurrentFrameDataEventName);

            FrameDataCStruct frameData = new FrameDataCStruct();
            for (int i = 0; i < _readyDataFormatsSize; i++)
            {
                switch (_readyDataFormats[i])
                {
                    case DataFormat.kCompass:
                    {
                        _platformDataAcquirer.TryGetCompass(out frameData.CompassData);
                        break;
                    }

                    case DataFormat.kGpsLocation:
                    {
                        _platformDataAcquirer.TryGetGpsLocation(out frameData.GpsLocation);
                        break;
                    }
                }
            }

            // Pose
            _platformDataAcquirer.TryGetCameraPose(out Matrix4x4 cameraToLocal);
            frameData.CameraPose.SetTransform(cameraToLocal.FromUnityToArdk());

            frameData.FrameId = _frameCounter++;
            frameData.CameraTimestampMs = (ulong)_texturesSetter.GetCurrentTimestampMs();
            frameData.TrackingState = _platformDataAcquirer.GetTrackingState().FromUnityToArdk();
            frameData.ScreenOrientation = _platformDataAcquirer.GetScreenOrientation().FromUnityToArdk();

            if (_platformDataAcquirer.TryGetCameraIntrinsicsDeprecated(out XRCameraIntrinsics cameraIntrinsics))
            {
                frameData.CameraIntrinsics.SetIntrinsics(cameraIntrinsics.focalLength, cameraIntrinsics.principalPoint);
            }

            // Note, cpuImage needs to be disposed
            // We only need to check valid because our tests create invalid XRCpuImage
            if (_platformDataAcquirer.TryGetCpuImageDeprecated(out XRCpuImage cpuImage) && cpuImage.valid)
            {
                unsafe
                {
                    frameData.CameraImagePlane0DataPtr = (IntPtr)cpuImage.GetPlane(0).data.GetUnsafeReadOnlyPtr();
                    frameData.CameraImagePlane1DataPtr = (cpuImage.planeCount > 1)?
                        (IntPtr)cpuImage.GetPlane(1).data.GetUnsafeReadOnlyPtr() : IntPtr.Zero;
                    frameData.CameraImagePlane2DataPtr = (cpuImage.planeCount > 2)?
                        (IntPtr)cpuImage.GetPlane(2).data.GetUnsafeReadOnlyPtr() : IntPtr.Zero;
                }

                frameData.CameraImageFormat = cpuImage.format.FromUnityToArdk();
                frameData.CameraImageWidth = (uint)cpuImage.width;
                frameData.CameraImageHeight = (uint)cpuImage.height;
            }

            ProfilerUtility.EventEnd(TraceCategory, setCurrentFrameDataEventName);

            // New WIP PAM codepath
            const string nativePamOnFrameEventName = "Lightship_ARDK_Unity_PAM_OnFrame";
            ProfilerUtility.EventBegin(TraceCategory, nativePamOnFrameEventName);
            {
                unsafe
                {
                    void* nonMoveablePtr = &frameData;
                    _api.Lightship_ARDK_Unity_PAM_OnFrame(_nativeHandle, (IntPtr)nonMoveablePtr);
                }
            }
            ProfilerUtility.EventEnd(TraceCategory, nativePamOnFrameEventName);

            // Cleanup
            cpuImage.Dispose();
        }

        // Returns only the data formats that were actually sent to SAH
        private DataFormat[] CollateSentDataFormats()
        {
            List<DataFormat> sentDataFormats = new();
            foreach (var dataFormat in _readyDataFormats)
            {
                switch (dataFormat)
                {
                    case DataFormat.kCpuRgba_256_144_Uint8:
                        if (_currentDeprecatedFrameDataDeprecated._frameCStruct.CpuRgba256x144ImageDataLength > 0)
                        {
                            sentDataFormats.Add(DataFormat.kCpuRgba_256_144_Uint8);
                        }

                        break;

                    case DataFormat.kCpuRgb_256_256_Uint8:
                        if (_currentDeprecatedFrameDataDeprecated._frameCStruct.CpuRgb256x256ImageDataLength > 0)
                        {
                            sentDataFormats.Add(DataFormat.kCpuRgb_256_256_Uint8);
                        }

                        break;

                    case DataFormat.kJpeg_720_540_Uint8:
                        if (_currentDeprecatedFrameDataDeprecated._frameCStruct.CpuJpeg720x540ImageDataLength > 0)
                        {
                            sentDataFormats.Add(DataFormat.kJpeg_720_540_Uint8);
                        }

                        break;

                    case DataFormat.kGpsLocation:
                        if (_currentDeprecatedFrameDataDeprecated._frameCStruct.GpsLocationLength > 0)
                        {
                            sentDataFormats.Add(DataFormat.kGpsLocation);
                        }

                        break;

                    case DataFormat.kCompass:
                        if (_currentDeprecatedFrameDataDeprecated._frameCStruct.CompassDataLength > 0)
                        {
                            sentDataFormats.Add(DataFormat.kCompass);
                        }

                        break;


                    case DataFormat.kJpeg_full_res_Uint8:
                        if (_currentDeprecatedFrameDataDeprecated._frameCStruct.CpuJpegFullResImageDataLength > 0)
                        {
                            sentDataFormats.Add(DataFormat.kJpeg_full_res_Uint8);
                        }

                        break;

                    case DataFormat.kPlatform_depth:
                        if (_currentDeprecatedFrameDataDeprecated._frameCStruct.PlatformDepthDataLength > 0)
                        {
                            sentDataFormats.Add(DataFormat.kPlatform_depth);
                        }

                        break;
                }
            }

            return sentDataFormats.ToArray();
        }
    }

    internal class PamEventArgs : EventArgs
    {
        public readonly DataFormat[] FormatsSent;

        public PamEventArgs(params DataFormat[] formatsSent)
        {
            FormatsSent = formatsSent;
        }
    }
}
