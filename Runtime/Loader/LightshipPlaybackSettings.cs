// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    [Serializable]
    public class LightshipPlaybackSettings
    {
        [SerializeField]
        private bool _usePlayback;

        public bool UsePlayback
        {
            get { return _usePlayback; }
            set { _usePlayback = value; }
        }

        [SerializeField]
        private string _playbackDatasetPath;

        public string PlaybackDatasetPath
        {
            get { return _playbackDatasetPath; }
            set { _playbackDatasetPath = value; }
        }

        [SerializeField]
        [Tooltip("Use the space bar to move forward frame by frame.")]
        private bool _runPlaybackManually;

        public bool RunManually
        {
            get { return _runPlaybackManually; }
            set { _runPlaybackManually = value; }
        }

        [SerializeField]
        private bool _loopInfinitely = false;

        public bool LoopInfinitely
        {
            get { return _loopInfinitely; }
            set { _loopInfinitely = value; }
        }

        [SerializeField]
        [Tooltip("How many times the dataset will be run. It will alternate each loop between going forward and backwards.")]
        private uint _numberOfIterations = 1;

        public uint NumberOfIterations
        {
            get {return _numberOfIterations; }
            set { _numberOfIterations = value; }
        }
    }
}
