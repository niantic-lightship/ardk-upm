// Copyright 2022-2024 Niantic.
using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PAM
{
    internal class MockDataAcquirer : PlatformDataAcquirer
    {
        private readonly Texture2D _emptyTexture = new Texture2D(2, 2);

        public override bool TryToBeReady()
        {
            return true;
        }

        // Always returns true. Frame only has valid timestamp.
        public override bool TryGetCameraFrame(out XRCameraFrame frame)
        {
            frame = new XRCameraFrame
            (
                DateTimeOffset.Now.ToUnixTimeMilliseconds() * 1000000,
                0,
                0,
                default,
                Matrix4x4.zero,
                Matrix4x4.zero,
                TrackingState.Tracking,
                IntPtr.Zero,
                XRCameraFrameProperties.Timestamp,
                0,
                0,
                0,
                0,
                default,
                Vector3.zero,
                default,
                default,
                0
            );

            return true;
        }

        public override bool TryGetCameraPose(out Matrix4x4 pose)
        {
            pose = Matrix4x4.TRS(Vector3.forward, Quaternion.identity, Vector3.one);
            return true;
        }

        public override bool TryGetCpuImage(out XRCpuImage cpuImage)
        {
            cpuImage = new XRCpuImage();
            return true;
        }

        public override bool TryGetGpuImage(out Texture2D gpuImage)
        {
            gpuImage = _emptyTexture;
            return true;
        }

        public override bool TryGetCpuDepthImage(out XRCpuImage cpuDepthImage, out XRCpuImage cpuDepthConfidenceImage)
        {
            cpuDepthImage = default;
            cpuDepthConfidenceImage = default;
            return false;
        }

        public override bool TryGetGpuDepthImage(out Texture2D gpuDepthImage, out Texture2D gpuDepthConfidenceImage)
        {
            gpuDepthImage = _emptyTexture;
            gpuDepthConfidenceImage = _emptyTexture;
            return true;
        }

        public override ScreenOrientation GetScreenOrientation()
        {
            return ScreenOrientation.Portrait;
        }

        public override TrackingState GetTrackingState()
        {
            return TrackingState.Tracking;
        }

        public override bool TryGetCameraIntrinsics(out XRCameraIntrinsics intrinsics)
        {
            intrinsics =
                new XRCameraIntrinsics(new Vector2(554.256f, 579.411f), new Vector2(320, 240), new Vector2Int(640, 480));
            return true;
        }

        public override bool TryGetGpsLocation(out GpsLocation gps)
        {
            gps = default;
            return true;
        }

        public override bool TryGetCompass(out CompassData compass)
        {
            compass = default;
            return true;
        }

        public override void OnFormatAdded(DataFormat addedFormat)
        {

        }

        public override void OnFormatRemoved(DataFormat addedFormat)
        {

        }
    }
}
