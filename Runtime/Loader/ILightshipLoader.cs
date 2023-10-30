// Copyright 2023 Niantic, Inc. All Rights Reserved.
using Niantic.Lightship.AR.Subsystems.Playback;

namespace Niantic.Lightship.AR.Loader
{
    internal interface ILightshipLoader
    {
        LightshipSettings InitializationSettings { get; set; }

        internal PlaybackDatasetReader PlaybackDatasetReader { get; }

        internal bool InitializeWithSettings(LightshipSettings settings, bool isTest = false);
    }
}
