using System;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// Represents the payload for a persistent anchor.
    /// </summary>
    /// <seealso cref="XRPersistentAnchorPayload"/>
    [Serializable]
    public struct XRPersistentAnchorPayload : IEquatable<XRPersistentAnchorPayload>
    {
        /// <summary>
        /// Constructs the payload data for an anchor from native code.
        /// </summary>
        /// <param name="nativePayloadPtr">A native pointer associated with the anchor payload. The data pointed to by
        /// this pointer is implementation-specific.</param>
        public XRPersistentAnchorPayload(
            IntPtr nativePayloadPtr, int size)
        {
            m_NativePtr = nativePayloadPtr;
            m_Size = size;
        }

        /// <summary>
        /// A native pointer associated with the anchor payload.
        /// The data pointed to by this pointer is implementation-specific.
        /// </summary>
        public IntPtr nativePtr => m_NativePtr;

        /// <summary>
        /// The size of the payload
        /// </summary>
        public int size => m_Size;

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="XRPersistentAnchorPayload"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="XRPersistentAnchorPayload"/>, otherwise false.</returns>
        public bool Equals(XRPersistentAnchorPayload other)
        {
            return m_NativePtr == other.m_NativePtr;
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="obj">The `object` to compare against.</param>
        /// <returns>`True` if <paramref name="obj"/> is of type <see cref="XRPersistentAnchorPayload"/> and
        /// <see cref="Equals(XRPersistentAnchorPayload)"/> also returns `true`; otherwise `false`.</returns>
        public override bool Equals(object obj) =>
            obj is XRPersistentAnchorPayload && Equals((XRPersistentAnchorPayload)obj);

        /// <summary>
        /// Tests for equality. Same as <see cref="Equals(XRPersistentAnchorPayload)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator ==(XRPersistentAnchorPayload lhs, XRPersistentAnchorPayload rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Tests for inequality. Same as `!`<see cref="Equals(XRPersistentAnchorPayload)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is not equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator !=(XRPersistentAnchorPayload lhs, XRPersistentAnchorPayload rhs) =>
            !lhs.Equals(rhs);

        IntPtr m_NativePtr;

        int m_Size;

        public override int GetHashCode()
        {
            return m_NativePtr.GetHashCode();
        }
    }
}
