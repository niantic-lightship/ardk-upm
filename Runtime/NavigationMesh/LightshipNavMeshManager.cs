// Copyright 2022-2024 Niantic.
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
    [PublicAPI("apiref/Niantic/Lightship/AR/NavigationMesh/LightshipNavMeshManager/")]
    public class LightshipNavMeshManager : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("The scene camera used to render AR content.")]
        [SerializeField]
        private Camera _camera;

        [Header("LightshipNavMesh Settings")]
        [Tooltip("Metric size of a grid tile containing one node")]
        [SerializeField]
        [Min(0.0000001f)]
        private float _tileSize = 0.15f;

        [Tooltip("Tolerance to consider floor as flat despite meshing noise")]
        [SerializeField]
        [Min(0.0000001f)]
        private float _flatFloorTolerance = 0.2f;

        [Tooltip("Maximum slope angle (degrees) an area can have and still be considered flat")]
        [SerializeField]
        [Range(0, 40)]
        private float _maxSlope = 25.0f;

        [Tooltip("The maximum amount two cells can differ in elevation and still be considered on the same plane")]
        [SerializeField]
        [Min(0.0000001f)]
        private float _stepHeight = 0.1f;

        [Header("Scan Settings")]
        [Tooltip("How long (in seconds) to wait before scanning the environment again to update the NavMesh")]
        [SerializeField]
        private float _scanInterval = 0.1f;

        [Tooltip("Size of the area to scan (width in meters of a square centered 1 meter in front of the player)")]
        [SerializeField]
        private float _scanRange = 1.5f;

        [Tooltip("Must be the same layer as meshes.")]
        [SerializeField]
        private LayerMask _layerMask = ~0;

        [Header("Debug")]
        [Tooltip("Draws vertical lines through each scanned tile. Visible in Scene and Game view if Gizmos are enabled.")]
        [SerializeField]
        public bool _visualise = true;

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

        private void Start()
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
