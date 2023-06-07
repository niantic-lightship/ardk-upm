using System;
using System.IO;
using Niantic.Lightship.AR.Utilities.CTrace;
using Niantic.Lightship.Utilities;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Playback
{
    internal class _PlaybackDatasetReader
    {
        private readonly _PlaybackDataset _dataset;
        private int _currentFrameIndex = -1;
        private int _lastLoadedImageFrameNumber = -1;
        private byte[] _imageBytes;

        private _ICTrace _ctrace;
        private const UInt64 CTRACE_ID = 185756; // A 64bit ID used to group ctrace events.

        public _PlaybackDatasetReader(_PlaybackDataset dataset)
        {
            if (dataset == null)
                throw new ArgumentNullException(nameof(dataset));

            _dataset = dataset;

            _ctrace = new _NativeCTrace();
            _ctrace.InitializeCtrace();
        }

        public bool TryMoveToNextFrame()
        {
            if (_currentFrameIndex == _dataset.FrameCount - 1)
            {
                _finished = true;
                return false;
            }

            _currentFrameIndex++;
            return true;
        }

        private bool _finished;
        public bool Finished => _finished;

        public string GetDatasetPath() => _dataset.DatasetPath;

        public bool GetAutofocusEnabled() => _dataset.AutofocusEnabled;

        public Vector2Int GetImageResolution() => _dataset.Resolution;

        public Vector2Int GetDepthResolution() => _dataset.DepthResolution;

        public int GetFramerate() => _dataset.FrameRate;

        public int GetTotalFrameCount() => _dataset.FrameCount;

        public bool GetLocationServicesEnabled() => _dataset.LocationServicesEnabled;

        public bool GetIsLidarAvailable() => _dataset?.LidarEnabled ?? false;

        public Matrix4x4 GetCurrentPose() => CurrFrame?.Pose ?? Matrix4x4.zero;

        public Matrix4x4 GetCurrentProjectionMatrix() => CurrFrame?.ProjectionMatrix ?? Matrix4x4.zero;

        public XRCameraIntrinsics GetCurrentIntrinsics() => CurrFrame?.Intrinsics ?? default;

        public byte[] GetCurrentImageData() =>
            CurrFrame != null ? ReadImageData(_currentFrameIndex, CurrFrame.ImagePath) : null;

        public string GetCurrentImagePath() => CurrFrame?.ImagePath;

        public string GetCurrentDepthPath() => CurrFrame?.DepthPath;

        public string GetCurrentDepthConfidencePath() => CurrFrame?.DepthConfidencePath;

        public TrackingState GetCurrentTrackingState() => CurrFrame?.TrackingState ?? TrackingState.None;

        public double GetCurrentTimestampInSeconds() => CurrFrame?.TimestampInSeconds ?? 0;

        // TODO: add logic to determine if dataset is Lidar or not
        public bool GetIsLidarData() => true;

        public int CurrentFrameIndex => _currentFrameIndex;

        public _PlaybackDataset.FrameMetadata CurrFrame
        {
            get
            {
                if (_currentFrameIndex < 0)
                    return null;

                return _dataset.Frames[_currentFrameIndex];
            }
        }

        private byte[] ReadImageData(int frameNumber, string fileName)
        {
            if (_lastLoadedImageFrameNumber == frameNumber)
                return _imageBytes;

            _ctrace.TraceEventAsyncBegin0("_PlaybackDatasetReader", "ReadImageData", CTRACE_ID);
            var filePath = Path.Combine(_dataset.DatasetPath, fileName);
            _imageBytes = FileUtilities.GetAllBytes(filePath);
            _lastLoadedImageFrameNumber = frameNumber;
            _ctrace.TraceEventAsyncEnd0("_PlaybackDatasetReader", "ReadImageData", CTRACE_ID);
            return _imageBytes;
        }
    }
}
