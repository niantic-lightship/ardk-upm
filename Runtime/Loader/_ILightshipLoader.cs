using Niantic.Lightship.AR.Playback;

namespace Niantic.Lightship.AR.Loader
{
    internal interface _ILightshipLoader
    {
        internal _PlaybackDatasetReader PlaybackDatasetReader { get; }

        internal bool InitializeWithSettings(LightshipSettings settings, bool isTest = false);
    }
}
