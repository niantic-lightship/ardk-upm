// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    [Serializable]
    public class LightshipSimulationPersistentAnchorParams
    {
        [SerializeField]
        [Tooltip("Minimum time in seconds that must elapse before the anchor is surfaced.")]
        float m_minimumAnchorDiscoveryTimeSeconds = 1.0f;

        [SerializeField]
        [Tooltip("Maximum time in seconds that must elapse before the anchor is surfaced.")]
        float m_maximumAnchorDiscoveryTimeSeconds = 2.0f;

        [SerializeField]
        [Tooltip("Whether to apply a translational offset to the anchor.")]
        bool m_applyTranslationalOffset = false;

        [SerializeField]
        [Tooltip("Maximum translational offset in meters to apply to the anchor. An offset from 0 - max will be chosen at random.")]
        float m_translationalOffsetSeverityMeters = 0.05f;

        [SerializeField]
        [Tooltip("Whether to apply a rotational offset to the anchor.")]
        bool m_applyRotationalOffset = false;

        [SerializeField]
        [Tooltip("Maximum rotational offset in degrees to apply to the anchor. An offset from 0 - max will be chosen at random.")]
        float m_rotationalOffsetSeverityDegrees = 1.0f;

        [SerializeField]
        [Tooltip("Whether to surface an anchor failure state instead of success")]
        bool m_surfaceAnchorFailure = false;

        [SerializeField]
        [Tooltip("Failure reason for surfacing an anchor failure state")]
        TrackingStateReason m_trackingStateReason = TrackingStateReason.None;

        public float minimumAnchorDiscoveryTimeSeconds
        {
            get => m_minimumAnchorDiscoveryTimeSeconds;
            set => m_minimumAnchorDiscoveryTimeSeconds = value;
        }

        public float maximumAnchorDiscoveryTimeSeconds
        {
            get => m_maximumAnchorDiscoveryTimeSeconds;
            set => m_maximumAnchorDiscoveryTimeSeconds = value;
        }

        public bool applyTranslationalOffset
        {
            get => m_applyTranslationalOffset;
            set => m_applyTranslationalOffset = value;
        }

        public float translationalOffsetSeverityMeters
        {
            get => m_translationalOffsetSeverityMeters;
            set => m_translationalOffsetSeverityMeters = value;
        }

        public bool applyRotationalOffset
        {
            get => m_applyRotationalOffset;
            set => m_applyRotationalOffset = value;
        }

        public float rotationalOffsetSeverityDegrees
        {
            get => m_rotationalOffsetSeverityDegrees;
            set => m_rotationalOffsetSeverityDegrees = value;
        }

        public bool surfaceAnchorFailure
        {
            get => m_surfaceAnchorFailure;
            set => m_surfaceAnchorFailure = value;
        }

        public TrackingStateReason trackingStateReason
        {
            get => m_trackingStateReason;
            set => m_trackingStateReason = value;
        }

        internal LightshipSimulationPersistentAnchorParams() { }

        internal LightshipSimulationPersistentAnchorParams(LightshipSimulationPersistentAnchorParams source)
        {
            CopyFrom(source);
        }

        internal void CopyFrom(LightshipSimulationPersistentAnchorParams source)
        {
            minimumAnchorDiscoveryTimeSeconds = source.minimumAnchorDiscoveryTimeSeconds;
            maximumAnchorDiscoveryTimeSeconds = source.maximumAnchorDiscoveryTimeSeconds;
            applyTranslationalOffset = source.applyTranslationalOffset;
            translationalOffsetSeverityMeters = source.translationalOffsetSeverityMeters;
            applyRotationalOffset = source.applyRotationalOffset;
            rotationalOffsetSeverityDegrees = source.rotationalOffsetSeverityDegrees;
            surfaceAnchorFailure = source.surfaceAnchorFailure;
            trackingStateReason = source.trackingStateReason;
        }
    }
}
