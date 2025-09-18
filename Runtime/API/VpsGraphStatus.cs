// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.API
{
    /// <summary>
    /// Status codes returned by VPS graph-based functions.
    /// </summary>
    [Experimental]
    public enum VpsGraphStatus
    {
        /// <summary>
        /// Unknown status or uninitialized state.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The function executed successfully
        /// </summary>
        Success,

        /// <summary>
        /// Invalid arguments were provided to the function.
        /// </summary>
        InvalidArgument,

        /// <summary>
        /// The operation is invalid for the current VPS state (not localized, no connected cloud nodes, missing transform graph).
        /// </summary>
        InvalidOperation,

        /// <summary>
        /// Required VPS feature or subsystem does not exist or is not available.
        /// </summary>
        FeatureUnavailable,

        /// <summary>
        /// No reference data is available (missing georeference data or no cloud map nodes found).
        /// </summary>
        NoData
    }
}
