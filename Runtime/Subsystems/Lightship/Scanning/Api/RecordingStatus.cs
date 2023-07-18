// Copyright 2023 Niantic, Inc. All Rights Reserved.

namespace Niantic.Lightship.AR.ScanningSubsystem
{
    public enum RecordingStatus
    {
        Unknown = 0,

        /// <summary>
        /// The scan is currently recording.
        /// </summary>
        Started = 1,

        /// <summary>
        /// The scan has been saved to persistent storage.
        /// </summary>
        Saved = 2,

        /// <summary>
        /// The scan has been discarded.
        /// </summary>
        Discarded = 3,

        /// <summary>
        /// There is an error with the scan.
        /// </summary>
        Error = 4
    }
}
