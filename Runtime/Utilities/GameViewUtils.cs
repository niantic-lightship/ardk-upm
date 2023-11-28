// Copyright 2022-2023 Niantic.
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    public static class GameViewUtils
    {
        public static ScreenOrientation GetGameViewAspectRatio(XRCameraParams cameraParams)
        {
#if UNITY_EDITOR
            var aspectRatio = CameraEditorUtils.GameViewAspectRatio;
            return aspectRatio >= 1.0 ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
#else
            return cameraParams.screenOrientation;
#endif
        }
    }
}