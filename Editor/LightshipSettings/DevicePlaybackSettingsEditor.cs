// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Loader;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    public class DevicePlaybackSettingsEditor: BasePlaybackSettingsEditor
    {
        protected override ILightshipPlaybackSettings PlaybackSettings => LightshipSettings.Instance.DevicePlaybackSettings;
    }
}
