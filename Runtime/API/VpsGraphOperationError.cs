// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.API
{
    /// <summary>
    /// Error codes returned by VPS graph-based functions.
    /// </summary>
    [Experimental]
    public enum VpsGraphOperationError
    {
        /// <summary>
        /// Code returned when no error has occurred.
        /// </summary>
        None = 0,

        /// <summary>
        /// Code returned when VPS is not initialized. Make sure the subsystem has been started.
        /// </summary>
        NotInitialized,

        /// <summary>
        /// Code returned when the device is not localized to any VPS location.
        /// </summary>
        NotLocalized,

        /// <summary>
        /// Code returned when the device is not localized to a VPS location that contains the target node specified
        /// for the graph operation.
        /// </summary>
        NoTransformToTrackingNode,

        /// <summary>
        /// Code returned when the VPS location that the device is localized to does not contain the target node
        /// specified for the graph operation.
        /// </summary>
        TargetNodeNotFound,

        /// <summary>
        /// Code returned when the VPS location that the device is localized to contains no nodes with georeference
        /// data. Currently, only publicly available VPS locations have georeference data.
        /// </summary>
        NoGeoreferenceData
    }
}
