// Copyright 2022-2025 Niantic.

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
        private DataFormatFlags _addedDataFormats;
        private DataFormatFlags _readyDataFormats;
        private DataFormatFlags _removedDataFormats;
        private int _readyDataFormatsSize;

        private uint _frameCounter;
        private bool _alreadyDisposed;

        private ulong? _prevSentTimestamp = null;

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

            //var bitflag = DataFormatConverter.ArrayToBitFlag(_readyDataFormats);
            _api.ARDK_SAH_GetDataFormatsReadyForNewFrame
            (
                _nativeHandle,
                out uint readyDataFormatsUInt
            );
            _readyDataFormats = (DataFormatFlags)readyDataFormatsUInt;

            if (readyDataFormatsUInt == 0)
            {
                ProfilerUtility.EventEnd(TraceCategory, traceMethodName);
                return;
            }

            // Profile group by ready formats
            string traceReadyFormats = DataFormatUtils.FlagsToString(_readyDataFormats);

            ProfilerUtility.EventBegin(TraceCategory, traceMethodName + traceReadyFormats);

            ARDKFrameData frameData = new ARDKFrameData();

            // Populate the compass and GPS data, which updates independently of AR data.
            // Population of AR data must be done after the timestamp check below.
            var sendingAnyData = false;
            if ((_readyDataFormats & DataFormatFlags.kCompass) == DataFormatFlags.kCompass)
            {
                sendingAnyData |= _platformDataAcquirer.TryGetCompass(out frameData.CompassData);
            }

            if ((_readyDataFormats & DataFormatFlags.kGpsLocation) == DataFormatFlags.kGpsLocation)
            {
                sendingAnyData |= _platformDataAcquirer.TryGetGpsLocation(out frameData.GpsLocation);
            }

            var hasTimestamp = _platformDataAcquirer.TryGetCameraTimestampMs(out double timestampMs);
            var currTimestamp = (ulong)timestampMs;
            if (hasTimestamp && (!_prevSentTimestamp.HasValue || currTimestamp != _prevSentTimestamp))
            {
                sendingAnyData = true;
                _prevSentTimestamp = currTimestamp;

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

                frameData.CameraTimestampMs = currTimestamp;
                frameData.ScreenOrientation = _platformDataAcquirer.GetScreenOrientation().FromUnityToArdk();
                frameData.TrackingState = _platformDataAcquirer.GetTrackingState().FromUnityToArdk();

                // Check if we are requesting a camera image
                var getCameraImage = (_readyDataFormats & DataFormatFlags.kImage) != DataFormatFlags.kNone;
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

                // Check if we are requesting a depth image
                var getDepthImage = (_readyDataFormats & DataFormatFlags.kPlatform_depth) == DataFormatFlags.kPlatform_depth;
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
                    
                    if (_platformDataAcquirer.TryGetDepthPose(out var depthPose))
                    {
                        frameData.DepthCameraPose.SetTransform(depthPose.FromUnityToArdk());
                    }
                    else
                    {
                        frameData.DepthCameraPose.SetTransform(Matrix4x4.identity.FromUnityToArdk());
                    }
                }
            }

            if (sendingAnyData)
            {
                // This way, the frame id always (and only) increments when we actually have data to send
                frameData.FrameId = _frameCounter++;

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
            }

            ProfilerUtility.EventEnd(TraceCategory, traceMethodName + traceReadyFormats);

            if (SentData != null)
            {
                _api.ARDK_SAH_GetDispatchedFormatsToModules
                (
                    _nativeHandle,
                    out var dispatchedFrameId,
                    out var dispatchedToModules,
                    out var dispatchedDataFormats
                );

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
        public readonly DataFormatFlags FormatsSent;

        public PamEventArgs(uint dispatchedFormats)
        {
            FormatsSent = (DataFormatFlags)dispatchedFormats;
        }
    }
}
