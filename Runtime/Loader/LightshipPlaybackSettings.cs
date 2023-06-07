using System;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.Serialization;

namespace Niantic.Lightship.AR.Loader
{
    /// <summary>
    /// Settings to control Playback behaviour (at runtime).
    /// </summary>
    public partial class LightshipSettings
    {
        [SerializeField]
        private bool _usePlaybackOnEditor;

        public bool UsePlaybackOnEditor => _usePlaybackOnEditor;

        [SerializeField]
        private bool _usePlaybackOnDevice;

        public bool UsePlaybackOnDevice => _usePlaybackOnDevice;

        [SerializeField]
        private string _playbackDatasetPathEditor;

        public string PlaybackDatasetPathEditor => _playbackDatasetPathEditor;


        [SerializeField]
        private string _playbackDatasetPathDevice;

        public string PlaybackDatasetPathDevice => _playbackDatasetPathDevice;

        [SerializeField]
        [Tooltip(
            "Use the space bar to move forward frame by frame.")]
        private bool _runPlaybackManuallyEditor;

        public bool RunManuallyEditor => _runPlaybackManuallyEditor;

        [SerializeField]
        [Tooltip(
            "Use two-finger touch to move forward frame by frame.")]
        private bool _runPlaybackManuallyDevice;

        public bool RunManuallyDevice => _runPlaybackManuallyDevice;
    }
}
