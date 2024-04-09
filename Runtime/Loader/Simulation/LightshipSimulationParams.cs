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
        private GameObject _previousEnvironmentPrefab;

        [SerializeField, Tooltip("Use this environment prefab for simulation, applies to XRSimulationPreferences")]
        private GameObject _environmentPrefab;

        public GameObject EnvironmentPrefab
        {
            get => _environmentPrefab;
#if UNITY_EDITOR
            internal set
            {
                _environmentPrefab = value;
                OnValidate();
            }
#endif
        }

        /// <summary>
        /// Layer used for the depth
        /// </summary>
        public bool UseZBufferDepth => _useZBufferDepth;

        [SerializeField, Tooltip("When enabled, uses the geometry depth from camera z-buffer, instead of Lightship depth prediction")]
        private bool _useZBufferDepth = true;

        /// <summary>
        /// Layer used for the persistent anchor
        /// </summary>
        public bool UseLightshipPersistentAnchor => _useLightshipPersistentAnchor;

        [SerializeField,
         Tooltip("When enabled, use Lightship Persistent Anchors instead of simulation Persistent Anchors")]
        private bool _useLightshipPersistentAnchor = false;

        public LightshipSimulationPersistentAnchorParams SimulationPersistentAnchorParams => _simulationPersistentAnchorParams;

        /// <summary>
        /// Parameters for simulating the persistent anchor subsystem
        /// </summary>
        [SerializeField]
        LightshipSimulationPersistentAnchorParams _simulationPersistentAnchorParams = new LightshipSimulationPersistentAnchorParams();

#if UNITY_EDITOR
        internal void OnValidate()
        {
            // If the environment prefab has changed, update the XRSimulationPreferences
            if (_environmentPrefab != _previousEnvironmentPrefab)
            {
                _previousEnvironmentPrefab = _environmentPrefab;
                if (_environmentPrefab != XRSimulationPreferences.Instance.environmentPrefab)
                {
                    XRSimulationPreferences.Instance.environmentPrefab = _environmentPrefab;
                }
            }
        }
#endif
    }
}
