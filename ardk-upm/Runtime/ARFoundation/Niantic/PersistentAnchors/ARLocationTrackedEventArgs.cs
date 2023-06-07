using System;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// Event arguments for the <see cref="ARLocationManager.locationTrackingStateChanged"/> event.
    /// </summary>
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

        /// <summary>
        /// Creates the args for ARLocation tracking
        /// </summary>
        /// <param name="arARLocation">The ARLocation to track</param>
        /// <param name="tracking">Whether or not the ARLocation is being tracked</param>
        public ARLocationTrackedEventArgs(ARLocation arARLocation, bool tracking)
        {
            ARLocation = arARLocation;
            Tracking = tracking;
        }
    }
}
