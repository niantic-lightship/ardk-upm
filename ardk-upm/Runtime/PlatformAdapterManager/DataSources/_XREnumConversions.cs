using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR
{
    internal static class _XREnumConversions
    {
        // Translates ARFoundation's TrackingState enum into the corresponding Lightship tracking state values
        // defined in tracking_state.h
        // Note: ARFoundation has no "Failed" state corresponding to Lightship's
        public static uint FromUnityToArdk(this TrackingState state)
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

        // Translates Unity Input's DeviceOrientation enum into the corresponding Lightship values
        // defined in orientation.h
        public static uint FromUnityToArdk(this DeviceOrientation orientation)
        {
            switch (orientation)
            {
                case DeviceOrientation.Unknown:
                    return 0; // Unknown
                case DeviceOrientation.Portrait:
                    return 1; // Portrait
                case DeviceOrientation.PortraitUpsideDown:
                    return 2; // PortraitUpsideDown
                case DeviceOrientation.LandscapeLeft:
                    return 4; // LandscapeLeft
                case DeviceOrientation.LandscapeRight:
                    return 3; // LandscapeRight
                //NOTE: we default to portrait orientation in face down/up mode because we don't
                //have enough information about the last valid orientation.
                //TODO: Implement a better way to track the last valid orientation.
                case DeviceOrientation.FaceUp:
                    return 1; // Portrait
                case DeviceOrientation.FaceDown:
                    return 1; // Portrait
                default:
                    return 0;
            }
        }
    }
}
