// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Provides further information about the tracking state of an anchor.
    /// Query this if the anchor's tracking state is NotTracking
    /// </summary>
    [PublicAPI]
    public enum TrackingStateReason : UInt32
    {
        None = 0,
        Removed = 1,
        AnchorTooFar = 2,
        PermissionDenied = 3
    }
}
