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

        public override bool TryGetCameraTimestampMs(out double timestampMs)
        {
            timestampMs = (double)(DateTimeOffset.Now.ToUnixTimeMilliseconds());
            return true;
        }

        public override bool TryGetCameraPose(out Matrix4x4 pose)
        {
            pose = Matrix4x4.TRS(Vector3.forward, Quaternion.identity, Vector3.one);
            return true;
        }

        public override bool TryGetCpuImage(out LightshipCpuImage cpuImage)
        {
            cpuImage = new LightshipCpuImage();
            return true;
        }

        public override bool TryGetDepthCpuImage
        (
            out LightshipCpuImage depthCpuImage,
            out LightshipCpuImage confidenceCpuImage
        )
        {
            depthCpuImage = default;
            confidenceCpuImage = default;
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

        public override bool TryGetCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct intrinsics)
        {
            intrinsics =
                new CameraIntrinsicsCStruct
                (
                    new Vector2(554.256f, 579.411f),
                    new Vector2(320, 240),
                    new Vector2Int(720, 540)
                );

            return true;
        }

        public override bool TryGetDepthCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct depthIntrinsics)
        {
            depthIntrinsics =
                new CameraIntrinsicsCStruct
                (
                    new Vector2(554.256f, 579.411f),
                    new Vector2(320, 240),
                    new Vector2Int(720, 540)
                );

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
    }
}
