using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// Represents an Anchor tracked by an XR device.
    /// </summary>
    /// <remarks>
    /// An anchor is a pose in the physical environment that is tracked by an XR device.
    /// As the device refines its understanding of the environment, anchors will be
    /// updated, helping you to keep virtual content connected to a real-world position and orientation.
    /// </remarks>
    [DefaultExecutionOrder(ARUpdateOrder.k_Anchor)]
    [DisallowMultipleComponent]
    public sealed class ARPersistentAnchor : ARTrackable<XRPersistentAnchor, ARPersistentAnchor>
    {
        /// <summary>
        /// Get the native pointer associated with this <see cref="ARAnchor"/>.
        /// </summary>
        /// <remarks>
        /// The data pointed to by this pointer is implementation defined. While its
        /// lifetime is also implementation defined, it should be valid until at least
        /// the next <see cref="ARSession"/> update.
        /// </remarks>
        public IntPtr nativePtr => sessionRelativeData.nativePtr;

        /// <summary>
        /// The reason for the anchor's current tracking state
        /// </summary>
        public TrackingStateReason trackingStateReason => sessionRelativeData.trackingStateReason;

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
