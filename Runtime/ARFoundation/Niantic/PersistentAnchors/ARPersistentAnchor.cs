using System;
using System.Collections.Generic;
using System.Linq;

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
    [DefaultExecutionOrder(LightshipARUpdateOrder.k_PersistentAnchor)]
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

        internal bool _markedForDestruction = false;

        internal XRPersistentAnchor SessionRelativeData => sessionRelativeData;

        private void Awake()
        {
            cachedTransform = transform;
        }

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

#region Temporal Fusion
        // Defines the number of poses to hold for temporal fusion
        internal static int FusionSlidingWindowSize = 5;
        private List<Pose> predictedPoses = new List<Pose>();

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
#endregion

#region Interpolation
        // Defines the seconds over which interpolation takes place
        internal static float InterpolationTimeSeconds = 1;
        private float elapsedInterpolationTime;

        private Pose lerpStartPose = Pose.identity;
        private Pose intermediateLerpPose = Pose.identity;
        private Pose targetPose = Pose.identity;
        private Transform cachedTransform;

        internal void StartInterpolationToPose(Pose newPose, Pose startPose)
        {
            // For the first prediction, directly apply the pose instead of interpolating
            if (PredictedPose.Equals(Pose.identity))
            {
                targetPose = newPose;
                lerpStartPose = newPose;
                intermediateLerpPose = newPose;
                ApplyPoseToTransform(newPose);
                return;
            }

            // If updating from the middle of a previous interpolation, use the last interpolation pose as start
            if (elapsedInterpolationTime != 0)
            {
                lerpStartPose = intermediateLerpPose;
            }
            // Otherwise, interpolate from the last predicted pose
            else
            {
                lerpStartPose = startPose;
            }

            targetPose = newPose;
            ApplyPoseToTransform(lerpStartPose);
            elapsedInterpolationTime = 0;
        }
        
        private void Update()
        {
            if (!targetPose.Equals(lerpStartPose))
            {
                elapsedInterpolationTime += Time.deltaTime;
                
                // At end of interpolation time, reset interpolation
                if (elapsedInterpolationTime >= InterpolationTimeSeconds)
                {
                    lerpStartPose = targetPose;
                    intermediateLerpPose = targetPose;
                    elapsedInterpolationTime = 0;
                }
                // Interpolate towards to target
                else
                {
                    var lerpPos = Vector3.Lerp
                    (
                        lerpStartPose.position,
                        targetPose.position,
                        elapsedInterpolationTime / InterpolationTimeSeconds
                    );

                    var lerpRot = Quaternion.Lerp
                    (
                        lerpStartPose.rotation,
                        targetPose.rotation,
                        elapsedInterpolationTime / InterpolationTimeSeconds
                    );

                    intermediateLerpPose = new Pose(lerpPos, lerpRot);
                }
                
                ApplyPoseToTransform(intermediateLerpPose);
            }
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
