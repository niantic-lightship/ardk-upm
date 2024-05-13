// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;

// Used for simulation settings in editor only, does not exist on device.
#if UNITY_EDITOR
using UnityEngine.XR.Simulation;
#endif

namespace Niantic.Lightship.AR.Loader
{
    [Serializable]
    public class LightshipSimulationParams
    {
        /// <summary>
        /// Layer used for the depth
        /// </summary>
        public bool UseZBufferDepth => _useZBufferDepth;

        [SerializeField, Tooltip("When enabled, uses the geometry depth from camera z-buffer, instead of Lightship depth prediction")]
        private bool _useZBufferDepth = true;

        /// <summary>
        /// Layer used for the persistent anchor
        /// </summary>
        public bool UseSimulationPersistentAnchor => _useSimulationPersistentAnchor;

        [SerializeField,
         Tooltip("When enabled, use Lightship Persistent Anchors instead of simulation Persistent Anchors")]
        private bool _useSimulationPersistentAnchor = true;

        public LightshipSimulationPersistentAnchorParams SimulationPersistentAnchorParams => _simulationPersistentAnchorParams;

        /// <summary>
        /// Parameters for simulating the persistent anchor subsystem
        /// </summary>
        [SerializeField]
        LightshipSimulationPersistentAnchorParams _simulationPersistentAnchorParams = new LightshipSimulationPersistentAnchorParams();
    }
}
