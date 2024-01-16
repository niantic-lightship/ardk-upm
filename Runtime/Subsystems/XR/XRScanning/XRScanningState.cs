// Copyright 2022-2024 Niantic.

namespace Niantic.Lightship.AR.XRSubsystems
{
    public enum XRScanningState
    {
        Unknown = 0,

        /// The scanner is created and ready to start. From this state:
        ///    - <see cref="Start"/> can be called to begin scanning.
        Ready,

        /// The scanner is started and scanning. From this state:
        ///    - <see cref="Stop"/> can be called to end the scan, transitioning to the *Stopped* state.
        ///    - <see cref="SaveScan"/> can be called to save the scan, transitioning to the *Saving* state.
        ///    - <see cref="DiscardScan"/> can be called to discard the scan, transitioning to the *Discarding* state.
        Started,

        /// The scan is currently being saved. From this state:
        ///    - <see cref="Stop"/> can be called to end the scan, transitioning to the *Stopped* state.
        ///    - Automatically transitions to the "Saved" state when done.
        Saving,

        /// The scan has been saved. From this state:
        ///    - <see cref="Stop"/> can be called to end the scan, transitioning to the *Stopped* state.
        Saved,

        /// The scan is currently being discarded. From this state:
        ///    - <see cref="Stop"/> can be called to end the scan, transitioning to the *Stopped* state.
        ///    - Automatically transitions to the "Discarded" state when done.
        Discarding,

        /// The scan has been discarded. From this state:
        ///    - <see cref="Stop"/> can be called to end the scan, transitioning to the *Stopped* state.
        Discarded,

        /// Scanning is stopped. From this state:
        ///    - <see cref="Start"/> can be called to re-start scanning, transitioning to the *Started* state.
        Stopped,

        /// Scan processing has failed. From this state:
        ///    - <see cref="Stop"/> can be called to reset the scanner to the *Stopped* state.
        Error = 8,
    }
}
