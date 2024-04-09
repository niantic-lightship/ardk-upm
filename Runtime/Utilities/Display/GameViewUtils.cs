// Copyright 2022-2024 Niantic.
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    [PublicAPI]
    public static class GameViewUtils
    {
        /// <summary>
        /// Calculates the screen orientation via the game view aspect ratio. Use this method instead of
        /// UnityEngine.Screen.orientation in Editor, as the latter only returns Portrait in Editor.
        /// </summary>
        /// <returns>The screen orientation. When this method is called in a platform other than the Unity Editor,
        /// it defaults to return ScreenOrientation.Portrait. </returns>
        public static ScreenOrientation GetEditorScreenOrientation()
        {
#if UNITY_EDITOR
            var aspectRatio = CameraEditorUtils.GameViewAspectRatio;
            return aspectRatio >= 1.0 ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
#endif
            return ScreenOrientation.Portrait;
        }

        [Obsolete("Use GetEditorScreenOrientation instead")]
        // Note: Cannot wrap entire class in #if UNITY_EDITOR because this method was originally defined for
        // all platforms.
        public static ScreenOrientation GetGameViewAspectRatio(XRCameraParams cameraParams)
        {
#if UNITY_EDITOR
            return GetEditorScreenOrientation();
#else
            return cameraParams.screenOrientation;
#endif
        }
    }
}

