// #define NIANTIC_ARDK_USE_FAST_LIGHTWEIGHT_PAM
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
        private uint _lastSentFrameId;
        private readonly DeprecatedFrameData _currentDeprecatedFrameDataDeprecated;

        private readonly IApi _api;
        private readonly PlatformDataAcquirer _platformDataAcquirer;
#if NIANTIC_ARDK_USE_FAST_LIGHTWEIGHT_PAM
        public static bool UseNewDataPipeline = true;
#else
        public static bool UseNewDataPipeline = false;
#endif
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

            _frameCounter = 0;
            _lastSentFrameId = UInt32.MaxValue;
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
                AddUpdateListeners();
            }
        }

        internal void AddUpdateListeners()
        {
            MonoBehaviourEventDispatcher.Updating.RemoveListener(SendUpdatedFrameData);
            MonoBehaviourEventDispatcher.Updating.RemoveListener(DeprecatedSendUpdatedFrameData);

            if (UseNewDataPipeline)
            {
                MonoBehaviourEventDispatcher.Updating.AddListener(SendUpdatedFrameData);
            }
            else
            {
                MonoBehaviourEventDispatcher.Updating.AddListener(DeprecatedSendUpdatedFrameData);
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

            MonoBehaviourEventDispatcher.Updating.RemoveListener(SendUpdatedFrameData);
            MonoBehaviourEventDispatcher.Updating.RemoveListener(DeprecatedSendUpdatedFrameData);

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

            const string traceMethodName = "SendUpdatedFrameData_OLD";
            ProfilerUtility.EventBegin(TraceCategory, traceMethodName);

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

            if (_readyDataFormatsSize == 0)
            {
                ProfilerUtility.EventEnd(TraceCategory, traceMethodName);
                return;
            }

            // Profile group by ready formats
            string traceReadyFormats = "_";
            for (int i = 0; i < _readyDataFormatsSize; i++)
            {
                traceReadyFormats += DataFormatNames.GetName(_readyDataFormats[i]) + "_";
            }
            ProfilerUtility.EventBegin(TraceCategory, traceMethodName + traceReadyFormats);

            for (int i = 0; i < _readyDataFormatsSize; i++)
            {
                var dataFormat = _readyDataFormats[i];

                ProfilerUtility.EventBegin(TraceCategory, dataFormat.ToString());
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

                ProfilerUtility.EventEnd(TraceCategory, dataFormat.ToString());
            }

            SetCameraPose();
            _currentDeprecatedFrameDataDeprecated.FrameId = _frameCounter++;
            _currentDeprecatedFrameDataDeprecated.TimestampMs = (ulong)_texturesSetter.GetCurrentTimestampMs();
            _currentDeprecatedFrameDataDeprecated.TrackingState = _platformDataAcquirer.GetTrackingState().FromUnityToArdk();
            _currentDeprecatedFrameDataDeprecated.ScreenOrientation = _platformDataAcquirer.GetScreenOrientation().FromUnityToArdk();
            _currentDeprecatedFrameDataDeprecated.CameraImageResolution = _platformDataAcquirer.TryGetCameraIntrinsicsDeprecated(out var intrinsics)
                ? intrinsics.resolution
                : Vector2Int.zero;

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
            ProfilerUtility.EventEnd(TraceCategory, traceMethodName + traceReadyFormats);

            if (SentData != null)
            {
                const string sentDataEventName = "SentData";
                ProfilerUtility.EventBegin(TraceCategory, sentDataEventName);

                DataFormat[] sentDataFormats = DeprecatedCollateSentDataFormats();

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

            ProfilerUtility.EventEnd(TraceCategory, traceMethodName);
        }

        public void SendUpdatedFrameData()
        {
            if (!_platformDataAcquirer.TryToBeReady())
            {
                return;
            }

            const string traceMethodName = "SendUpdatedFrameData";
            ProfilerUtility.EventBegin(TraceCategory, traceMethodName);

            _api.Lightship_ARDK_Unity_PAM_GetDataFormatsReadyForNewFrame
            (
                _nativeHandle,
                _readyDataFormats,
                out _readyDataFormatsSize
            );

            if (_readyDataFormatsSize == 0)
            {
                ProfilerUtility.EventEnd(TraceCategory, traceMethodName);
                return;
            }

            // Profile group by ready formats
            string traceReadyFormats = "_";
            for (int i = 0; i < _readyDataFormatsSize; i++)
            {
                traceReadyFormats += DataFormatNames.GetName(_readyDataFormats[i]) + "_";
            }

            ProfilerUtility.EventBegin(TraceCategory, traceMethodName + traceReadyFormats);

            FrameDataCStruct frameData = new FrameDataCStruct();

            var getCameraImage = false;
            var getDepthImage = false;
            for (int i = 0; i < _readyDataFormatsSize; i++)
            {
                switch (_readyDataFormats[i])
                {
                    case DataFormat.kCompass:
                        _platformDataAcquirer.TryGetCompass(out frameData.CompassData);
                        break;

                    case DataFormat.kGpsLocation:
                        _platformDataAcquirer.TryGetGpsLocation(out frameData.GpsLocation);
                        break;

                    case DataFormat.kCpuRgba_256_144_Uint8:
                    case DataFormat.kCpuRgb_256_256_Uint8:
                    case DataFormat.kCpuRgb_384_216_Uint8:
                    case DataFormat.kJpeg_720_540_Uint8:
                    case DataFormat.kJpeg_full_res_Uint8:
                        getCameraImage = true;
                        break;

                    case DataFormat.kPlatform_depth:
                        getDepthImage = true;
                        break;

                    case DataFormat.kPose:
                    case DataFormat.kDeviceOrientation:
                    case DataFormat.kTrackingState:
                        // Always sent, whether it was requested or not, because the cost is negligible
                        break;

                    default:
                        Log.Error($"Native layer requested a format {_readyDataFormats[i]} that is not handled by the PAM.");
                        break;
                }
            }

            // Pose
            _platformDataAcquirer.TryGetCameraPose(out Matrix4x4 cameraToLocal);
            var cameraPose = cameraToLocal.FromUnityToArdk();
            frameData.CameraPose.SetTransform(cameraPose);

            frameData.FrameId = _frameCounter++;
            frameData.CameraTimestampMs = _platformDataAcquirer.TryGetCameraTimestampMs(out var timestampMs) ? (ulong)timestampMs : 0;
            frameData.TrackingState = _platformDataAcquirer.GetTrackingState().FromUnityToArdk();
            frameData.ScreenOrientation = _platformDataAcquirer.GetScreenOrientation().FromUnityToArdk();

            if (getCameraImage && _platformDataAcquirer.TryGetCpuImage(out var cpuCamera))
            {
                _platformDataAcquirer.TryGetCameraIntrinsicsCStruct(out frameData.CameraIntrinsics);
                frameData.CameraImagePlane0.SetImagePlane(cpuCamera.Planes[0]);
                frameData.CameraImagePlane1.SetImagePlane(cpuCamera.Planes[1]);
                frameData.CameraImagePlane2.SetImagePlane(cpuCamera.Planes[2]);
                frameData.CameraImageFormat = cpuCamera.Format;
                frameData.CameraImageWidth = cpuCamera.Width;
                frameData.CameraImageHeight = cpuCamera.Height;
            }

            if (getDepthImage && _platformDataAcquirer.TryGetDepthCpuImage(out var cpuDepth, out var cpuDepthConfidence))
            {
                frameData.DepthDataPtr = cpuDepth.Planes[0].DataPtr;
                frameData.DepthDataWidth = cpuDepth.Width;
                frameData.DepthDataHeight = cpuDepth.Height;
                if (cpuDepthConfidence.Planes.Length > 0)
                {
                    frameData.DepthConfidencesDataPtr = cpuDepthConfidence.Planes[0].DataPtr;
                    frameData.DepthAndConfidenceDataLength = cpuDepth.Width * cpuDepth.Height;
                }

                // TODO [ARDK-3966]: Move scaling calculation to C++
                _platformDataAcquirer.TryGetDepthCameraIntrinsicsCStruct
                (
                    out frameData.DepthCameraIntrinsics,
                    new Vector2Int((int)cpuDepth.Width, (int)cpuDepth.Height)
                );
            }

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
            ProfilerUtility.EventEnd(TraceCategory, traceMethodName + traceReadyFormats);

            if (SentData != null)
            {
                _api.Lightship_ARDK_Core_SAH_GetDispatchedFormatsToModules(_nativeHandle,
                    out var dispatchedFrameId,
                    out var dispatchedToModules,
                    out var dispatchedDataFormats);
                if (dispatchedFrameId != _lastSentFrameId)
                {
                    _lastSentFrameId = dispatchedFrameId;
                    SentData?.Invoke(new PamEventArgs(dispatchedDataFormats));
                }
            }

            ProfilerUtility.EventEnd(TraceCategory, traceMethodName);
        }

        // Returns only the data formats that were actually sent to SAH
        private DataFormat[] DeprecatedCollateSentDataFormats()
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

        public PamEventArgs(ulong dispatchedFormats)
        {
            List<DataFormat> formatsSent = new List<DataFormat>();
            foreach (var enumItem in Enum.GetValues(typeof(DataFormat)))
            {
                int enumVal = Convert.ToInt32(enumItem);
                if ((dispatchedFormats & (1UL << enumVal)) != 0)
                {
                    formatsSent.Add((DataFormat)enumItem);
                }
            }
            FormatsSent = formatsSent.ToArray();
        }
    }
}
