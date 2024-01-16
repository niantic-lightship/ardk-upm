// Copyright 2022-2024 Niantic.
using System;
using Unity.Collections;
using Random = System.Random;

namespace Niantic.Lightship.AR.Scanning
{
    internal class MockSqcApi : ISqcApi
    {
        private IntPtr _handle;
        private static bool _isRunning;
        private static float _progress;

        public IntPtr SQCCreate(IntPtr unityContext)
        {
            _handle = new IntPtr(1);
            return _handle;
        }

        public void SQCRelease(IntPtr handle)
        {
            if (handle == _handle)
            {
                _handle = IntPtr.Zero;
            }
        }

        public bool SQCRun(IntPtr handle, float framerate, string scanPath)
        {
            if (!_isRunning)
            {
                _isRunning = true;
                return true;
            }

            return false;
        }

        public void SQCCancelCurrentRun(IntPtr handle)
        {
            _isRunning = false;
        }

        public bool SQCIsRunning(IntPtr handle)
        {
            return _isRunning;
        }

        public float SQCGetProgress(IntPtr handle)
        {
            // Cache the value for return.
            var progress = _progress;
            // Increase the |_progress| for next query.
            if (_isRunning)
            {
                _progress = _progress >= 90 ? 100 : _progress + 10;
            }

            return progress;
        }

        public void SQCGetResult(IntPtr handle,
            string scanPath, IntPtr scores, out int scoresSize)
        {
            scoresSize = 0;
        }
    }
}
