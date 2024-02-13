// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using UnityEngine;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Scanning;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace Niantic.ARDK.AR.Scanning
{
    /// <summary>
    /// ARScanQualityClassifier is responsible for computing the scan quality.
    ///   - Run() will start a quality compute asynchronously.
    ///   - CancelCurrentRun() will interrupt the current compute, if any.
    /// </summary>
    /// Result returned by the <see cref="ARScanQualityClassifier"/>.
    [PublicAPI]
    public class ARScanQualityClassifier : IARScanQualityClassifier
    {
        private readonly ISqcApi _api;
        private IntPtr _nativeHandle = IntPtr.Zero;
        private NativeArray<ScanningSqcScores> _qualityScores;

        public ARScanQualityClassifier()
        {
            _api = new NativeSqcApi();
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _nativeHandle = _api.SQCCreate(LightshipUnityContext.UnityContextHandle);
#endif
            var numCategories = Enum.GetValues(typeof(ScanQualityCategory)).Length;
            _qualityScores = new NativeArray<ScanningSqcScores>(numCategories, Allocator.Persistent);
        }

        ~ARScanQualityClassifier()
        {
            Dispose();
        }

        /// <summary>
        /// Dispose the object and its internal resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_nativeHandle != IntPtr.Zero)
            {
                _api.SQCRelease(_nativeHandle);
                _nativeHandle = IntPtr.Zero;
            }

            _qualityScores.Dispose();
        }

        /// <summary>
        /// Start a run to compute the scan quality asynchoursly.
        /// </summary>
        /// <param name="scanPath">Identifier of a scan.</param>
        /// <returns>Whether or not the run will start, if ARScanQualityClassifier has already
        ///          running, it will return false. </returns>
        public bool Run(float framerate, string scanPath)
        {
            if (framerate < 0 || framerate > 60)
            {
                Log.Error("Invalid framerate");
                return false;
            }

            if (!_nativeHandle.IsValidHandle())
                return false;

            return _api.SQCRun(_nativeHandle, framerate, scanPath);
        }

        /// <summary>
        /// Cancel current run. When the function returns, it is guaranteed the ARScanQualityClassifier
        /// is not running a scan. Do nothing if it is not running.
        /// </summary>
        public void CancelCurrentRun()
        {
            if (!_nativeHandle.IsValidHandle())
                return;

            _api.SQCCancelCurrentRun(_nativeHandle);
        }

        /// <summary>
        /// Flag tells if a scan quality compute is running or not.
        /// </summary>
        /// <returns> Whether or not a run of quality computer is ongoing. <returns>
        public bool Running
        {
            get
            {
                if (!_nativeHandle.IsValidHandle())
                    return false;

                return _api.SQCIsRunning(_nativeHandle);
            }
        }

        /// <summary>
        /// Progress of current quality compute, range is in percent between [0, 100.0].
        /// </summary>
        /// <returns> Percentage of existing run. 0 will be returned if not running a quality compute.
        /// <returns>
        public float Progress
        {
            get
            {
                if (!_nativeHandle.IsValidHandle())
                    return -1f;

                return _api.SQCGetProgress(_nativeHandle);
            }
        }

        /// <summary>
        /// Returns a ScanQualityResult of current quality compute,
        /// Scores are in range of [0, 1.0]. Higher values means high scan quality.
        /// Categories are within ScanQualityCategory.
        /// </summary>
        /// <returns> A ScanQualityResult with list of Scores and CategoriesFailRequirement.
        /// <returns>
        public ScanQualityResult GetResult(string scanPath)
        {
            if (!_nativeHandle.IsValidHandle())
                return null;

            var scanQualityResult = new ScanQualityResult();
            scanQualityResult.RejectionReasons = new List<ScanningSqcScores>();
            var scoresOutSize = 0;
            unsafe
            {
                _api.SQCGetResult(_nativeHandle, scanPath, (IntPtr)_qualityScores.GetUnsafePtr(), out int size);
                scoresOutSize = size;
            }

            if (scoresOutSize > Enum.GetValues(typeof(ScanQualityCategory)).Length)
            {
                throw new ArgumentException("Native Size does not match ScanQualityCategory.Length");
            }

            for (int i = 0; i < scoresOutSize; i++)
            {
                if (_qualityScores[i].Category == ScanQualityCategory.Overall)
                {
                    scanQualityResult.ScanQualityScore = _qualityScores[i].Score;
                }
                else
                {
                    scanQualityResult.RejectionReasons.Add(_qualityScores[i]);
                }
            }

            return scanQualityResult;
        }
    }
}
