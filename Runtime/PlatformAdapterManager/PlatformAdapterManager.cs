// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Occlusion;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Profiling;
using Matrix4x4 = UnityEngine.Matrix4x4;

namespace Niantic.Lightship.AR.PAM
{
    internal class PlatformAdapterManager : IDisposable
    {
        public Action<PamEventArgs> SentData;
        private readonly FrameData _currentFrameData;

        private readonly IApi _api;
        private readonly PlatformDataAcquirer _platformDataAcquirer;

        private IntPtr _nativeHandle;
        private NativeArray<DataFormat> _addedDataFormats;
        private NativeArray<DataFormat> _readyDataFormats;
        private NativeArray<DataFormat> _removedDataFormats;

        private uint _frameCounter;
        private bool _alreadyDisposed;

        internal enum ImageProcessingMode
        {
            CPU,
            GPU
        }

        private readonly AbstractTexturesSetter _abstractTexturesSetter;

        private const string TraceCategory = "PlatformAdapterManager";

        // TODO [AR-16350]: Once linked ticket to use GPU path on device is complete, won't need imageProcessingMode param
        public static PlatformAdapterManager Create<TApi, TXRDataAcquirer>
        (
            IntPtr contextHandle,
            ImageProcessingMode imageProcessingMode,
            bool isLidarDepthEnabled,
            bool trySendOnUpdate
            )
            where TApi : IApi, new()
            where TXRDataAcquirer : PlatformDataAcquirer, new()
        {
            return new PlatformAdapterManager(new TApi(), new TXRDataAcquirer(), contextHandle, imageProcessingMode, isLidarDepthEnabled, trySendOnUpdate);
        }

        public PlatformAdapterManager
        (
            IApi api,
            PlatformDataAcquirer platformDataAcquirer,
            IntPtr unityContext,
            ImageProcessingMode imageProcessingMode,
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

            _currentFrameData = new FrameData(imageProcessingMode);
            if (imageProcessingMode == ImageProcessingMode.CPU)
            {
                _abstractTexturesSetter =
                    new CpuTexturesSetter(_platformDataAcquirer, _currentFrameData);
            }
            else
            {
                _abstractTexturesSetter =
                    new GpuTexturesSetter(_platformDataAcquirer, _currentFrameData);
            }

            Log.Info
            (
                $"{nameof(PlatformAdapterManager)}>{_api.GetType()}, <{_platformDataAcquirer.GetType()}> was created " +
                $"with nativeHandle ({_nativeHandle})"
            );

            if (trySendOnUpdate)
            {
                MonoBehaviourEventDispatcher.Updating.AddListener(SendUpdatedFrameData);
            }

            Application.onBeforeRender += OnBeforeRender;
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
            _currentFrameData.Dispose();

            if (_abstractTexturesSetter != null)
                _abstractTexturesSetter.Dispose();

            MonoBehaviourEventDispatcher.Updating.RemoveListener(SendUpdatedFrameData);
            Application.onBeforeRender -= OnBeforeRender;
        }

        private void SetRgba256x144CameraIntrinsics()
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
            {
                ImageConverter.ConvertCameraIntrinsics
                (
                    cameraIntrinsics,
                    _currentFrameData.Rgba256x144ImageResolution,
                    _currentFrameData.Rgba256x144CameraIntrinsicsData
                );

                _currentFrameData.Rgba256x144CameraIntrinsicsLength = DataFormatConstants.FlatMatrix3x3Length;
            }
            else
            {
                _currentFrameData.Rgba256x144CameraIntrinsicsLength = 0;
            }
        }

        private void SetRgb256x256CameraIntrinsics()
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
            {
                ImageConverter.ConvertCameraIntrinsics
                (
                    cameraIntrinsics,
                    _currentFrameData.Rgb256x256ImageResolution,
                    _currentFrameData.Rgb256x256CameraIntrinsicsData
                );

                _currentFrameData.Rgb256x256CameraIntrinsicsLength = DataFormatConstants.FlatMatrix3x3Length;
            }
            else
            {
                _currentFrameData.Rgb256x256CameraIntrinsicsLength = 0;
            }
        }

        private void SetJpeg720x540CameraIntrinsics()
        {
            if (_platformDataAcquirer.TryGetCameraIntrinsics(out XRCameraIntrinsics jpegCameraIntrinsics))
            {
                ImageConverter.ConvertCameraIntrinsics
                (
                    jpegCameraIntrinsics,
                    _currentFrameData.Jpeg720x540ImageResolution,
                    _currentFrameData.Jpeg720x540CameraIntrinsicsData
                );
                _currentFrameData.Jpeg720x540CameraIntrinsicsLength = DataFormatConstants.FlatMatrix3x3Length;
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
                ImageConverter.ConvertCameraIntrinsics
                (
                    cameraIntrinsics,
                    resolution,
                    _currentFrameData.PlatformDepthCameraIntrinsicsData
                );

                _currentFrameData.PlatformDepthCameraIntrinsicsLength = DataFormatConstants.FlatMatrix3x3Length;
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

        private void SetCameraPose()
        {
            if (_platformDataAcquirer.TryGetCameraPose(out Matrix4x4 cameraToLocal))
            {
                _currentFrameData.SetPoseData(cameraToLocal.FromUnityToArdk().ToColumnMajorArray());
                _currentFrameData.CameraPoseLength = DataFormatConstants.FlatMatrix4x4Length;
            }
            else
            {
                _currentFrameData.CameraPoseLength = 0;
            }
        }

        // Note, all the data lengths needs to be invalidated after sending the frame to SAH,
        // otherwise the same data will be assumed being valid and used again.
        private void InvalidateFrameData()
        {
            _currentFrameData.CompassDataLength = 0;
            _currentFrameData.CameraPoseLength = 0;
            _currentFrameData.GpsLocationLength = 0;
            _currentFrameData.Jpeg720x540CameraIntrinsicsLength = 0;
            _currentFrameData.PlatformDepthDataLength = 0;
            _currentFrameData.Rgba256x144CameraIntrinsicsLength = 0;
            _currentFrameData.CpuJpeg720x540ImageDataLength = 0;
            _currentFrameData.CpuRgba256x144ImageDataLength = 0;
            _currentFrameData.JpegFullResCameraIntrinsicsLength = 0;
            _currentFrameData.CpuJpegFullResImageHeight = 0;
            _currentFrameData.CpuJpegFullResImageWidth = 0;
            _currentFrameData.CpuJpegFullResImageDataLength = 0;
        }

        public void SendUpdatedFrameData()
        {
            const string name = "SendUpdatedFrameData";
            if (!_platformDataAcquirer.TryToBeReady())
                return;

            _api.Lightship_ARDK_Unity_PAM_GetDataFormatUpdatesForNewFrame
            (
                _nativeHandle,
                _addedDataFormats,
                out var addedDataFormatsSize,
                _readyDataFormats,
                out var readyDataFormatsSize,
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

            if (readyDataFormatsSize == 0)
                return;

            ProfilerUtility.EventBegin(TraceCategory, name, "formatsize", readyDataFormatsSize.ToString());

            for (var i = 0; i < readyDataFormatsSize; i++)
            {
                var dataFormat = _readyDataFormats[i];

                switch (dataFormat)
                {
                    case DataFormat.kCpuRgba_256_144_Uint8:
                        SetRgba256x144CameraIntrinsics();

                        // Not going to bother checking if the resolution is large enough here, because presumably
                        // any AR camera image is going to be larger than 256 x 144
                        _abstractTexturesSetter.SetRgba256x144Image();
                        break;

                    case DataFormat.kCpuRgb_256_256_Uint8:
                        SetRgb256x256CameraIntrinsics();

                        _abstractTexturesSetter.SetRgb256x256Image();
                        break;

                    case DataFormat.kJpeg_720_540_Uint8:
                        SetJpeg720x540CameraIntrinsics();

                        _abstractTexturesSetter.SetJpeg720x540Image();
                        break;

                    case DataFormat.kGpsLocation:
                        SetGpsLocation();
                        break;

                    case DataFormat.kCompass:
                        SetCompass();
                        break;


                    case DataFormat.kJpeg_full_res_Uint8:
                        _abstractTexturesSetter.SetJpegFullResImage();
                        break;

                    case DataFormat.kPlatform_depth:
                        _abstractTexturesSetter.SetPlatformDepthBuffer();

                        // Need to acquire platform depth buffer first, so that resolution is known
                        if (_currentFrameData.PlatformDepthDataLength > 0)
                            SetPlatformDepthCameraIntrinsics(_currentFrameData.PlatformDepthResolution);

                        break;
                }
            }

            ProfilerUtility.EventStep(TraceCategory, name, "SetCommonData");

            SetCameraPose();

            _currentFrameData.TimestampMs = (ulong)_abstractTexturesSetter.GetCurrentTimestampMs();

            _currentFrameData.ScreenOrientation = _platformDataAcquirer.GetScreenOrientation().FromUnityToArdk();

            _currentFrameData.TrackingState = _platformDataAcquirer.GetTrackingState().FromUnityToArdk();
            _currentFrameData.FrameId = _frameCounter++;
            _currentFrameData.CameraImageResolution = _platformDataAcquirer.TryGetCameraIntrinsics(out var intrinsics)
                ? intrinsics.resolution
                : Vector2Int.zero;

            // SAH handles checking if all required data is in this frame
            ProfilerUtility.EventStep(TraceCategory, name, "SendData");
            unsafe
            {
                fixed (void* pointerToFrameStruct = &_currentFrameData._frameCStruct)
                {
                    _api.Lightship_ARDK_Unity_PAM_OnFrame(_nativeHandle, (IntPtr)pointerToFrameStruct);
                }
            }

            ProfilerUtility.EventEnd(TraceCategory, name);

            if (SentData != null)
            {
                DataFormat[] sentDataFormats = CollateSentDataFormats();

                if (sentDataFormats.Length > 0)
                {
                    Log.Debug
                        (
                            $"PAM sending data: {string.Join(',', sentDataFormats)}, " +
                            $"orientation: {_platformDataAcquirer.GetScreenOrientation()}, " +
                            $"time: ulong={(ulong)_abstractTexturesSetter.GetCurrentTimestampMs()}"
                        );
                }

                SentData?.Invoke(new PamEventArgs(sentDataFormats));
            }

            // Invalidate the data lengths so that SAH won't pick them up in the next frame.
            InvalidateFrameData();
            _abstractTexturesSetter.InvalidateCachedTextures();
        }

        private void OnBeforeRender()
        {
            ProfilerUtility.EventInstance("Rendering", "FrameUpdate", new CustomProcessingOptions
            {
                ProcessingType = CustomProcessingOptions.Type.TIME_UNTIL_NEXT
            });
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
                        if (_currentFrameData._frameCStruct.CpuRgba256x144ImageDataLength > 0)
                            sentDataFormats.Add(DataFormat.kCpuRgba_256_144_Uint8);
                        break;

                    case DataFormat.kCpuRgb_256_256_Uint8:
                        if (_currentFrameData._frameCStruct.CpuRgb256x256ImageDataLength > 0)
                            sentDataFormats.Add(DataFormat.kCpuRgb_256_256_Uint8);
                        break;

                    case DataFormat.kJpeg_720_540_Uint8:
                        if (_currentFrameData._frameCStruct.CpuJpeg720x540ImageDataLength > 0)
                            sentDataFormats.Add(DataFormat.kJpeg_720_540_Uint8);
                        break;

                    case DataFormat.kGpsLocation:
                        if (_currentFrameData._frameCStruct.GpsLocationLength > 0)
                            sentDataFormats.Add(DataFormat.kGpsLocation);
                        break;

                    case DataFormat.kCompass:
                        if (_currentFrameData._frameCStruct.CompassDataLength > 0)
                            sentDataFormats.Add(DataFormat.kCompass);
                        break;


                    case DataFormat.kJpeg_full_res_Uint8:
                        if (_currentFrameData._frameCStruct.CpuJpegFullResImageDataLength > 0)
                            sentDataFormats.Add(DataFormat.kJpeg_full_res_Uint8);
                        break;

                    case DataFormat.kPlatform_depth:
                        if (_currentFrameData._frameCStruct.PlatformDepthDataLength > 0)
                            sentDataFormats.Add(DataFormat.kPlatform_depth);
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
