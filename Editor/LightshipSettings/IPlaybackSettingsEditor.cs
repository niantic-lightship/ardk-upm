// Copyright 2022-2024 Niantic.

using UnityEditor;

namespace Niantic.Lightship.AR.Editor
{
    public interface IPlaybackSettingsEditor
    {
        void InitializeSerializedProperties(SerializedObject lightshipSettings);
        void DrawGUI();
    }
}
