// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh.Internal
{
  internal sealed class PathFinding
  {
    private NavMeshModel _model;

    public PathFinding(NavMeshModel model)
    {
      _model = model;
    }

    public bool CalculatePath
    (
      Vector3 fromPosition,
      Vector3 toPosition,
      AgentConfiguration agent,
      out Path path
    )
    {
      bool result;

      // Convert world positions to coordinates on the grid
      var source = Utils.PositionToGridNode(fromPosition, _model.Settings.TileSize);
      var destination = Utils.PositionToGridNode(toPosition, _model.Settings.TileSize);

      // Attempting to get path to the same tile
      if (source.Equals(destination))
      {
        //ARLog._Warn("Attempted to calculate path to the same position.");
        path = new Path(null, Path.Status.PathInvalid);
        return false;
      }

      // Find the subject surface on the LightshipNavMesh
      var startSurface = _model.Surfaces.FirstOrDefault(p => p.ContainsElement(source));
      if (startSurface == null)
      {
        //ARLog._Warn("Could not locate start position on any surface.");
        path = new Path(null, Path.Status.PathInvalid);
        return false;
      }

      switch (agent.Behaviour)
      {
        case PathFindingBehaviour.SingleSurface:
          result = CalculatePathOnSurface(startSurface, source, destination, out Vector2Int _, out path);
          break;

        case PathFindingBehaviour.InterSurfacePreferPerformance:
          result = CalculatePathOnBoardLocal(startSurface, source, destination, agent, out path);
          break;

        case PathFindingBehaviour.InterSurfacePreferResults:
          result = CalculatePathOnBoardGlobal(startSurface, source, destination, agent, out path);
          break;

        default:
          throw new NotImplementedException();
      }

      return result;
    }

    private bool CalculatePathOnSurface
    (
      Surface surface,
      GridNode source,
      GridNode destination,
      out Vector2Int closestCoordinateToDestination,
      out Path path
    )
    {
      var costToGoal = Vector2Int.Distance(source.Coordinates, destination.Coordinates);
      var start = new PathFindingNode(source.Coordinates, surface)
      {
        CostToGoal = costToGoal
      };

      var open = new List<PathFindingNode> {start};

      var closed = new HashSet<PathFindingNode>();
      List<Waypoint> waypoints;

      // This is a substitute for the destination if it cannot be found on the surface
      var closestNodeToGoal = start;

      while (open.Count > 0)
      {
        // Get the most eligible node to continue traversal
        var current = open[0];
        open.RemoveAt(0);
        closed.Add(current);

        if (current.CostToGoal < closestNodeToGoal.CostToGoal)
        {
          closestNodeToGoal = current;
        }

        // Find neighbours on the plane
        var neighbours = Utils.GetNeighbours(current.Coordinates);
        foreach (var coords in neighbours)
        {
          if (!surface.ContainsElement(new GridNode(coords)))
          {
            // Discard this neighbour, since it cannot be found on the same plane
            continue;
          }

          // Potential successor
          var successor = new PathFindingNode(coords, surface, current.Coordinates);

          // We arrived at the goal
          if (successor.Coordinates.Equals(destination.Coordinates))
          {
            closestCoordinateToDestination = successor.Coordinates;
            waypoints = GeneratePath(nodes: closed, traceStart: successor);
            path = new Path(waypoints, Path.Status.PathComplete);
            return true;
          }

          // We have already processed this grid cell.
          if (closed.Contains(successor))
          {
            continue;
          }

          // Calculate costs
          successor.CostToThis = current.CostToThis + Utils.ManhattanDistance(successor, current);
          successor.CostToGoal = Vector2Int.Distance
            (destination.Coordinates, successor.Coordinates);

          var existingIndex = open.FindIndex
            (openNode => openNode.Coordinates.Equals(successor.Coordinates));

          if (existingIndex >= 0)
          {
            var existing = open[existingIndex];
            if (existing.Cost <= successor.Cost)
            {
              continue;
            }

            open.RemoveAt(existingIndex);
          }

          open.InsertIntoSortedList(successor, (a, b) => a.CompareTo(b));
        }
      }

      // We have reached the closest position to our destination on this surface
      closestCoordinateToDestination = closestNodeToGoal.Coordinates;
      waypoints = GeneratePath(nodes: closed, traceStart: closestNodeToGoal);
      path = new Path(waypoints, Path.Status.PathPartial);
      return true;
    }

    private bool CalculatePathOnBoardLocal
    (
      Surface startSurface,
      GridNode source,
      GridNode destination,
      AgentConfiguration agent,
      out Path path
    )
    {
      // We will use this origin to test its neighbouring nodes on the grid for validity to be jumped over or onto
      var result = CalculatePathOnSurface(startSurface, source, destination, out Vector2Int searchOrigin, out path);

      var currentSurface = startSurface;
      var closestCoordinateToDestination = searchOrigin;
      var nextOrigin = searchOrigin;

      while (!searchOrigin.Equals(destination.Coordinates))
      {
        var continueSearch = false;
        var neighbours = Utils.GetNeighbours(searchOrigin)
          .OrderBy(coords => Vector2Int.Distance(coords, destination.Coordinates))
          .ToArray();

        foreach (var neighbour in neighbours)
        {
          var inspectedNeighbour = new GridNode(neighbour);

          // The inspected node can't belong to the same plane we're currently on and it has to be within the specified jump distance
          var isValidNeighbour = !currentSurface.ContainsElement(inspectedNeighbour) &&
            Vector2Int.Distance
              (inspectedNeighbour.Coordinates, closestCoordinateToDestination) *
            _model.Settings.TileSize <
            agent.JumpDistance &&
            Vector2Int.Distance
              (inspectedNeighbour.Coordinates, destination.Coordinates) <
            Vector2Int.Distance(searchOrigin, destination.Coordinates);

          if (!isValidNeighbour)
          {
            continue;
          }

          if (!continueSearch)
          {
            // We store the new closest node to the destination
            // in case we can't find a valid node to jump to in this range...
            nextOrigin = inspectedNeighbour.Coordinates;
            continueSearch = true;
          }

          // Check whether the inspected node belongs to any other existing plane
          var nextSurface = _model.Surfaces.FirstOrDefault
            (surface => surface.ContainsElement(inspectedNeighbour));

          if (nextSurface != null)
          {
            // Can we jump here?
            if (Vector3.Distance
              (
                Utils.TileToPosition(closestCoordinateToDestination, currentSurface.Elevation, _model.Settings.TileSize),
                Utils.TileToPosition(nextOrigin, nextSurface.Elevation, _model.Settings.TileSize)
              ) <
              agent.JumpDistance)
            {
              // New surface found!
              inspectedNeighbour.Elevation = nextSurface.Elevation;
              var subRoute = CalculatePathOnSurface
                (nextSurface, inspectedNeighbour, destination, out nextOrigin, out Path subPath);

              closestCoordinateToDestination = nextOrigin;
              currentSurface = nextSurface;
              path.Waypoints.AddRange(subPath.Waypoints);

              break;
            }
          }
        }

        if (!continueSearch)
        {
          break;
        }

        searchOrigin = nextOrigin;
      }

      return result;
    }

    private bool CalculatePathOnBoardGlobal
    (
      Surface startSurface,
      GridNode source,
      GridNode destination,
      AgentConfiguration agent,
      out Path path
    )
    {
      var costToGoal = Vector2Int.Distance(source.Coordinates, destination.Coordinates);
      var start = new PathFindingNode(source.Coordinates, startSurface)
      {
        CostToGoal = costToGoal
      };

      var open = new List<PathFindingNode>
      {
        start
      };

      var closed = new HashSet<PathFindingNode>();
      List<Waypoint> waypoints;

      var closestNodeToGoal = start;

      while (open.Count > 0)
      {
        // Get the most eligible node to continue traversal
        var current = open[0];
        open.RemoveAt(0);
        closed.Add(current);

        if (current.CostToGoal < closestNodeToGoal.CostToGoal && current.Surface != null)
        {
          closestNodeToGoal = current;
        }

        // Find neighbours on the plane
        var neighbours = Utils.GetNeighbours(current.Coordinates);
        foreach (var coords in neighbours)
        {
          var node = new GridNode(coords);
          var surfaceOfNeighbour = current.Surface == null || !current.Surface.ContainsElement(node)
            ? _model.Surfaces.FirstOrDefault(s => s.ContainsElement(node))
            : current.Surface;

          var offSurface = surfaceOfNeighbour == null;

          // Potential successor
          var successor = offSurface
            ? new PathFindingNode(coords, current.Elevation, parentCoordinates: current.Coordinates)
            : new PathFindingNode(coords, surfaceOfNeighbour, parentCoordinates: current.Coordinates);

          var aggregateOffSurface = offSurface
            ? current.AggregateOffSurface + 1
            : 0;

          var elevationDiff = Mathf.Abs(successor.Elevation - current.Elevation);
          var offSurfaceDist = aggregateOffSurface * _model.Settings.TileSize;
          var jumpDistance = Mathf.Sqrt
            (elevationDiff * elevationDiff + offSurfaceDist * offSurfaceDist);

          if (jumpDistance > agent.JumpDistance)
          {
            continue;
          }

          // We arrived at the goal
          if (!offSurface && successor.Coordinates.Equals(destination.Coordinates))
          {
            waypoints = GeneratePath(nodes: closed, traceStart: successor);
            path = new Path(waypoints, Path.Status.PathComplete);
            return true;
          }

          // We have already processed this grid cell.
          if (closed.Contains(successor))
          {
            continue;
          }

          // Calculate costs
          var isJump = elevationDiff > 0 || offSurface;
          successor.AggregateOffSurface = aggregateOffSurface;
          successor.CostToThis = current.CostToThis +
            Utils.ManhattanDistance(successor, current) +
            (isJump ? agent.JumpPenalty : 0);

          successor.CostToGoal = Vector2Int.Distance
            (destination.Coordinates, successor.Coordinates);

          var existingIndex = open.FindIndex
            (openNode => openNode.Coordinates.Equals(successor.Coordinates));

          if (existingIndex >= 0)
          {
            var existing = open[existingIndex];
            if (existing.Cost <= successor.Cost)
            {
              continue;
            }

            open.RemoveAt(existingIndex);
          }

          open.InsertIntoSortedList(successor, (a, b) => a.CompareTo(b));
        }
      }

      // We have reached the closest position to our destination on this surface
      waypoints = GeneratePath(nodes: closed, traceStart: closestNodeToGoal);
      path = new Path(waypoints, Path.Status.PathPartial);
      return true;
    }

    /// Traces a path from a pre-computed PathNode collection.
    /// @param nodesA collection containing parental relationships.
    /// @param traceStart The source node of the trace.
    private List<Waypoint> GeneratePath(HashSet<PathFindingNode> nodes, PathFindingNode traceStart)
    {
      var path = new List<Waypoint>();

      // Trace path
      var node = traceStart;
      while (node.HasParent)
      {
        var parent = nodes.FirstOrDefault
          (entry => entry.Coordinates.Equals(node.ParentCoordinates));

        // Extract node position
        if (node.Surface != null)
        {
          var type = parent.Surface == node.Surface
            ? Waypoint.MovementType.Walk
            : Waypoint.MovementType.SurfaceEntry;

          var pos = Utils.TileToPosition(node.Coordinates, _model.Settings.TileSize);
          path.Add(new Waypoint(new Vector3(pos.x, node.Elevation, pos.y), type, node.Coordinates));
        }

        // Go to parent
        node = parent;
      }

      // The resulting array should start with the source node
      path.Reverse();

      return path;
    }
  }
}
