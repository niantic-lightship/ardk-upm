using System;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// Represents a Persistent Anchor tracked by an XR device.
    /// </summary>
    /// <remarks>
    /// Persistent anchors are persistent <c>Pose</c>s in the world that are generated
    /// by processed scans, and will be in the same real world location in future sessions.
    /// By placing virtual content relative to a Persistent Anchor, it can be restored to the same
    /// real world location in a future session.
    /// </remarks>
    [PublicAPI]
    [DefaultExecutionOrder(ARUpdateOrder.k_Anchor)]
    [DisallowMultipleComponent]
    public sealed class ARPersistentAnchor : ARTrackable<XRPersistentAnchor, ARPersistentAnchor>
    {
        /// <summary>
        /// Get the native pointer associated with this <see cref="ARPersistentAnchor"/>.
        /// </summary>
        /// <remarks>
        /// The data pointed to by this pointer is implementation defined. While its
        /// lifetime is also implementation defined, it should be valid until at least
        /// the next <see cref="ARSession"/> update.
        /// </remarks>
        public IntPtr nativePtr => sessionRelativeData.nativePtr;

        /// <summary>
        /// The Persistent Anchor's current tracking state.
        /// A Persistent Anchor should only be used to display virtual content if its tracking state
        ///     is Tracking or Limited. Otherwise, the pose of the Persistent Anchor is not guaranteed
        ///     to be in the correct real world location
        /// </summary>
        public new TrackingState trackingState => trackingStateOverride ?? sessionRelativeData.trackingState;

        /// <summary>
        /// The reason for the Persistent Anchor's current tracking state
        /// </summary>
        public TrackingStateReason trackingStateReason => trackingStateReasonOverride ?? sessionRelativeData.trackingStateReason;

        internal TrackingState? trackingStateOverride { get; set; }
        internal TrackingStateReason? trackingStateReasonOverride { get; set; }

        internal bool _markedForDestruction = false;

        internal XRPersistentAnchor SessionRelativeData => sessionRelativeData;

        void OnDestroy()
        {
            if (_markedForDestruction)
            {
                return;
            }
            if(ARPersistentAnchorManager.Instance)
            {
                _markedForDestruction = true;
                ARPersistentAnchorManager.Instance.DestroyAnchor(this);
            }
        }
    }
}
