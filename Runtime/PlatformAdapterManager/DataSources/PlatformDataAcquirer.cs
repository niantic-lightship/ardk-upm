// Copyright 2022-2024 Niantic.
using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PAM
{
    /// Adapter that connects to all data sources that the PlatformAdapterManager (PAM) needs.
    /// Drives the PAM lifecycle through the OnUpdate Action.
    internal abstract class PlatformDataAcquirer : IDisposable
    {
        public abstract bool TryToBeReady();

        public abstract bool TryGetCameraTimestampMs(out double timestampMs);

        public abstract bool TryGetCameraPose(out Matrix4x4 pose);

        public abstract bool TryGetCpuImageDeprecated(out XRCpuImage cpuImage);

        public abstract bool TryGetDepthCpuImageDeprecated
        (
            out XRCpuImage cpuDepthImage,
            out XRCpuImage cpuDepthConfidenceImage
        );

        public abstract bool TryGetCpuImage(out LightshipCpuImage cpuImage);

        public abstract bool TryGetDepthCpuImage
        (
            out LightshipCpuImage depthCpuImage,
            out LightshipCpuImage confidenceCpuImage
        );

        public abstract ScreenOrientation GetScreenOrientation();

        public abstract TrackingState GetTrackingState();

        public abstract bool TryGetCameraIntrinsicsDeprecated(out XRCameraIntrinsics intrinsics);

        public abstract bool TryGetCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct intrinsics);

        public abstract bool TryGetDepthCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct depthIntrinsics, Vector2Int depthResolution);

        public abstract bool TryGetGpsLocation(out GpsLocationCStruct gps);

        public abstract bool TryGetCompass(out CompassDataCStruct compass);

        public virtual void Dispose() { }

        // DEPRECATED – New PAM does FormatAdded on Register, reduces interface for new HMDs
        public abstract void OnFormatAdded(DataFormat addedFormat);
        // DEPRECATED – New PAM does FormatAdded on Register, reduces interface for new HMDs
        public abstract void OnFormatRemoved(DataFormat addedFormat);
    }
}
