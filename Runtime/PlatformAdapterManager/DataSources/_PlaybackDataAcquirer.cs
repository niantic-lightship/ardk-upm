using System;
using System.IO;
using Niantic.Lightship.AR.Playback;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    internal class _PlaybackDataAcquirer : _PlatformDataAcquirer, _IPlaybackDatasetUser
    {
        private _PlaybackDatasetReader _reader;

        private Texture2D _gpuImage;
        private int _cachedImageIndex = -1;

        public _PlaybackDataAcquirer()
        {
#if ANDROID
            throw new Exception("Streaming Assets and therefore Playback not supported on Android");
#endif
            _gpuImage = new Texture2D(1, 1);
        }

        public override void Dispose()
        {
            Object.DestroyImmediate(_gpuImage);
        }

        public override DeviceOrientation GetDeviceOrientation()
        {
            return DeviceOrientation.Portrait;
        }

        public override bool TryToBeReady()
        {
            return true;
        }

        public override bool TryGetCameraFrame(out XRCameraFrame frame)
        {
            frame = default;
            return false;
        }

        public override bool TryGetCameraPose(out Matrix4x4 pose)
        {
            pose = _reader.GetCurrentPose();
            return true;
        }

        public override bool TryGetImageResolution(out Resolution resolution)
        {
            var imageResolution = _reader.GetImageResolution();

            resolution = new Resolution
            {
                width = imageResolution.x, height = imageResolution.y
            };

            return true;
        }

        public override bool TryGetCpuImage(out XRCpuImage cpuImage)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetGpuImage(out Texture2D gpuImage)
        {
            if (_cachedImageIndex != _reader.CurrentFrameIndex)
            {
                var filePath = Path.Combine(_reader.GetDatasetPath(), _reader.GetCurrentImagePath());
                _gpuImage.LoadImage(File.ReadAllBytes(filePath));
                _cachedImageIndex = _reader.CurrentFrameIndex;
            }

            gpuImage = _gpuImage;
            return true;
        }

        public override bool TryGetCpuDepthImage(out XRCpuImage cpuDepthImage, out XRCpuImage cpuDepthConfidenceImage)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetGpuDepthImage(out Texture2D gpuDepthImage, out Texture2D gpuDepthConfidenceImage)
        {
            gpuDepthImage = null;
            gpuDepthConfidenceImage = null;
            return false;
        }

        public override TrackingState GetTrackingState()
        {
            return _reader.GetCurrentTrackingState();
        }

        public override bool TryGetCameraIntrinsics(out XRCameraIntrinsics intrinsics)
        {
            intrinsics = _reader.GetCurrentIntrinsics();
            return true;
        }

        public override bool TryGetGpsLocation(out GpsLocation gps)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetCompass(out CompassData compass)
        {
            throw new NotImplementedException();
        }

        public void SetPlaybackDatasetReader(_PlaybackDatasetReader reader)
        {
            _reader = reader;

            // Set framerate in Unity
            Time.fixedDeltaTime = 1f / _reader.GetFramerate();
        }
    }
}
