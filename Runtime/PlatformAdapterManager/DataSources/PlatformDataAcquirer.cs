// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PAM
{
    /// Adapter that connects to all data sources that the PlatformAdapterManager (PAM) needs.
    /// Drives the PAM lifecycle through the OnUpdate Action.
    internal abstract class PlatformDataAcquirer : IDisposable
    {
        public abstract bool TryToBeReady();

        public abstract bool TryGetCameraFrame(out XRCameraFrame frame);

        public abstract bool TryGetCameraPose(out Matrix4x4 pose);

        public abstract bool TryGetCpuImage(out XRCpuImage cpuImage);

        public abstract bool TryGetGpuImage(out Texture2D gpuImage);

        public abstract bool TryGetCpuDepthImage(out XRCpuImage cpuDepthImage, out XRCpuImage cpuDepthConfidenceImage);

        public abstract bool TryGetGpuDepthImage(out Texture2D gpuDepthImage, out Texture2D gpuDepthConfidenceImage);

        public abstract ScreenOrientation GetScreenOrientation();

        public abstract TrackingState GetTrackingState();

        public abstract bool TryGetCameraIntrinsics(out XRCameraIntrinsics intrinsics);

        public abstract bool TryGetGpsLocation(out GpsLocation gps);

        public abstract bool TryGetCompass(out CompassData compass);

        public virtual void Dispose()
        {
        }

        public abstract void OnFormatAdded(DataFormat addedFormat);
        public abstract void OnFormatRemoved(DataFormat addedFormat);
    }
}
