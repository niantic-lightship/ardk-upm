// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.Collections;
using UnityEngine;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Profiling;
using Matrix4x4 = UnityEngine.Matrix4x4;

namespace Niantic.Lightship.AR.PAM
{
    internal class PlatformAdapterManager : IDisposable
    {
        public Action<PamEventArgs> SentData;
        private uint _lastSentFrameId;

        private readonly IApi _api;
        private readonly PlatformDataAcquirer _platformDataAcquirer;

        private IntPtr _nativeHandle;
        private NativeArray<DataFormat> _addedDataFormats;
        private NativeArray<DataFormat> _readyDataFormats;
        private NativeArray<DataFormat> _removedDataFormats;
        private int _readyDataFormatsSize;

        private uint _frameCounter;
        private bool _alreadyDisposed;

        private const string TraceCategory = "PlatformAdapterManager";

        public static PlatformAdapterManager Create<TApi, TXRDataAcquirer>
        (
            IntPtr contextHandle,
            bool isLidarDepthEnabled,
            bool trySendOnUpdate
        )
            where TApi : IApi, new()
            where TXRDataAcquirer : PlatformDataAcquirer, new()
        {
            return new PlatformAdapterManager(new TApi(), new TXRDataAcquirer(), contextHandle, isLidarDepthEnabled,
                trySendOnUpdate);
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
            _nativeHandle = _api.ARDK_SAH_Create(unityContext, isLidarDepthEnabled);

            var numFormats = Enum.GetValues(typeof(DataFormat)).Length;
            _addedDataFormats = new NativeArray<DataFormat>(numFormats, Allocator.Persistent);
            _readyDataFormats = new NativeArray<DataFormat>(numFormats, Allocator.Persistent);
            _removedDataFormats = new NativeArray<DataFormat>(numFormats, Allocator.Persistent);

            _frameCounter = 0;
            _lastSentFrameId = UInt32.MaxValue;

            Log.Info
            (
                $"{nameof(PlatformAdapterManager)}>{_api.GetType()}, <{_platformDataAcquirer.GetType()}> was created " +
                $"with nativeHandle ({_nativeHandle})"
            );

            Application.onBeforeRender += OnBeforeRender;
            if (trySendOnUpdate)
            {
                MonoBehaviourEventDispatcher.Updating.AddListener(SendUpdatedFrameData);
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
                _api.ARDK_SAH_Release(_nativeHandle);
                _nativeHandle = IntPtr.Zero;
            }

            _addedDataFormats.Dispose();
            _readyDataFormats.Dispose();
            _removedDataFormats.Dispose();

            _platformDataAcquirer.Dispose();

            MonoBehaviourEventDispatcher.Updating.RemoveListener(SendUpdatedFrameData);

            Application.onBeforeRender -= OnBeforeRender;
        }

        private void OnBeforeRender()
        {
            ProfilerUtility.EventInstance("Rendering", "FrameUpdate",
                new CustomProcessingOptions { ProcessingType = CustomProcessingOptions.Type.TIME_UNTIL_NEXT });
        }

        public void SendUpdatedFrameData()
        {
            if (!_platformDataAcquirer.TryToBeReady())
            {
                return;
            }

            const string traceMethodName = "SendUpdatedFrameData";
            ProfilerUtility.EventBegin(TraceCategory, traceMethodName);

            _api.ARDK_SAH_GetDataFormatsReadyForNewFrame
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

            ARDKFrameData frameData = new ARDKFrameData();

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
                        Log.Error(
                            $"Native layer requested a format {_readyDataFormats[i]} that is not handled by the PAM.");
                        break;
                }
            }

            // Pose
            if (_platformDataAcquirer.TryGetCameraPose(out Matrix4x4 cameraToLocal))
            {
                frameData.CameraPose.SetTransform(cameraToLocal.FromUnityToArdk());
            }
            else
            {
                // The SAH checks the CameraPose against the Identity transform to validate if
                // a valid value was received or not
                frameData.CameraPose.SetTransform(Matrix4x4.identity.FromUnityToArdk());
            }

            frameData.FrameId = _frameCounter++;

            frameData.CameraTimestampMs = _platformDataAcquirer.TryGetCameraTimestampMs(out var timestampMs)
                ? (ulong)timestampMs
                : 0;

            frameData.ScreenOrientation = _platformDataAcquirer.GetScreenOrientation().FromUnityToArdk();
            frameData.TrackingState = _platformDataAcquirer.GetTrackingState().FromUnityToArdk();

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

            if (getDepthImage &&
                _platformDataAcquirer.TryGetDepthCpuImage(out var cpuDepth, out var cpuDepthConfidence))
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
                _platformDataAcquirer.TryGetDepthCameraIntrinsicsCStruct(out frameData.DepthCameraIntrinsics);
            }

            // New WIP PAM codepath
            const string nativePamOnFrameEventName = "ARDK_SAH_OnFrame";
            ProfilerUtility.EventBegin(TraceCategory, nativePamOnFrameEventName);
            {
                unsafe
                {
                    void* nonMoveablePtr = &frameData;
                    _api.ARDK_SAH_OnFrame(_nativeHandle, (IntPtr)nonMoveablePtr);
                }
            }
            ProfilerUtility.EventEnd(TraceCategory, nativePamOnFrameEventName);
            ProfilerUtility.EventEnd(TraceCategory, traceMethodName + traceReadyFormats);

            if (SentData != null)
            {
                _api.ARDK_SAH_GetDispatchedFormatsToModules(_nativeHandle,
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
