// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.PersistentAnchors
{
    // Provides a default behaviour for interpolating Anchor updates instead of snapping to the
    //  new pose. This is useful for smooth transitions between predicted poses.
    // Provides virtual methods to be overriden for custom interpolation behaviour.
    // @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
    public class ARPersistentAnchorInterpolator :
        MonoBehaviour
    {
        // Tunable value for how long it takes to interpolate between poses. In the default behaviour, this is used as
        // a maximum value, and the actual interpolation time is calculated based on distance.
        public static float InterpolationTimeSeconds { get; set; } = 3.0f;
        protected ARPersistentAnchor ARPersistentAnchor { get; private set; }

        // Cached transform on this Gameobject, to avoid repeated lookups
        protected Transform cachedTransform;

        private float elapsedInterpolationTime;

        private Pose lerpStartPose = Pose.identity;
        private Pose intermediateLerpPose = Pose.identity;
        private Pose targetPose = Pose.identity;
        private float _interpolationTime;

        // This can be overriden for a custom Awake, but be sure to call base.Awake() to register
        protected virtual void Awake()
        {
            cachedTransform = transform;
            ARPersistentAnchor = GetComponent<ARPersistentAnchor>();
            if (!ARPersistentAnchor)
            {
                Log.Error("No ARPersistentAnchor found on this GameObject");
                Destroy(this);
                return;
            }

            ARPersistentAnchor.RegisterInterpolator(this);
        }

        // This can be overriden for a custom OnDestroy, but be sure to call base.OnDestroy() to deregister
        protected virtual void OnDestroy()
        {
            if (!ARPersistentAnchor)
            {
                Log.Error("No ARPersistentAnchor found on this GameObject");
                return;
            }

            ARPersistentAnchor.DeregisterInterpolator();
        }

        // Override this to listen to pose updates
        protected virtual void OnPoseUpdateReceived(Pose newPose, Pose startPose)
        {
            // For the first prediction, directly apply the pose instead of interpolating
            if (ARPersistentAnchor.PredictedPose.Equals(Pose.identity))
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
            // Apply interpolation depending on distance, capped at 3 seconds. 1 second per 10cm
            _interpolationTime = Math.Min(InterpolationTimeSeconds, Vector3.Distance(lerpStartPose.position, targetPose.position) / 0.1f);
            elapsedInterpolationTime = 0;
        }

        // Override this to define custom interpolation behaviour
        protected virtual void Update()
        {
            if (!targetPose.Equals(lerpStartPose))
            {
                elapsedInterpolationTime += Time.deltaTime;

                // At end of interpolation time, reset interpolation
                if (elapsedInterpolationTime >= _interpolationTime)
                {
                    lerpStartPose = targetPose;
                    intermediateLerpPose = targetPose;
                    elapsedInterpolationTime = 0;
                }
                // Interpolate towards to target
                else
                {
                    // Elapsed / Interpolation is between 0 and 1
                    var smoothTime = EaseInOut(elapsedInterpolationTime / _interpolationTime);
                    var lerpPos = Vector3.Lerp
                    (
                        lerpStartPose.position,
                        targetPose.position,
                        smoothTime
                    );

                    var lerpRot = Quaternion.Lerp
                    (
                        lerpStartPose.rotation,
                        targetPose.rotation,
                        smoothTime
                    );

                    intermediateLerpPose = new Pose(lerpPos, lerpRot);
                }

                ApplyPoseToTransform(intermediateLerpPose);
            }
        }

        // Applies a Pose to the transform this component is attached to
        protected void ApplyPoseToTransform(Pose pose)
        {
            cachedTransform.position = pose.position;
            cachedTransform.rotation = pose.rotation;
        }

        // Get the Pose from the transform this component is attached to
        protected Pose TransformToPose()
        {
            return new Pose(cachedTransform.position, cachedTransform.rotation);
        }

        internal void InvokeInterpolation(Pose newPose, Pose oldPose)
        {
            OnPoseUpdateReceived(newPose, oldPose);
        }

        private float EaseIn(float t)
        {
            return t * t;
        }

        private float EaseOut(float t)
        {
            return 1- ((1 - t) * (1 - t));
        }

        private float EaseInOut(float t)
        {
            return t < 0.5 ? EaseIn(t * 2) / 2 : EaseOut(t * 2 - 1) / 2 + 0.5f;
        }
    }
}
