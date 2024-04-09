// Copyright 2022-2024 Niantic.

using System;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PAM
{
    internal abstract class AbstractTexturesSetter : IDisposable
    {
        protected PlatformDataAcquirer PlatformDataAcquirer;
        protected FrameData CurrentFrameData;

        protected UInt64 CtraceId;

        private const int MillisecondToNanosecondFactor = 1000000;

        public AbstractTexturesSetter(PlatformDataAcquirer dataAcquirer, FrameData frameData)
        {
            PlatformDataAcquirer = dataAcquirer;
            CurrentFrameData = frameData;
        }

        public abstract void InvalidateCachedTextures();

        public abstract void Dispose();

        // Get the timestamp associated with the current textures
        public virtual double GetCurrentTimestampMs()
        {
            if (PlatformDataAcquirer.TryGetCameraFrame(out XRCameraFrame frame))
                return (double)frame.timestampNs / MillisecondToNanosecondFactor;

            return 0;
        }

        public abstract void SetRgba256x144Image();
        public abstract void SetRgb256x256Image();
        public abstract void SetJpeg720x540Image();
        public abstract void SetJpegFullResImage();
        public abstract void SetPlatformDepthBuffer();
        protected abstract bool ReinitializeJpegFullResDataIfNeeded();
    }
}
