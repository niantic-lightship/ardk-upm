// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.Utilities;
using UnityEngine.SubsystemsImplementation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Constructor parameters for the <see cref="XRSemanticsSubsystemDescriptor"/>.
    /// </summary>
    [PublicAPI]
    public struct XRSemanticsSubsystemCinfo : IEquatable<XRSemanticsSubsystemCinfo>
    {
        /// <summary>
        /// Specifies an identifier for the provider implementation of the subsystem.
        /// </summary>
        /// <value>
        /// The identifier for the provider implementation of the subsystem.
        /// </value>
        public string id { get; set; }

        /// <summary>
        /// Specifies the provider implementation type to use for instantiation.
        /// </summary>
        /// <value>
        /// The provider implementation type to use for instantiation.
        /// </value>
        public Type providerType { get; set; }

        /// <summary>
        /// Specifies the <c>XRAnchorSubsystem</c>-derived type that forwards casted calls to its provider.
        /// </summary>
        /// <value>
        /// The type of the subsystem to use for instantiation. If null, <c>XRAnchorSubsystem</c> will be instantiated.
        /// </value>
        public Type subsystemTypeOverride { get; set; }


        /// <summary>
        /// Specifies if the current subsystem supports semantics segmentation image.
        /// </summary>
        public Func<Supported> semanticSegmentationImageSupportedDelegate { get; set; }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="XRSemanticsSubsystemCinfo"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="XRSemanticsSubsystemCinfo"/>, otherwise false.</returns>
        public bool Equals(XRSemanticsSubsystemCinfo other)
        {
            return
                ReferenceEquals(id, other.id)
                && ReferenceEquals(providerType, other.providerType)
                && ReferenceEquals(subsystemTypeOverride, other.subsystemTypeOverride)
                && semanticSegmentationImageSupportedDelegate == other.semanticSegmentationImageSupportedDelegate;
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="obj">The `object` to compare against.</param>
        /// <returns>`True` if <paramref name="obj"/> is of type <see cref="XRSemanticsSubsystemCinfo"/> and
        /// <see cref="Equals(XRSemanticsSubsystemCinfo)"/> also returns `true`; otherwise `false`.</returns>
        public override bool Equals(System.Object obj) => ((obj is XRSemanticsSubsystemCinfo) && Equals((XRSemanticsSubsystemCinfo)obj));

        /// <summary>
        /// Tests for equality. Same as <see cref="Equals(XRSemanticsSubsystemCinfo)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator ==(XRSemanticsSubsystemCinfo lhs, XRSemanticsSubsystemCinfo rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Tests for inequality. Same as `!`<see cref="Equals(XRSemanticsSubsystemCinfo)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is not equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator !=(XRSemanticsSubsystemCinfo lhs, XRSemanticsSubsystemCinfo rhs) => !lhs.Equals(rhs);

        /// <summary>
        /// Generates a hash suitable for use with containers like `HashSet` and `Dictionary`.
        /// </summary>
        /// <returns>A hash code generated from this object's fields.</returns>
        public override int GetHashCode()
        {
            int hashCode = 486187739;
            unchecked
            {
                hashCode = (hashCode * 486187739) + id.GetHashCode();
                hashCode = (hashCode * 486187739) + providerType.GetHashCode();
                hashCode = (hashCode * 486187739) + subsystemTypeOverride.GetHashCode();
                hashCode = (hashCode * 486187739) + semanticSegmentationImageSupportedDelegate.GetHashCode();
            }
            return hashCode;
        }
    }

    /// <summary>
    /// Descriptor for the XRSemanticsSubsystem.
    /// </summary>
    [PublicAPI]
    public class XRSemanticsSubsystemDescriptor :
        SubsystemDescriptorWithProvider<XRSemanticsSubsystem, XRSemanticsSubsystem.Provider>
    {
        private XRSemanticsSubsystemDescriptor(XRSemanticsSubsystemCinfo semanticsSubsystemCinfo)
        {
            id = semanticsSubsystemCinfo.id;
            providerType = semanticsSubsystemCinfo.providerType;
            subsystemTypeOverride = semanticsSubsystemCinfo.subsystemTypeOverride;
            m_SemanticSegmentationImageSupportedDelegate = semanticsSubsystemCinfo.semanticSegmentationImageSupportedDelegate;
        }

        /// <summary>
        /// Query for whether semantic segmentation is supported.
        /// </summary>
        private Func<Supported> m_SemanticSegmentationImageSupportedDelegate;

        /// <summary>
        /// (Read Only) Whether the subsystem supports semantic segmentation image.
        /// </summary>
        /// <remarks>
        /// The supported status might take time to determine. If support is still being determined, the value will be <see cref="Supported.Unknown"/>.
        /// </remarks>
        public Supported semanticSegmentationImageSupported
        {
            get
            {
                if (m_SemanticSegmentationImageSupportedDelegate != null)
                {
                    return m_SemanticSegmentationImageSupportedDelegate();
                }

                return Supported.Unknown;
            }
        }


        /// <summary>
        /// Creates the semantics subsystem descriptor from the construction info.
        /// </summary>
        /// <param name="semanticsSubsystemCinfo">The semantics subsystem descriptor constructor information.</param>
        internal static XRSemanticsSubsystemDescriptor Create(XRSemanticsSubsystemCinfo semanticsSubsystemCinfo)
        {
            if (string.IsNullOrEmpty(semanticsSubsystemCinfo.id))
            {
                throw new ArgumentException("Cannot create semantics subsystem descriptor because id is invalid",
                                            nameof(semanticsSubsystemCinfo));
            }

            if (semanticsSubsystemCinfo.providerType == null
                || !semanticsSubsystemCinfo.providerType.IsSubclassOf(typeof(XRSemanticsSubsystem.Provider)))
            {
                throw new ArgumentException("Cannot create semantics subsystem descriptor because providerType is invalid",
                                            nameof(semanticsSubsystemCinfo));
            }

            if (semanticsSubsystemCinfo.subsystemTypeOverride == null
                || !semanticsSubsystemCinfo.subsystemTypeOverride.IsSubclassOf(typeof(XRSemanticsSubsystem)))
            {
                throw new ArgumentException("Cannot create semantics subsystem descriptor because subsystemTypeOverride is invalid",
                                            nameof(semanticsSubsystemCinfo));
            }

            return new XRSemanticsSubsystemDescriptor(semanticsSubsystemCinfo);
        }
    }
}
