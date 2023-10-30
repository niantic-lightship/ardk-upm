// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;

namespace Niantic.Lightship.AR.XRSubsystems
{
    public enum LocalizationStatus : byte
    {
        Unknown = 0,
        Failure,
        Limited,
        Success,
    }

    public struct XRPersistentAnchorLocalizationStatus
    {
        public Guid NodeId;
        public LocalizationStatus Status;
        public float LocalizationConfidence;
    }
}
