// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PersistentAnchors
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
    [PublicAPI("apiref/Niantic/Lightship/AR/PersistentAnchors/ARPersistentAnchor/")]
    [DefaultExecutionOrder(LightshipARUpdateOrder.PersistentAnchor)]
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

        /// <summary>
        /// Positive number representing confidence we have in latest tracking update.
        /// </summary>
        public float trackingConfidence => trackingConfidenceOverride ?? sessionRelativeData.trackingConfidence;

        /// <summary>
        /// The last predicted pose for this Persistent Anchor. This is used as a source of truth in
        ///     case the GameObject's pose is manipulated by interpolation or other features.
        /// </summary>
        public Pose PredictedPose { get; internal set; } = Pose.identity;

        /// <summary>
        /// The timestamp in miliseconds corresponding to the last predicted pose for this Persistent Anchor(PredictedPose).
        /// The timestamp is in the same base as the frame.
        /// </summary>
        public UInt64 TimestampMs => sessionRelativeData.timestampMs;

        internal TrackingState? trackingStateOverride { get; set; }
        internal TrackingStateReason? trackingStateReasonOverride { get; set; }
        internal float? trackingConfidenceOverride { get; set; }

        internal bool _markedForDestruction = false;

        internal XRPersistentAnchor SessionRelativeData => sessionRelativeData;

        internal ARPersistentAnchorInterpolator Interpolator { get; private set; }

        private bool _wasntTrackingLastInterpolationApplication = false;

        /// <summary>
        /// The payload for this anchor as bytes[]
        /// This is an expensive call!
        /// </summary>
        public byte[] GetDataAsBytes() {
            return sessionRelativeData.xrPersistentAnchorPayload.GetDataAsBytes();
        }

        private void Awake()
        {
            cachedTransform = transform;
        }

        public void RegisterInterpolator(ARPersistentAnchorInterpolator interpolator)
        {
            if (Interpolator)
            {
                Log.Error
                (
                    "Cannot register multiple interpolators for the same anchor"
                );

                return;
            }

            Interpolator = interpolator;
        }

        public void DeregisterInterpolator()
        {
            Interpolator = null;
        }

        internal void ApplyPoseForInterpolation(Pose newPose, Pose startPose)
        {
            // This is only called when interpolation is enabled, so add the default interpolator
            if (!Interpolator)
            {
                gameObject.AddComponent<ARPersistentAnchorInterpolator>();
            }

            if (_wasntTrackingLastInterpolationApplication || trackingState == TrackingState.None)
            {
                // Just jump to new pose after tracking was regained
                Interpolator.InvokeInterpolation(newPose, newPose);
            }
            else
            {
                Interpolator.InvokeInterpolation(newPose, startPose);
            }

            _wasntTrackingLastInterpolationApplication = trackingState == TrackingState.None;
        }

        private void OnDestroy()
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

#region Temporal Fusion
        // Defines the number of poses to hold for temporal fusion
        internal static int FusionSlidingWindowSize = 5;
        private List<Pose> predictedPoses = new List<Pose>();
        private Transform cachedTransform;

        internal Pose FusedPose { get; private set; }

        internal void ClearTemporalFusion()
        {
            predictedPoses.Clear();
            // Just hold the current pose as the fused pose
            FusedPose = TransformToPose();
        }

        internal Pose UpdateAndApplyTemporalFusion(Pose pose)
        {
            while (predictedPoses.Count >= FusionSlidingWindowSize)
            {
                predictedPoses.RemoveAt(0);
            }

            predictedPoses.Add(pose);

            FusedPose = GetAveragePose(predictedPoses);
            ApplyPoseToTransform(FusedPose);
            return FusedPose;
        }

        // Currently this uses the latest known rotation until proper Quaternion averaging is implemented
        private Pose GetAveragePose(List<Pose> poses)
        {
            if (poses.Count == 0)
            {
                return Pose.identity;
            }

            if (poses.Count == 1)
            {
                return poses[0];
            }

            Vector3 pos = Vector3.zero;
            foreach (var pose in poses)
            {
                pos += pose.position;
            }

            pos /= poses.Count;
            // Currently this uses the latest known rotation until proper Quaternion averaging is implemented
            var rot = poses.Last().rotation;
            return new Pose(pos, rot);
        }

        internal void ApplyPoseToTransform(Pose pose)
        {
            cachedTransform.position = pose.position;
            cachedTransform.rotation = pose.rotation;
        }

        private Pose TransformToPose()
        {
            return new Pose(cachedTransform.position, cachedTransform.rotation);
        }
#endregion
    }
}
