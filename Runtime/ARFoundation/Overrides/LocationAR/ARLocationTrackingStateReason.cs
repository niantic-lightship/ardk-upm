// Copyright 2022-2024 Niantic.

using System;

namespace Niantic.Lightship.AR.LocationAR
{
    public enum ARLocationTrackingStateReason : UInt32
    {
        Unknown = 0,
        None = 1,
        Limited = 2,
        Removed = 3,
    }
}
