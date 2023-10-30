// Copyright 2023 Niantic, Inc. All Rights Reserved.
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
    /// <summary>
    /// <c>LightshipNavMeshManager</c> is a <c>MonoBehaviour</c> that will create a <see cref="LightshipNavMesh"/> configured according to your settings and manage how it gets updated.
    /// You can add this component to a <c>GameObject</c> in your scene to use the  <see cref="LightshipNavMesh"/> features.
    /// You can pass this to any <c>GameObject</c> s that may need the  <see cref="LightshipNavMesh"/> e.g. your agents that handle moving across the board.
    /// </summary>
    [PublicAPI]
    public class LightshipNavMeshManager : MonoBehaviour
    {
        [Header("Camera")] [SerializeField] [Tooltip("The scene camera used to render AR content.")]
        private Camera _camera;

        [Header("LightshipNavMesh Settings")]
        [SerializeField]
        [Tooltip("Metric size of a grid tile containing one node")]
        [Min(0.0000001f)]
        private float _tileSize = 0.15f;

        [SerializeField] [Tooltip("Tolerance to consider floor as flat despite meshing noise")] [Min(0.0000001f)]
        private float _flatFloorTolerance = 0.2f;

        [SerializeField]
        [Tooltip("Maximum slope angle (degrees) an area can have and still be considered flat")]
        [Range(0, 40)]
        private float _maxSlope = 25.0f;

        [SerializeField]
        [Tooltip("The maximum amount two cells can differ in elevation and still be considered on the same plane")]
        [Min(0.0000001f)]
        private float _stepHeight = 0.1f;

        [Header("Scan Settings")] [SerializeField]
        private float _scanInterval = 0.1f;

        [SerializeField] private float _scanRange = 1.5f;

        [SerializeField] [Tooltip("Must be the same layer as meshes.")]
        private LayerMask _layerMask = ~0;

        [Header("Debug")] [SerializeField] public bool _visualise = true;

        //manager owns these
        private LightshipNavMesh _lightshipNavMesh;
        private ModelSettings _settings;

        private float _lastScan;

        /// <summary>
        /// A reference to the <c>LightshipNavMesh</c> that is being managed by this LightshipNavMeshManager
        /// </summary>
        public LightshipNavMesh LightshipNavMesh
        {
            get { return _lightshipNavMesh; }
        }

        void Start()
        {
            //create my LightshipNavMesh
            _settings = new ModelSettings
            (
                _tileSize,
                _flatFloorTolerance,
                _maxSlope,
                _stepHeight,
                _layerMask
            );
            _lightshipNavMesh = new LightshipNavMesh(_settings, _visualise);
        }

        private void UpdateNavMesh()
        {
            //tell LightshipNavMesh to scan where the player is.
            var cameraTransform = _camera.transform;
            var playerPosition = cameraTransform.position;
            var playerForward = cameraTransform.forward;

            // The origin of the scan should be in front of the player
            var origin = playerPosition + Vector3.ProjectOnPlane(playerForward, Vector3.up).normalized;

            // Scan the environment
            _lightshipNavMesh.Scan(origin, range: _scanRange);
        }

        private void Update()
        {
            if (!(Time.time - _lastScan > _scanInterval))
                return;

            _lastScan = Time.time;
            UpdateNavMesh();
        }
    }
}
