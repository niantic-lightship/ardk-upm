// Copyright 2022-2023 Niantic.

using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
    /// <summary>
    /// LightshipNavMeshAgent is an example agent implementation that navigates a LightshipNavMesh based on logic programmed here.
    /// You place this MonoBehaviour on a GameObject to have that GameObject navigate autonomously through your real environment.
    /// You can create new versions of this to change how your creatures navigate the LightshipNavMesh.
    /// For example you may want to use physics/forces or add splines rather than straight lines.
    /// This is a basic example that using linear interpolation and coroutines.
    /// </summary>
    [PublicAPI]
    public class LightshipNavMeshAgent : MonoBehaviour
    {
        [Header("Agent Settings")] [SerializeField]
        private float walkingSpeed = 3.0f;

        [SerializeField] private float jumpDistance = 1;
        [SerializeField] private int jumpPenalty = 2;

        [SerializeField]
        private PathFindingBehaviour pathFindingBehaviour = PathFindingBehaviour.InterSurfacePreferResults;

        public enum AgentNavigationState
        {
            Paused,
            Idle,
            HasPath
        }

        public AgentNavigationState State { get; set; } = AgentNavigationState.Idle;
        private Path _path = new Path(null, Path.Status.PathInvalid);
        public Path path
        {
            get => _path;
        }
        private Vector3 _destination;

        private Coroutine _actorMoveCoroutine;
        private Coroutine _actorJumpCoroutine;

        private AgentConfiguration _agentConfig;
        private LightshipNavMeshManager _lightshipNavMeshManager;
        private LightshipNavMesh _lightshipNavMesh;

        Vector3 _dir = new Vector3(0, 0, 0);

        void Start()
        {
            _agentConfig = new AgentConfiguration(jumpPenalty, jumpDistance, pathFindingBehaviour);

            //find the LightshipNavMesh manager and connect it.
            _lightshipNavMeshManager = GameObject.FindObjectOfType<LightshipNavMeshManager>();

            //fail if missing
            if (_lightshipNavMeshManager == null)
                throw new ArgumentException("You need to add a LightshipNavMeshManager to the scene");

            _lightshipNavMesh = _lightshipNavMeshManager.LightshipNavMesh;
        }

        void Update()
        {
            switch (State)
            {
                case AgentNavigationState.Paused:
                    break;

                case AgentNavigationState.Idle:
                    StayOnNavMesh();
                    break;

                case AgentNavigationState.HasPath:
                    break;
            }
        }

        public void StopMoving()
        {
            if (_actorMoveCoroutine != null)
                StopCoroutine(_actorMoveCoroutine);
        }

        public void SetDestination(Vector3 destination)
        {
            StopMoving();

            if (_lightshipNavMesh == null)
                return;

            _destination = destination;

            Vector3 startOnBoard;
            _lightshipNavMesh.FindNearestFreePosition(transform.position, out startOnBoard);

            bool result = _lightshipNavMesh.CalculatePath(startOnBoard, destination, _agentConfig, out _path);

            if (!result)
                State = AgentNavigationState.Idle;
            else
            {
                State = AgentNavigationState.HasPath;
                _actorMoveCoroutine = StartCoroutine(Move(this.transform, _path.Waypoints));
            }
        }

        private void StayOnNavMesh()
        {
            if (_lightshipNavMesh == null || _lightshipNavMesh.Area == 0)
                return;

            if (_lightshipNavMesh.IsOnNavMesh(transform.position, 0.2f))
                return;

            List<Waypoint> pathToNavMesh = new List<Waypoint>();
            Vector3 nearestPosition;
            _lightshipNavMesh.FindNearestFreePosition(transform.position, out nearestPosition);

            _destination = nearestPosition;

            pathToNavMesh.Add(new Waypoint
            (
                transform.position,
                Waypoint.MovementType.Walk,
                Utils.PositionToTile(transform.position, _lightshipNavMesh.Settings.TileSize)
            ));

            pathToNavMesh.Add(new Waypoint
            (
                nearestPosition,
                Waypoint.MovementType.SurfaceEntry,
                Utils.PositionToTile(nearestPosition, _lightshipNavMesh.Settings.TileSize)
            ));

            _path = new Path(pathToNavMesh, Path.Status.PathComplete);
            _actorMoveCoroutine = StartCoroutine(Move(this.transform, _path.Waypoints));
            State = AgentNavigationState.HasPath;
        }

        private IEnumerator Move(Transform actor, IList<Waypoint> path)
        {
            var startPosition = actor.position;
            var startRotation = actor.rotation;
            var interval = 0.0f;
            var destIdx = 0;

            while (destIdx < path.Count)
            {
                var destination = path[destIdx].WorldPosition;

                //make sure the destination is on the mesh
                //LightshipNavMesh is an average height so can be under/over the mesh
                //the nav agent should always stand on the mesh, so using a ray cast to lift them up/down as they move.
                var from = destination + Vector3.up;
                var dir = Vector3.down;

                RaycastHit hit;

                if (Physics.Raycast(from, dir, out hit, 100, _lightshipNavMesh.Settings.LayerMask))
                {
                    destination = hit.point;
                }

                //do i need to jump or walk to the target point
                if (path[destIdx].Type == Waypoint.MovementType.SurfaceEntry)
                {
                    yield return new WaitForSeconds(0.5f);

                    _actorJumpCoroutine = StartCoroutine
                    (
                        Jump(actor, actor.position, destination)
                    );

                    yield return _actorJumpCoroutine;

                    _actorJumpCoroutine = null;
                    startPosition = actor.position;
                    startRotation = actor.rotation;
                }
                else
                {
                    //move on step towards target waypoint
                    interval += Time.deltaTime * walkingSpeed;
                    actor.position = Vector3.Lerp(startPosition, destination, interval);
                }

                //face the direction we are moving
                Vector3 lookRotationTarget = (destination - transform.position);

                //ignore up/down we dont want the creature leaning forward/backward.
                lookRotationTarget.y = 0.0f;
                lookRotationTarget = lookRotationTarget.normalized;

                //check for bad rotation
                if (lookRotationTarget != Vector3.zero)
                    transform.rotation = Quaternion.Lerp(startRotation, Quaternion.LookRotation(lookRotationTarget),
                        interval);

                //have we reached our target position, if so go to the next waypoint
                if (Vector3.Distance(actor.position, destination) < 0.01f)
                {
                    startPosition = actor.position;
                    startRotation = actor.rotation;
                    interval = 0;
                    destIdx++;
                }

                yield return null;
            }

            _actorMoveCoroutine = null;
            State = AgentNavigationState.Idle;
        }

        private IEnumerator Jump(Transform actor, Vector3 from, Vector3 to, float speed = 2.0f)
        {
            var interval = 0.0f;
            Quaternion startRotation = actor.rotation;
            var height = Mathf.Max(0.1f, Mathf.Abs(to.y - from.y));
            while (interval < 1.0f)
            {
                interval += Time.deltaTime * speed;
                Vector3 rotation = to - from;
                rotation = Vector3.ProjectOnPlane(rotation, Vector3.up).normalized;
                if (rotation != Vector3.zero)
                    transform.rotation = Quaternion.Lerp(startRotation, Quaternion.LookRotation(rotation), interval);
                var p = Vector3.Lerp(from, to, interval);
                actor.position = new Vector3
                (
                    p.x,
                    -4.0f * height * interval * interval +
                    4.0f * height * interval +
                    Mathf.Lerp(from.y, to.y, interval),
                    p.z
                );

                yield return null;
            }

            actor.position = to;
        }
    }
}

