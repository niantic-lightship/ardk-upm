using Niantic.Lightship.AR.Playback;

namespace Niantic.Lightship.AR.Loader
{
    internal interface ILightshipLoader
    {
        internal PlaybackDatasetReader PlaybackDatasetReader { get; }

        internal bool InitializeWithSettings(LightshipSettings settings, bool isTest = false);
    }
}
