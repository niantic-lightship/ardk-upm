// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.ObjectDetection
{
    /// <summary>
    /// A structure for information about the object detection model that's become ready. This is used to
    /// communicate information in the <see cref="ARObjectDetectionManager.MetadataInitialized" /> event.
    /// </summary>
    /// <remarks>
    /// This is an experimental API. Experimental features are subject to breaking changes,
    /// not officially supported, and may be deprecated without notice.
    /// </remarks>
    [PublicAPI]
    public struct ARObjectDetectionModelEventArgs: IEquatable<ARObjectDetectionModelEventArgs>
    {
        /// <summary>
        /// The names of all the categories that the currently active object detection model is able to detect.
        /// </summary>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public IReadOnlyList<string> CategoryNames { get; internal set; }

        /// <summary>
        /// Generates a hash suitable for use with containers like `HashSet` and `Dictionary`.
        /// </summary>
        /// <returns>A hash code generated from this object's fields.</returns>
        public override int GetHashCode() => CategoryNames.GetHashCode();

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="obj">The `object` to compare against.</param>
        /// <returns>`True` if <paramref name="obj"/> is of type <see cref="ARObjectDetectionModelEventArgs"/> and
        /// <see cref="Equals(ARObjectDetectionModelEventArgs)"/> also returns `true`; otherwise `false`.</returns>
        public override bool Equals(object obj)
            => obj is ARObjectDetectionModelEventArgs && Equals((ARObjectDetectionModelEventArgs)obj);

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="ARObjectDetectionModelEventArgs"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="ARObjectDetectionModelEventArgs"/>, otherwise false.</returns>
        public bool Equals
            (ARObjectDetectionModelEventArgs other) =>
            (CategoryNames == null ? other.CategoryNames == null : CategoryNames.SequenceEqual(other.CategoryNames));

        /// <summary>
        /// Tests for equality. Same as <see cref="Equals(ARObjectDetectionModelEventArgs)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator ==(ARObjectDetectionModelEventArgs lhs, ARObjectDetectionModelEventArgs rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Tests for inequality. Same as `!`<see cref="Equals(ARObjectDetectionModelEventArgs)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is not equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator !=(ARObjectDetectionModelEventArgs lhs, ARObjectDetectionModelEventArgs rhs) => !lhs.Equals(rhs);
    }
}
