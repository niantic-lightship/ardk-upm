using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.Extensions.Gameboard;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.Extensions.Gameboard
{
    /// <summary>
    /// GameboardAgentPathRenderer is a debug renderer to show you the path a <c>GameboardAgent</c> is moving along
    /// while  navigating the environment. You add it to the <c>GameBoardAgent</c> <c>GameObject</c> in your scene
    /// and it will draw that agent's current path.
    /// </summary>
    [PublicAPI]
    public class GameboardAgentPathRenderer : MonoBehaviour
    {
        /// <summary>
        /// The <c>GameboardAgent</c> that you want to render the path for.
        /// </summary>
        public GameboardAgent _agent;
        /// <summary>
        /// The <c>Material</c> used to render the path. This <c>Material</c> will be applied on a <c>LineRenderer</c>.
        /// </summary>
        public Material _material;
        private LineRenderer _lineRenderer;
        private List<Vector3> _points = new List<Vector3>();

        void Start()
        {
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
            _lineRenderer.material = _material;
            //.shader=Shader.Find("Unlit/Color");
            _lineRenderer.material.color = Color.blue;

            _lineRenderer.startWidth = 0.05f;
            _lineRenderer.endWidth = 0.05f;

        }

        void AddLine(Vector3 start, Vector3 end)
        {
            _points.Add(start);
            _points.Add(end);
        }

        private void OnEnable()
        {
            if (_lineRenderer != null)
                _lineRenderer.enabled = true;
        }

        private void OnDisable()
        {
            _lineRenderer.enabled = false;
        }

        void Update()
        {

            if (_agent.path.Waypoints == null)
                return;

            //get this agents path and make a flat array of points
            var path = _agent.path;
            _points.Clear();

            float offset = 0.01f;
            for (int i = 0; i < path.Waypoints.Count - 1; i++)
            {
                var points = path.Waypoints;
                AddLine(points[i].WorldPosition + Vector3.up * offset,
                    points[i + 1].WorldPosition + Vector3.up * offset);
            }

            _lineRenderer.positionCount = _points.Count;
            _lineRenderer.SetPositions(_points.ToArray());

        }
    }
}
