// Copyright 2022-2025 Niantic.

namespace Niantic.Lightship.AR.Loader
{
    public interface ILightshipPlaybackSettings
    {
        bool UsePlayback { get; set; }

        string PlaybackDatasetPath { get; set; }

        bool RunManually { get; set; }

        bool LoopInfinitely { get; set; }

        int StartFrame { get; set; }

        int EndFrame { get; set; }
    }
}
