// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.Lightship.AR.Utilities.CTrace;
using PlatformAdapterManager;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    internal abstract class AbstractTexturesSetter : IDisposable
    {
        protected _PlatformDataAcquirer _platformDataAcquirer;
        protected _FrameData _currentFrameData;

        protected _ICTrace _ctrace;
        protected UInt64 _ctraceId;

        private const int MillisecondToNanosecondFactor = 1000000;

        public AbstractTexturesSetter(_PlatformDataAcquirer dataAcquirer, _FrameData frameData, _ICTrace ctrace, UInt64 ctraceId)
        {
            _ctrace = ctrace;
            _ctraceId = ctraceId;
            _platformDataAcquirer = dataAcquirer;
            _currentFrameData = frameData;
        }

        public abstract void InvalidateCachedTextures();

        public abstract void Dispose();

        // Get the timestamp associated with the current textures
        public virtual double GetCurrentTimestampMs()
        {
            if (_platformDataAcquirer.TryGetCameraFrame(out XRCameraFrame frame))
                return frame.timestampNs / MillisecondToNanosecondFactor;

            return 0;
        }

        public abstract void SetRgba256x144Image();
        public abstract void SetJpeg720x540Image();
        public abstract void SetJpegFullResImage();
        public abstract void SetPlatformDepthBuffer();
        protected abstract bool ReinitializeJpegFullResDataIfNeeded();
    }
}
