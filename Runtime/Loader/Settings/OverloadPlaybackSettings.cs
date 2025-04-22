// Copyright 2022-2025 Niantic.

using System;

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

        [Obsolete]
        public uint NumberOfIterations
        {
            get {return _numberOfIterations; }
            set { _numberOfIterations = value; }
        }

        private int _startFrame = 0;
        public int StartFrame
        {
            get { return _startFrame; }
            set { _startFrame = value; }
        }

        private int _endFrame = -1;
        public int EndFrame
        {
            get { return _endFrame; }
            set { _endFrame = value; }
        }

        internal OverloadPlaybackSettings()
        {
        }

        internal OverloadPlaybackSettings(ILightshipPlaybackSettings source)
        {
            UsePlayback = source.UsePlayback;
            PlaybackDatasetPath = source.PlaybackDatasetPath;
            RunManually = source.RunManually;
            LoopInfinitely = source.LoopInfinitely;
            StartFrame = source.StartFrame;
            EndFrame = source.EndFrame;
        }
    }
}
