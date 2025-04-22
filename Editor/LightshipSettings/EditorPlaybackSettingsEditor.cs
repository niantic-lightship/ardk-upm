// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Loader;

namespace Niantic.Lightship.AR.Editor
{
    public class EditorPlaybackSettingsEditor : BasePlaybackSettingsEditor
    {
        protected override ILightshipPlaybackSettings PlaybackSettings => LightshipSettings.Instance.EditorPlaybackSettings;
    }
}
