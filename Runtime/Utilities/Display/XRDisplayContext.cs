// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Subsystems.Playback;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Utilities
{
    /// <summary>
    /// Container for data relevant to rendering in relation to the XR camera's display
    /// </summary>
    [PublicAPI]
    public static class XRDisplayContext
    {
        private const float DefaultOccludeeEyeDepth = 5f;

        /// Linear eye-depth from the camera to the occludee
        public static float OccludeeEyeDepth = DefaultOccludeeEyeDepth;

        internal static void ResetOccludee()
        {
            OccludeeEyeDepth = DefaultOccludeeEyeDepth;
        }

        // TODO [ARDK-2596]:
        // Refactor occlusion feature's blit and warp methods to not require camera image resolution input from C#.
        internal static bool TryGetCameraImageAspectRatio(out float aspectRatio)
        {
            XRCameraSubsystem cameraSubsystem = null;
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                var loader = XRGeneralSettings.Instance.Manager.activeLoader;
                if (loader != null)
                    cameraSubsystem = loader.GetLoadedSubsystem<XRCameraSubsystem>();
            }

            if (cameraSubsystem == null)
            {
                aspectRatio = 0f;
                return false;
            }

            if (cameraSubsystem.TryGetIntrinsics(out var cameraIntrinsics))
            {
                aspectRatio = cameraIntrinsics.resolution.x / (float)cameraIntrinsics.resolution.y;
                return true;
            }

            aspectRatio = 0f;
            return false;
        }

        /// <summary>
        /// For Lightship, it is important to know the screen orientation in order to know how to rotate the camera
        /// input image received from the XRCameraSubsystem. The UnityEngine.Screen.orientation property only returns
        /// ScreenOrientation.Portrait when called in Editor, presumably because there is no rotation offset
        /// between the camera image and the displayed screen image in Editor. Thus, use this method to get the
        /// screen orientation value expected by Lightship's APIs, regardless of the active platform.
        /// </summary>
        /// <returns>The corrected screen orientation. When running XR in Lightship's Playback mode, the
        /// returned value will match the screen orientation recorded in the dataset's current frame. Else, will
        /// return the UnityEngine.Screen.orientation value.</returns>
        public static ScreenOrientation GetScreenOrientation()
        {
#if !UNITY_EDITOR && UNITY_ANDROID && (NIANTIC_LIGHTSHIP_SPACES_ENABLED || NIANTIC_LIGHTSHIP_ML2_ENABLED)
            //TODO [ARDK-2593]: Fix this properly to account for head tilt
            return ScreenOrientation.LandscapeLeft;
#endif

            // If using Playback, get the recorded screen orientation
            if (XRGeneralSettings.Instance != null
                && XRGeneralSettings.Instance.Manager != null
                && XRGeneralSettings.Instance.Manager.isInitializationComplete
                && XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRSessionSubsystem>()
                    is LightshipPlaybackSessionSubsystem)
            {
                var deviceOrientation = InputReader.GetDeviceOrientation();
                switch (deviceOrientation)
                {
                    case DeviceOrientation.Portrait:
                        return ScreenOrientation.Portrait;
                    case DeviceOrientation.LandscapeLeft:
                        return ScreenOrientation.LandscapeLeft;
                    case DeviceOrientation.LandscapeRight:
                        return ScreenOrientation.LandscapeRight;
                    case DeviceOrientation.PortraitUpsideDown:
                        return ScreenOrientation.PortraitUpsideDown;
                }
            }
            // Else (aka if Simulation)
            else if (Application.isEditor)
            {
                return GameViewUtils.GetEditorScreenOrientation();
            }

            // If on device, or if Playback provided no orientation, use the Screen.orientation value
            return Screen.orientation;
        }
    }
}
