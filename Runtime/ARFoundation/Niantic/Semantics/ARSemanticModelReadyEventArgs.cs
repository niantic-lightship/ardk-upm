// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;

namespace Niantic.Lightship.AR.ARFoundation
{
    /// <summary>
    /// A structure for information about the semantic segmentation model that's become ready. This is used to
    /// communicate information in the <see cref="ARSemanticSegmentationManager.SemanticModelIsReady" /> event.
    /// </summary>
    public struct ARSemanticModelReadyEventArgs : IEquatable<ARSemanticModelReadyEventArgs>
    {
        /// <summary>
        /// The semantic channels detected by the semantic segmentation model.
        /// </summary>
        public List<string> SemanticChannelNames { get; internal set; }

        /// <summary>
        /// Generates a hash suitable for use with containers like `HashSet` and `Dictionary`.
        /// </summary>
        /// <returns>A hash code generated from this object's fields.</returns>
        public override int GetHashCode() => SemanticChannelNames.GetHashCode();

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="obj">The `object` to compare against.</param>
        /// <returns>`True` if <paramref name="obj"/> is of type <see cref="ARSemanticModelReadyEventArgs"/> and
        /// <see cref="Equals(ARSemanticModelReadyEventArgs)"/> also returns `true`; otherwise `false`.</returns>
        public override bool Equals(object obj)
            => obj is ARSemanticModelReadyEventArgs && Equals((ARSemanticModelReadyEventArgs)obj);

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="ARSemanticModelReadyEventArgs"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="ARSemanticModelReadyEventArgs"/>, otherwise false.</returns>
        public bool Equals(ARSemanticModelReadyEventArgs other)
            => SemanticChannelNames == null ? other.SemanticChannelNames == null : SemanticChannelNames.Equals(other.SemanticChannelNames);

        /// <summary>
        /// Tests for equality. Same as <see cref="Equals(ARSemanticModelReadyEventArgs)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator ==(ARSemanticModelReadyEventArgs lhs, ARSemanticModelReadyEventArgs rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Tests for inequality. Same as `!`<see cref="Equals(ARSemanticModelReadyEventArgs)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is not equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator !=(ARSemanticModelReadyEventArgs lhs, ARSemanticModelReadyEventArgs rhs) => !lhs.Equals(rhs);
    }
}
