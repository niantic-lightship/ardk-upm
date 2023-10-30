// Copyright 2023 Niantic, Inc. All Rights Reserved.

using UnityEditor;

namespace Niantic.Lightship.AR.Editor
{
    public interface IPlaybackSettingsEditor
    {
        void InitializeSerializedProperties(SerializedObject lightshipSettings);
        void DrawGUI();
    }
}
