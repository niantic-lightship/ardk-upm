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

        private readonly _ICTrace _ctrace;
        private const UInt64 CTRACE_ID = 185756; // A 64bit ID used to group ctrace events.

        private readonly bool _loopInfinitely;
        private uint _iterations;
        // for looping we go back and forth to not have immediate jumps in pose and tracking
        private bool _goingForward = true;
        private double _timestampLoopOffset;

        public _PlaybackDatasetReader(_PlaybackDataset dataset, uint iterations = 1, bool loopInfinitely = false)
        {
            if (dataset == null)
                throw new ArgumentNullException(nameof(dataset));

            _dataset = dataset;
            _iterations = iterations;
            _loopInfinitely = loopInfinitely;

            _ctrace = new _NativeCTrace();
            _ctrace.InitializeCtrace();
        }

        public bool TryMoveToNextFrame()
        {
            // we have reached the end of the dataset, either forward or looping backwards
            if (_currentFrameIndex == _dataset.FrameCount - 1 && _goingForward
                || _currentFrameIndex == 0 && !_goingForward)
            {
                if (_iterations > 1 || _loopInfinitely)
                {
                    _goingForward = !_goingForward;
                    _iterations--;
                }
                // no more looping left
                else
                {
                    _finished = true;
                    return false;
                }
            }

            if (_goingForward)
            {
                _currentFrameIndex++;
            }
            else
            {
                _currentFrameIndex--;
                // increase timestamp offset with the difference between this and last played frame
                var deltaTimeBetweenFrames = _dataset.Frames[_currentFrameIndex + 1].TimestampInSeconds -
                    _dataset.Frames[_currentFrameIndex].TimestampInSeconds;
                _timestampLoopOffset += 2 * deltaTimeBetweenFrames;
            }

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

        public bool GetCompassEnabled() => _dataset.CompassEnabled;

        public bool GetIsLidarAvailable() => _dataset?.LidarEnabled ?? false;

        public Matrix4x4 GetCurrentPose() => CurrFrame?.Pose ?? MatrixUtils.InvalidMatrix;

        public Matrix4x4 GetCurrentProjectionMatrix() => CurrFrame?.ProjectionMatrix ?? MatrixUtils.InvalidMatrix;

        public XRCameraIntrinsics GetCurrentIntrinsics() => CurrFrame?.Intrinsics ?? default;

        public byte[] GetCurrentImageData() =>
            CurrFrame != null ? ReadImageData(_currentFrameIndex, CurrFrame.ImagePath) : null;

        public string GetCurrentImagePath() => CurrFrame?.ImagePath;

        public string GetCurrentDepthPath() => CurrFrame?.DepthPath;

        public string GetCurrentDepthConfidencePath() => CurrFrame?.DepthConfidencePath;

        public TrackingState GetCurrentTrackingState() => CurrFrame?.TrackingState ?? TrackingState.None;

        public double GetCurrentTimestampInSeconds() => CurrFrame?.TimestampInSeconds + _timestampLoopOffset ?? 0;

        public int CurrentFrameIndex => _currentFrameIndex;

        public void Reset()
        {
            _currentFrameIndex = -1;
            _timestampLoopOffset = 0;
            _goingForward = true;
            _iterations = 1; // reset to default
            _finished = false;
        }

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
