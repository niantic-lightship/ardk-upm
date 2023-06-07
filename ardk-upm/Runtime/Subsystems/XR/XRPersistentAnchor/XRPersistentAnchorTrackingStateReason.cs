// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;

namespace Niantic.Lightship.AR.Subsystems
{
    public enum TrackingStateReason : UInt32
    {
        None = 0,
        Removed = 1,
        AnchorTooFar = 2,
        PermissionDenied = 3
    }
}
