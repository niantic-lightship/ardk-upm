// Copyright 2022-2023 Niantic.

namespace Niantic.Lightship.AR.Loader
{
    public interface ILightshipPlaybackSettings
    {
        bool UsePlayback { get; set; }

        string PlaybackDatasetPath { get; set; }

        bool RunManually { get; set; }

        bool LoopInfinitely { get; set; }
    }
}
