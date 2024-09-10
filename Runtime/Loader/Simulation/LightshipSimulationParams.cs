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
        [SerializeField]
        [Tooltip("When enabled, uses the geometry depth from camera z-buffer, instead of Lightship depth prediction")]
        private bool _useZBufferDepth = true;

        [SerializeField]
        [Tooltip("When enabled, use Lightship Persistent Anchors instead of simulation Persistent Anchors")]
        private bool _useSimulationPersistentAnchor = true;

        [SerializeField]
        [Tooltip("Parameters for simulating the persistent anchor subsystem")]
        LightshipSimulationPersistentAnchorParams _simulationPersistentAnchorParams = new ();

        /// <summary>
        /// Layer used for the depth
        /// </summary>
        public bool UseZBufferDepth
        {
            get => _useZBufferDepth;
            set => _useZBufferDepth = value;
        }

        /// <summary>
        /// Layer used for the persistent anchor
        /// </summary>
        public bool UseSimulationPersistentAnchor
        {
            get => _useSimulationPersistentAnchor;
            set => _useSimulationPersistentAnchor = value;
        }

        /// <summary>
        /// Parameters for simulating the persistent anchor subsystem
        /// </summary>
        public LightshipSimulationPersistentAnchorParams SimulationPersistentAnchorParams
        {
            get => _simulationPersistentAnchorParams;
        }

        internal LightshipSimulationParams()
        {
            _simulationPersistentAnchorParams = new LightshipSimulationPersistentAnchorParams();
        }

        internal LightshipSimulationParams(LightshipSimulationParams source)
        {
            _simulationPersistentAnchorParams = new LightshipSimulationPersistentAnchorParams();
            CopyFrom(source);
        }

        internal void CopyFrom(LightshipSimulationParams source)
        {
            UseZBufferDepth = source._useZBufferDepth;
            UseSimulationPersistentAnchor = source._useSimulationPersistentAnchor;
            SimulationPersistentAnchorParams.CopyFrom(source._simulationPersistentAnchorParams);
        }
    }
}
