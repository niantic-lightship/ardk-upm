// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.PAM;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    internal static class XREnumConversions
    {
        // Translates ARFoundation's TrackingState enum into the corresponding Lightship tracking state values
        // defined in tracking_state.h
        // Note: ARFoundation has no "Failed" state corresponding to Lightship's
        public static byte FromUnityToArdk(this TrackingState state)
        {
            switch (state)
            {
                case TrackingState.None:
                    return 0; // Unknown
                case TrackingState.Limited:
                    return 2; // Poor
                case TrackingState.Tracking:
                    return 3; // Normal
                default:
                    return 0;
            }
        }

        // Translates Unity's ScreenOrientation enum into the corresponding Lightship values
        // defined in orientation.h
        public static byte FromUnityToArdk(this ScreenOrientation orientation)
        {
            switch (orientation)
            {
                case ScreenOrientation.Portrait:
                    return 1; // Portrait
                case ScreenOrientation.PortraitUpsideDown:
                    return 2; // PortraitUpsideDown
                case ScreenOrientation.LandscapeLeft:
                    return 4; // LandscapeLeft
                case ScreenOrientation.LandscapeRight:
                    return 3; // LandscapeRight
                default:
                    return 0;
            }
        }
    }
}
