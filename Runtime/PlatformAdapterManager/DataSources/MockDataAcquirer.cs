// Copyright 2022-2024 Niantic.
using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PAM
{
    internal class MockDataAcquirer : PlatformDataAcquirer
    {
        public override bool TryToBeReady()
        {
            return true;
        }

        // Always returns true. Frame only has valid timestamp.
        public override bool TryGetCameraFrameDeprecated(out XRCameraFrame frame)
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

        public override bool TryGetCameraTimestampMs(out ulong timestampNs)
        {
            timestampNs = (ulong)(DateTimeOffset.Now.ToUnixTimeMilliseconds());
            return true;
        }

        public override bool TryGetCameraPose(out Matrix4x4 pose)
        {
            pose = Matrix4x4.TRS(Vector3.forward, Quaternion.identity, Vector3.one);
            return true;
        }

        public override bool TryGetCpuImageDeprecated(out XRCpuImage cpuImage)
        {
            cpuImage = new XRCpuImage();
            return true;
        }

        public override bool TryGetCpuDepthImageDeprecated(out XRCpuImage cpuDepthImage, out XRCpuImage cpuDepthConfidenceImage)
        {
            cpuDepthImage = default;
            cpuDepthConfidenceImage = default;
            return false;
        }

        public override bool TryGetLightshipCpuImage(out LightshipCpuImage cpuImage)
        {
            cpuImage = new LightshipCpuImage();
            return true;
        }

        public override bool TryGetLightshipCpuDepthImage(out LightshipCpuImage cpuDepthImage, out LightshipCpuImage cpuDepthConfidenceImage)
        {
            cpuDepthImage = default;
            cpuDepthConfidenceImage = default;
            return false;
        }

        public override ScreenOrientation GetScreenOrientation()
        {
            return ScreenOrientation.Portrait;
        }

        public override TrackingState GetTrackingState()
        {
            return TrackingState.Tracking;
        }

        public override bool TryGetCameraIntrinsicsDeprecated(out XRCameraIntrinsics intrinsics)
        {
            intrinsics =
                new XRCameraIntrinsics(new Vector2(554.256f, 579.411f), new Vector2(320, 240), new Vector2Int(640, 480));
            return true;
        }

        public override bool TryGetCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct intrinsics)
        {
            intrinsics = default;
            intrinsics.SetIntrinsics(new Vector2(554.256f, 579.411f), new Vector2(320, 240));
            // Note: no field for resolution: new Vector2Int(640, 480)
            return true;
        }

        public override bool TryGetGpsLocation(out GpsLocationCStruct gps)
        {
            gps = default;
            return true;
        }

        public override bool TryGetCompass(out CompassDataCStruct compass)
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
