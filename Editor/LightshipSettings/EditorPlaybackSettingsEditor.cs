// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Loader;

namespace Niantic.Lightship.AR.Editor
{
    public class EditorPlaybackSettingsEditor : BasePlaybackSettingsEditor
    {
        protected override ILightshipPlaybackSettings PlaybackSettings => LightshipSettings.Instance.EditorPlaybackSettings;

        /// <summary>
        /// Override to prevent EditorPlaybackSettings from being marked dirty and persisted.
        /// Editor settings are meant to be transient and session-specific.
        /// </summary>
        protected override void MarkSettingsDirty()
        {
            // Intentionally do nothing - EditorPlaybackSettings should not be persisted
        }
    }
}
