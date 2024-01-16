// Copyright 2022-2024 Niantic.

using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    public class OverloadPlaybackSettings: ILightshipPlaybackSettings
    {
        private bool _usePlayback;

        public bool UsePlayback
        {
            get { return _usePlayback; }
            set { _usePlayback = value; }
        }

        private string _playbackDatasetPath;

        public string PlaybackDatasetPath
        {
            get { return _playbackDatasetPath; }
            set { _playbackDatasetPath = value; }
        }

        private bool _runPlaybackManually;

        public bool RunManually
        {
            get { return _runPlaybackManually; }
            set { _runPlaybackManually = value; }
        }

        private bool _loopInfinitely = false;

        public bool LoopInfinitely
        {
            get { return _loopInfinitely; }
            set { _loopInfinitely = value; }
        }

        private uint _numberOfIterations = 1;

        public uint NumberOfIterations
        {
            get {return _numberOfIterations; }
            set { _numberOfIterations = value; }
        }
    }
}
