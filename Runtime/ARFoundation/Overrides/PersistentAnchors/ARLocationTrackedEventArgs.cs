// Copyright 2022-2024 Niantic.
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;

namespace Niantic.Lightship.AR.PersistentAnchors
{
    /// <summary>
    /// Event arguments for the <see cref="ARLocationManager.locationTrackingStateChanged"/> event.
    /// </summary>
    [PublicAPI]
    public struct ARLocationTrackedEventArgs
    {
        /// <summary>
        /// The ARLocation being tracked
        /// </summary>
        public ARLocation ARLocation { get; }

        /// <summary>
        /// Whether or not the ARLocation is currently tracked
        /// </summary>
        public bool Tracking { get; }

        public ARLocationTrackingStateReason TrackingStateReason { get; }

        /// <summary>
        /// Creates the args for ARLocation tracking
        /// </summary>
        /// <param name="arARLocation">The ARLocation to track</param>
        /// <param name="tracking">Whether or not the ARLocation is being tracked</param>
        /// <param name="reason">If tracking is false, more information about why</param>
        public ARLocationTrackedEventArgs(ARLocation arARLocation, bool tracking, ARLocationTrackingStateReason reason)
        {
            ARLocation = arARLocation;
            Tracking = tracking;
            TrackingStateReason = reason;
        }
    }
}
