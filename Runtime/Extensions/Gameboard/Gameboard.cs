// Copyright 2022 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Random = UnityEngine.Random;

namespace Niantic.Lightship.AR.Extensions.Gameboard
{
  /// <summary>
  /// Gameboard this class manages the the gameboard data structures.
  /// It dynamically builds a 2d grid for running navigation algorithms on.
  /// There are a number of functions to help you place and move object on the board.
  /// </summary>
  public class Gameboard
  {
    private readonly ModelSettings _settings;

    private GameboardModel _model;
    private PathFinding _pathFinding;

    /// The configuration of the GameboardModel.
    public ModelSettings Settings
    {
      get => _settings;
    }

    /// The discovered free area in square meters.
    public float Area { get; private set; }

    private void RecalculateArea()
    {
      Area = 0;
      float tileArea = _settings.TileSize * _settings.TileSize;
      foreach (Surface modelSurface in _model.Surfaces)
      {
        Area += modelSurface.Elements.Count() * tileArea;
      }
    }

    public List<Surface> Surfaces
    {
      get => _model.Surfaces;
    }

    public void Destroy()
    {
      _model = null;
    }

    /// Allocates a new Gameboard.
    /// @param settings Settings to calibrate unoccupied area detection.
    /// @param visualise Activate visualisation.
    public Gameboard(ModelSettings settings, bool visualise)
    {
      if (settings.TileSize <= 0)
        throw new ArgumentException("Tile size must be greater than zero.");

      if (settings.KernelSize % 2 == 0)
        throw new ArgumentException("Kernel size must be an odd number.");

      if (settings.MaxSlope > 40.0f)
        throw new ArgumentException("MaxSlope must be less than or equal to 40 degrees.");

      if (settings.MaxSlope < 0.0f)
        throw new ArgumentException("MaxSlope must be positive.");

      _settings = settings;
      _model = new GameboardModel(settings, visualise);
      _pathFinding = new PathFinding(_model);
    }

    public bool IsOnGameboard(Vector3 position, float delta)
    {
      var tile = Utils.PositionToTile(position, _settings.TileSize);

      if (_model.SpatialTree.GetElement(tile, out var node))
      {
        float elevation = node.Elevation;
        if (elevation > position.y + delta)
          return false;

        if (elevation + delta < position.y)
          return false;

        return true;
      }

      return false;
    }

    public bool FindNearestFreePosition(Vector3 sourcePosition, float range, out Vector3 nearestPosition)
    {
      nearestPosition = default;

      if (Area == 0)
        return false;

      var referencePoint = Utils.PositionToTile(sourcePosition, _settings.TileSize);

      // Define the search window
      var halfSize = Mathf.FloorToInt(range / _settings.TileSize);
      var anchor = new Vector2Int(referencePoint.x - halfSize, referencePoint.y - halfSize);
      var boundsOfSearch = new Bounds
      (
        bottomLeft: anchor,
        size: halfSize * 2
      );

      // Extract points within the search bounds
      var pointsOfInterest = _model.SpatialTree.Query(withinBounds: boundsOfSearch)?.ToList();

      // Find the closest point from candidates
      var success = Utils.GetNearestNode(pointsOfInterest, referencePoint, out var nearestNode);

      // Convert to world position
      nearestPosition = Utils.GridNodeToPosition(nearestNode, _settings.TileSize);
      return success;
    }

    public bool FindRandomPosition(out Vector3 randomPosition)
    {
      return _model.FindRandomPosition(out randomPosition);
    }

    public bool FindNearestFreePosition(Vector3 sourcePosition, out Vector3 nearestPosition)
    {
      // Get reference coordinates
      var referencePoint = Utils.PositionToTile(sourcePosition, _settings.TileSize);

      // Get neighboring points
      var pointsOfInterest = _model.SpatialTree.Query(neighboursTo: referencePoint)?.ToList();

      // Find the closest point from candidates
      var success = Utils.GetNearestNode(pointsOfInterest, referencePoint, out var nearestNode);

      // Convert to world position
      nearestPosition = Utils.GridNodeToPosition(nearestNode, _settings.TileSize);
      return success;
    }

    public bool FindRandomPosition(Vector3 sourcePosition, float range, out Vector3 randomPosition)
    {
      randomPosition = default;

      if (Area == 0)
        return false;

      var referencePoint = Utils.PositionToTile(sourcePosition, _settings.TileSize);

      // Define the search window
      var halfSize = Mathf.FloorToInt(range / _settings.TileSize);
      var anchor = new Vector2Int(referencePoint.x - halfSize, referencePoint.y - halfSize);
      var boundsOfSearch = new Bounds
      (
        bottomLeft: anchor,
        size: halfSize * 2
      );

      // Extract points within the search bounds
      var pointsOfInterest = _model.SpatialTree.Query(withinBounds: boundsOfSearch).ToList();
      if (pointsOfInterest.Count > 0)
      {
        // Get random unoccupied position
        var idx = Random.Range(0, pointsOfInterest.Count - 1);
        randomPosition = Utils.GridNodeToPosition(pointsOfInterest[idx], _settings.TileSize);
        return true;
      }

      // The search didn't yield any unoccupied nodes within bounds
      return false;
    }

    public bool CheckFit(Vector3 center, float size)
    {
      var surface = _model.Surfaces.FirstOrDefault
        (s => s.ContainsElement(Utils.PositionToGridNode(center, _settings.TileSize)));
      if (surface == null)
        return false;

      var r = (Vector3.right + Vector3.forward) * (size * 0.5f);
      var min = Utils.PositionToGridNode(center - r, _settings.TileSize);
      var max = Utils.PositionToGridNode(center + r, _settings.TileSize);
      var position = min.Coordinates;

      for (position.x = min.Coordinates.x; position.x <= max.Coordinates.x; position.x += 1)
        for (position.y = min.Coordinates.y; position.y <= max.Coordinates.y; position.y += 1)
          if (!surface.ContainsElement(new GridNode(position)))
            return false;

      return true;
    }

    private bool RayCast(Surface surface, Ray ray, out Vector3 hitPoint)
    {
      // Initialize resulting point
      hitPoint = Vector3.zero;

      if (surface.IsEmpty)
        return false;

      // Construct a mathematical plane
      var position = Utils.TileToPosition(surface.Elements.FirstOrDefault().Coordinates, _settings.TileSize);
      var p = new UnityEngine.Plane
        (Vector3.up, new Vector3(position.x, surface.Elevation, position.y));

      // Raycast plane
      if (p.Raycast(ray, out float enter))
      {
        // Check whether the hit point refers to a valid tile on the plane
        hitPoint = ray.GetPoint(enter);
        return surface.ContainsElement(Utils.PositionToGridNode(hitPoint, _settings.TileSize));
      }

      return false;
    }

    public bool RayCast(Ray ray, out Vector3 hitPoint)
    {
      hitPoint = Vector3.zero;

      var didHit = false;
      var minDistance = float.MaxValue;
      foreach (var entry in _model.Surfaces)
      {
        if (RayCast(entry, ray, out Vector3 raycastHit))
        {
          didHit = true;
          var dist = Vector3.Distance(ray.origin, raycastHit);
          if (dist < minDistance)
          {
            minDistance = dist;
            hitPoint = raycastHit;
          }
        }
      }

      return didHit;
    }

    public bool CalculatePath
      (Vector3 fromPosition, Vector3 toPosition, AgentConfiguration agent, out Path path)
    {
      bool result = _pathFinding.CalculatePath(fromPosition, toPosition, agent, out path);
      return result;
    }

    public void Scan(Vector3 origin, float range)
    {
      HashSet<Vector2Int> nodesDeleted = _model.Scan(origin, range);
      RecalculateArea();
    }

    /// Removes all surfaces from the Gameboard.
    public void Clear()
    {
      _model.Clear();
      RecalculateArea();
    }

    public void Prune(Vector3 keepNodesOrigin, float range)
    {
      _model.Prune(keepNodesOrigin, range);
      RecalculateArea();
    }
  }
}
