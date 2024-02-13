// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Reports the status of a localization request (may contain multiple client -> server requests)
    /// A limited localization means that localization returned a value, but there
    ///     is not enough confidence to provide a meaningful pose.
    /// </summary>
    [PublicAPI]
    public enum LocalizationStatus : byte
    {
        Unknown = 0,
        Failure,
        Limited,
        Success,
    }

    /// <summary>
    /// Reports the result of a localization request.
    /// </summary>
    [PublicAPI]
    public struct XRPersistentAnchorLocalizationStatus
    {
        /// <summary>
        /// NodeId that this request was targeting
        /// </summary>
        public Guid NodeId;
        
        /// <summary>
        /// Status of the localization request
        /// </summary>
        public LocalizationStatus Status;
        
        /// <summary>
        /// Confidence of the localization.
        /// @note: Different algorithms may have different confidence scales. Confidences are a
        ///     general guideline for now, until confidence scales are normalized, or the
        ///     algorithm surfaced
        /// </summary>
        public float LocalizationConfidence;

        /// <summary>
        /// Frame Id corresponding to frame sent used in Localization
        /// </summary>
        public UInt64 FrameId;
    }
}
