// Copyright 2022-2025 Niantic.

namespace Niantic.Lightship.AR.API
{
    // Mirrors the native ArdkStatus struct
    internal enum ArdkStatus
    {
        Ok = 0,
        NullArgument,
        InvalidArgument,
        InvalidOperation,
        NullArdkHandle,
        FeatureDoesNotExist,
        FeatureAlreadyExists,
        NoData,
        InternalError
    }
}
