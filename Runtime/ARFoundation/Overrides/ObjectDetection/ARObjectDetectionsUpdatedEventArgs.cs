// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;

namespace Niantic.Lightship.AR.ObjectDetection
{
    /// <summary>
    /// A structure for information about the latest object detections that have been surfaced. This is used to
    /// communicate information in the <see cref="ARObjectDetectionManager.ObjectDetectionsUpdated" /> event.
    /// </summary>
    /// <remarks>
    /// This is an experimental API. Experimental features are subject to breaking changes,
    /// not officially supported, and may be deprecated without notice.
    /// </remarks>
    [PublicAPI]
    public struct ARObjectDetectionsUpdatedEventArgs: IEquatable<ARObjectDetectionsUpdatedEventArgs>
    {
        /// <summary>
        /// The list of objects detected in the latest input camera frame.
        /// </summary>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public IReadOnlyList<XRDetectedObject> Results;

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="obj">The `object` to compare against.</param>
        /// <returns>`True` if <paramref name="obj"/> is of type <see cref="ARObjectDetectionsUpdatedEventArgs"/> and
        /// <see cref="Equals(ARObjectDetectionsUpdatedEventArgs)"/> also returns `true`; otherwise `false`.</returns>
        public override bool Equals(object obj) => obj is ARObjectDetectionsUpdatedEventArgs other && Equals(other);

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="ARObjectDetectionsUpdatedEventArgs"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="ARObjectDetectionsUpdatedEventArgs"/>, otherwise false.</returns>
        public bool Equals(ARObjectDetectionsUpdatedEventArgs other) => Results.SequenceEqual(other.Results);

        /// <summary>
        /// Generates a hash suitable for use with containers like `HashSet` and `Dictionary`.
        /// </summary>
        /// <returns>A hash code generated from this object's fields.</returns>
        public override int GetHashCode() => Results.GetHashCode();
    }
}
