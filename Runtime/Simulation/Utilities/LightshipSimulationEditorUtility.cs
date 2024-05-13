// Copyright 2022-2024 Niantic.
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Niantic.Lightship.AR.Simulation
{
    internal static class LightshipSimulationEditorUtility
    {
        public static float GetGameViewAspectRatio()
        {
#if UNITY_EDITOR
            return CameraEditorUtils.GameViewAspectRatio;
#else
            return 1.0f;
#endif
        }
    }
}
