// Copyright 2022-2025 Niantic.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    [Serializable]
    public class DevicePlaybackSettings: ILightshipPlaybackSettings
    {
        [SerializeField]
        [Tooltip("When enabled, use a playback dataset on devices instead of the live AR session input.")]
        private bool _usePlayback;

        public bool UsePlayback
        {
            get { return _usePlayback; }
            set { _usePlayback = value; }
        }

        [SerializeField]
        [Tooltip("The absolute path to the folder containing the dataset to use for playback. " +
            "Must be within the StreamingAssets directory to work on-device.")]
        private string _playbackDatasetPath;

        public string PlaybackDatasetPath
        {
            get { return _playbackDatasetPath; }
            set { _playbackDatasetPath = value; }
        }

        [SerializeField]
        [Tooltip("Tap with two fingers to move forward frame by frame.")]
        private bool _runPlaybackManually;

        public bool RunManually
        {
            get { return _runPlaybackManually; }
            set { _runPlaybackManually = value; }
        }

        [SerializeField]
        [Tooltip("When enabled, the playback dataset will play continuously rather than halt at the end. " +
            "To prevent immediate jumps in pose and tracking, the dataset will alternate running forwards and backwards.")]
        private bool _loopInfinitely = false;

        public bool LoopInfinitely
        {
            get { return _loopInfinitely; }
            set { _loopInfinitely = value; }
        }

        [SerializeField]
        [Tooltip("How many times the dataset will be run. It will alternate each loop between going forward and backwards.")]
        private uint _numberOfIterations = 1;

        [Obsolete]
        public uint NumberOfIterations
        {
            get {return _numberOfIterations; }
            set { _numberOfIterations = value; }
        }

        [SerializeField]
        [Tooltip("The frame to start playback from.")]
        private int _startFrame = 0;

        public int StartFrame
        {
            get { return _startFrame; }
            set { _startFrame = value; }
        }

        [SerializeField]
        [Tooltip("The frame to end playback at.")]
        private int _endFrame = -1;

        public int EndFrame
        {
            get { return _endFrame; }
            set { _endFrame = value; }
        }

        internal DevicePlaybackSettings(ILightshipPlaybackSettings source)
        {
            UsePlayback = source.UsePlayback;
            PlaybackDatasetPath = source.PlaybackDatasetPath;
            RunManually = source.RunManually;
            LoopInfinitely = source.LoopInfinitely;
            StartFrame = source.StartFrame;
            EndFrame = source.EndFrame;
        }

        public DevicePlaybackSettings()
        {
        }
    }
}
