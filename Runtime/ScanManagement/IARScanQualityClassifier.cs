// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.Scanning
{
    /// Scan quality categories.
    /// Note, if changing this category, it needs to change the relevant native enum.
    public enum ScanQualityCategory : UInt32
    {
        // The overall score of the scan.
        Overall = 0,
        // Is the scan blurry?
        Blurry = 1,
        // Is the scan too dark?
        Dark = 2,
        // Is the scan bad quality?
        BadQuality = 3,
        // Is the scan focused on the ground or feet?
        GroundOrFeet = 4,
        // Is the scan captured indoor?
        ScanIndoor = 5,
        // Is the scan captured from inside a car?
        ScanFromCar = 6,
        // Is the scan target obstructed?
        Obstruction = 7,
        // Is the scan target visible?
        ScanTargetNotVisible = 8
    }

    /// This struct is used by publicly and internally.
    /// For internal usage, we will pass down an array of struct to native.
    [StructLayout(LayoutKind.Sequential)]
    public struct ScanningSqcScores
    {
        // Category of score.
        public ScanQualityCategory Category;

        // Quality score of specific category.
        public float Score;
    }

    /// Scan Quality Result.
    [PublicAPI]
    public class ScanQualityResult
    {
        /// An overall score of the scan's quality. Range is 0-1, higher is better.
        public float ScanQualityScore { get; set; }

        /// Returns a list of problems with the scan that may contribute to it receiving a lower
        /// scan quality score. This list will be empty for high-quality scans.
        public List<ScanningSqcScores> RejectionReasons { get; set; }
    }

    [PublicAPI]
    public interface IARScanQualityClassifier : IDisposable
    {
        /// <summary>
        /// Start a run to compute the scan quality asynchoursly.
        /// </summary>
        /// <param name="scanPath">Identifier of a scan.</param>
        /// <returns>Whether or not the run will start, if ARScanQualityClassifier has already
        ///          running, it will return false. </returns>
        bool Run(float framerate, string scanPath);

        /// <summary>
        /// Cancel current run. When the function returns, it is guaranteed the ARScanQualityClassifier
        /// is not running a scan. Do nothing if it is not running.
        /// </summary>
        void CancelCurrentRun();

        /// <summary>
        /// Flag tells if a scan quality compute is running or not.
        /// </summary>
        /// <returns> Whether or not a run of quality computer is ongoing. <returns>
        bool Running { get; }

        /// <summary>
        /// Progress of current quality compute, range is in percent between [0, 100.0].
        /// </summary>
        /// <returns> Percentage of existing run. 0 will be returned if not running a quality compute.
        /// <returns>
        float Progress { get; }

        /// <summary>
        /// Returns a ScanQualityResult of current quality compute,
        /// Scores are in range of [0, 1.0]. Higher values means high scan quality.
        /// Categories are within ScanQualityCategory.
        /// </summary>
        /// <returns> A ScanQualityResult with overall score and RejectionReasons.
        ///           RejectionReasons will be empty if all scores pass the quality bar.
        /// <returns>
        ScanQualityResult GetResult(string scanPath);
    }
}
