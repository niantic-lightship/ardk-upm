// Copyright 2023 Niantic, Inc. All Rights Reserved.

namespace Niantic.Lightship.AR.Subsystems
{
    public enum XRScanningState
    {
        /// The scanner is created and ready to start. From this state:
        ///    - <see cref="Start"/> can be called to begin scanning.
        Ready = 0,

        /// The scanner is started and scanning. From this state:
        ///    - <see cref="Stop"/> can be called to end the scan, transitioning to the *Stopped* state.
        Started,

        /// Scanning is stopped. From this state:
        ///    - <see cref="Start"/> can be called to re-start scanning, transitioning to the *Started* state.
        Stopped,

        /// Scan processing has failed. From this state:
        ///    - <see cref="Stop"/> can be called to reset the scanner to the *Stopped* state.
        Error = 8,
    }
}
