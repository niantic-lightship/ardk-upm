// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.Utilities;
using UnityEngine.SubsystemsImplementation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Constructor parameters for the <see cref="XRObjectDetectionSubsystemDescriptor"/>.
    /// </summary>
    [PublicAPI]
    public struct XRObjectDetectionSubsystemCinfo : IEquatable<XRObjectDetectionSubsystemCinfo>
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
        public Func<Supported> objectDetectionSupportedDelegate { get; set; }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="XRObjectDetectionSubsystemCinfo"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="XRObjectDetectionSubsystemCinfo"/>, otherwise false.</returns>
        public bool Equals(XRObjectDetectionSubsystemCinfo other)
        {
            return
                ReferenceEquals(id, other.id)
                && ReferenceEquals(providerType, other.providerType)
                && ReferenceEquals(subsystemTypeOverride, other.subsystemTypeOverride)
                && objectDetectionSupportedDelegate == other.objectDetectionSupportedDelegate;
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="obj">The `object` to compare against.</param>
        /// <returns>`True` if <paramref name="obj"/> is of type <see cref="XRObjectDetectionSubsystemCinfo"/> and
        /// <see cref="Equals(XRObjectDetectionSubsystemCinfo)"/> also returns `true`; otherwise `false`.</returns>
        public override bool Equals(System.Object obj) => ((obj is XRObjectDetectionSubsystemCinfo) && Equals((XRObjectDetectionSubsystemCinfo)obj));

        /// <summary>
        /// Tests for equality. Same as <see cref="Equals(XRObjectDetectionSubsystemCinfo)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator ==(XRObjectDetectionSubsystemCinfo lhs, XRObjectDetectionSubsystemCinfo rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Tests for inequality. Same as `!`<see cref="Equals(XRObjectDetectionSubsystemCinfo)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is not equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator !=(XRObjectDetectionSubsystemCinfo lhs, XRObjectDetectionSubsystemCinfo rhs) => !lhs.Equals(rhs);

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
                hashCode = (hashCode * 486187739) + objectDetectionSupportedDelegate.GetHashCode();
            }
            return hashCode;
        }
    }

    /// <summary>
    /// Descriptor for the XRSemanticsSubsystem.
    /// </summary>
    [PublicAPI]
    public class XRObjectDetectionSubsystemDescriptor :
        SubsystemDescriptorWithProvider<XRObjectDetectionSubsystem, XRObjectDetectionSubsystem.Provider>
    {
        private XRObjectDetectionSubsystemDescriptor(XRObjectDetectionSubsystemCinfo cinfo)
        {
            id = cinfo.id;
            providerType = cinfo.providerType;
            subsystemTypeOverride = cinfo.subsystemTypeOverride;
            m_ObjectDetectionSupportedDelegate = cinfo.objectDetectionSupportedDelegate;
        }

        /// <summary>
        /// Query for whether semantic segmentation is supported.
        /// </summary>
        private Func<Supported> m_ObjectDetectionSupportedDelegate;

        /// <summary>
        /// (Read Only) Whether the subsystem supports object detection.
        /// </summary>
        /// <remarks>
        /// The supported status might take time to determine. If support is still being determined, the value will be <see cref="Supported.Unknown"/>.
        /// </remarks>
        public Supported objectDetectionSupported
        {
            get
            {
                if (m_ObjectDetectionSupportedDelegate != null)
                {
                    return m_ObjectDetectionSupportedDelegate();
                }

                return Supported.Unknown;
            }
        }


        /// <summary>
        /// Creates the object detection subsystem descriptor from the construction info.
        /// </summary>
        /// <param name="cinfo">The semantics subsystem descriptor constructor information.</param>
        internal static XRObjectDetectionSubsystemDescriptor Create(XRObjectDetectionSubsystemCinfo cinfo)
        {
            if (string.IsNullOrEmpty(cinfo.id))
            {
                throw new ArgumentException
                (
                    "Cannot create object detection subsystem descriptor because id is invalid",
                    nameof(cinfo)
                );
            }

            if (cinfo.providerType == null || !cinfo.providerType.IsSubclassOf(typeof(XRObjectDetectionSubsystem.Provider)))
            {
                throw new ArgumentException
                (
                    "Cannot create object detection  descriptor because providerType is invalid",
                    nameof(cinfo)
                );
            }

            if (cinfo.subsystemTypeOverride == null || !cinfo.subsystemTypeOverride.IsSubclassOf(typeof(XRObjectDetectionSubsystem)))
            {
                throw new ArgumentException
                (
                    "Cannot create object detection  descriptor because subsystemTypeOverride is invalid",
                    nameof(cinfo)
                );
            }

            return new XRObjectDetectionSubsystemDescriptor(cinfo);
        }
    }
}
