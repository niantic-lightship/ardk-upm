// Copyright 2022-2025 Niantic.

using System;

namespace Niantic.Lightship.AR.MapStorageAccess
{
    // TODO: Revisit type of this enum. native expects unsigned 64bit int, but Unity serializes enum as 32 bit int
    [Flags]
    public enum OutputEdgeType : UInt32{
        None               = 0,
        Tracking           = 1 << 0,
        DeviceLocalization = 1 << 1,
        All                = 0xffffffff
    }
}
