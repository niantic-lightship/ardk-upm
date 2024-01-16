// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.Semantics
{
    /// <summary>
    /// A structure for information about the semantic segmentation model that's become ready. This is used to
    /// communicate information in the <see cref="ARSemanticSegmentationManager.MetadataInitialized" /> event.
    /// </summary>
    [PublicAPI]
    public struct ARSemanticSegmentationModelEventArgs : IEquatable<ARSemanticSegmentationModelEventArgs>
    {
        /// <summary>
        /// The semantic channels detected by the semantic segmentation model.
        /// </summary>
        public IReadOnlyList<string> ChannelNames { get; internal set; }

        /// <summary>
        /// The indices of the semantic channels detected by the semantic segmentation model.
        /// </summary>
        public IReadOnlyDictionary<string, int> ChannelIndices { get; internal set; }

        /// <summary>
        /// Generates a hash suitable for use with containers like `HashSet` and `Dictionary`.
        /// </summary>
        /// <returns>A hash code generated from this object's fields.</returns>
        public override int GetHashCode() =>
            HashCode.Combine(ChannelNames.GetHashCode(), ChannelIndices.GetHashCode());

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="obj">The `object` to compare against.</param>
        /// <returns>`True` if <paramref name="obj"/> is of type <see cref="ARSemanticSegmentationModelEventArgs"/> and
        /// <see cref="Equals(ARSemanticSegmentationModelEventArgs)"/> also returns `true`; otherwise `false`.</returns>
        public override bool Equals(object obj)
            => obj is ARSemanticSegmentationModelEventArgs && Equals((ARSemanticSegmentationModelEventArgs)obj);

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="ARSemanticSegmentationModelEventArgs"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="ARSemanticSegmentationModelEventArgs"/>, otherwise false.</returns>
        public bool Equals(ARSemanticSegmentationModelEventArgs other)
            => (ChannelNames == null ? other.ChannelNames == null : ChannelNames.Equals(other.ChannelNames))
                &&  (ChannelIndices == null ? other.ChannelIndices == null : ChannelIndices.Equals(other.ChannelIndices));

        /// <summary>
        /// Tests for equality. Same as <see cref="Equals(ARSemanticSegmentationModelEventArgs)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator ==(ARSemanticSegmentationModelEventArgs lhs, ARSemanticSegmentationModelEventArgs rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Tests for inequality. Same as `!`<see cref="Equals(ARSemanticSegmentationModelEventArgs)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is not equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator !=(ARSemanticSegmentationModelEventArgs lhs, ARSemanticSegmentationModelEventArgs rhs) => !lhs.Equals(rhs);
    }
}
