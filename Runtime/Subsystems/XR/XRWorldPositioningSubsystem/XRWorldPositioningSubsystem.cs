// Copyright 2023-2024 Niantic.

using System;

using UnityEngine;
using UnityEngine.SubsystemsImplementation;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// The <c>WorldPositioningStatus</c> represents the status of the AR to world transform.  The transform
    /// should only be used when the status is <c>Available</c>.
    /// </summary>
    /// <value><c>Available</c> indicates that the World Positioning transform is valid and ready to use</value>
    /// <value><c>NoGnss</c> indicates that the device doesn't have sufficient up-to-date GNSS measurements</value>
    /// <value><c>TrackingFailed</c> indicates that the device tracking has failed</value>
    /// <value><c>NoHeading</c> indicates that the device heading is unavailable</value>
    /// <value><c>Initializing</c> indicates that the system in preparing to produce the first transform</value>
    /// <value><c>SubsystemNotRunning</c> indicates that the XRWorldPositioningSubsystem is not currently running</value>
    public enum WorldPositioningStatus
    {
        Available = 0,
        NoGnss = 1,
        TrackingFailed = 2,
        NoHeading = 3,
        Initializing = 4,
        SubsystemNotRunning = 5
    };

    /// <summary>
    /// Defines an interface for interacting with World Space functionality.
    /// </summary>
    /// <remarks>
    /// A World Space subsystem provides the ability to convert between the local
    /// </remarks>
    public class XRWorldPositioningSubsystem
        : SubsystemWithProvider<XRWorldPositioningSubsystem, XRWorldPositioningSubsystemDescriptor,
            XRWorldPositioningSubsystem.Provider>
    {
        /// <summary>
        /// Construct the subsystem by creating the functionality provider.
        /// </summary>
        public XRWorldPositioningSubsystem()
        {
        }

        /// <summary>
        /// Gets the latest transform between XROrigin coordinates and World Space coordinates.
        /// </summary>
        /// <remarks>
        /// The transform converts from Right-Up-Forwards XROrigin coordinates to a Euclidean world coordinate system
        /// with axes aligned to the East-Up-North directions close to the device and measured in metres.   The world
        /// coordinates are measured relative to a origin which may occassionaly move to ensure that single-precision
        /// coordinates provide sufficient precision for working with objects around the player.
        /// </remarks>
        /// <param name="arToWorld">The transform from XROrigin coordinates to world coordinates</param>
        /// <param name="originLatitude">The latitude of the world coordinates origin.</param>
        /// <param name="originLongitude">The ongitude of the world coordinates origin</param>
        /// <param name="originAltitude">The altitude of the world coordinates origin</param>
        /// <returns>
        /// <c>true</c> If the arToWorld transform is valid. Otherwise,
        /// <c>false</c>.  The transform will be briefly unavailable at the start of a session and following a
        /// tracking failure.  Under normal conditions it will be available within one second, but under some
        /// conditions it might take up to ten seconds for the transform to become valid.
        /// </returns>
        public WorldPositioningStatus TryGetXRToWorld
        (
            ref Matrix4x4 arToWorld,
            ref double originLatitude,
            ref double originLongitude,
            ref double originAltitude
        ) =>
            provider.TryGetXRToWorld
                (ref arToWorld, ref originLatitude, ref originLongitude, ref originAltitude);

        /// <summary>
        /// The provider which will service the <see cref="XRWorldPositioningSubsystem"/>.
        /// </summary>
        public abstract class Provider : SubsystemProvider<XRWorldPositioningSubsystem>
        {
            /// <summary>
            /// Gets the latest transform between XROrigin coordinates and World Space coordinates.
            /// </summary>
            /// <returns>
            /// <c>true</c> If the arToWorld transform is valid. Otherwise,
            /// <c>false</c>.
            /// </returns>
            public virtual WorldPositioningStatus TryGetXRToWorld
            (
                ref Matrix4x4 arToWorld,
                ref double originLatitude,
                ref double originLongitude,
                ref double originAltitude
            ) =>
                throw new NotSupportedException
                    ("TryGetXRToWorld is not supported by this implementation");
        }
    }
}
