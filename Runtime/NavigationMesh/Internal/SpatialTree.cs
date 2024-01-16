// Copyright 2022-2024 Niantic.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
  public partial class SpatialTree
  {
    // The rank of the top level quads
    private readonly int _initialRank;

    // The size of each root quad
    // Each root can store N amount of GridNodes, where N = size * size
    private readonly int _rootSize;
    private readonly int _halfSize;

    // Store for root quads. Each root is identified with a location sensitive hash.
    private readonly Dictionary<long, Quad> _quads = new Dictionary<long, Quad>();

    /// Creates a new spatial tree instance.
    /// A spatial tree consists of a grid of quads, which themselves can subdivide.
    /// @param approximateQuadSize Defines the size of a quad. The actual size will
    ///   be less than or equal to this number.
    public SpatialTree(int approximateQuadSize)
    {
      _initialRank = (int)(Math.Log(approximateQuadSize) / Math.Log(2));
      _rootSize = (int)Mathf.Pow(2, _initialRank);
      _halfSize = _rootSize / 2;
    }

    /// Inserts the provided elements to the tree.
    /// @param gridNodes Grid nodes to insert.
    /// @returns Whether all nodes were inserted successfully.
    public bool Insert(IEnumerable<GridNode> gridNodes)
    {
      bool success = true;
      foreach (var gridNode in gridNodes)
      {
        var position = gridNode.Coordinates;
        var key = GetKey(forCoordinates: position);

        // Get or create (root) quad for the position
        Quad quad;
        if (_quads.ContainsKey(key))
        {
          quad = _quads[key];
        }
        else
        {
          quad = new Quad(SnapToGrid(position), _initialRank);
          _quads.Add(key, quad);
        }

        // Insert node
        success = success && quad.Insert(gridNode);
      }

      return success;
    }

    /// Removes the provided elements from the tree.
    /// @param gridNodes Grid nodes to remove.
    public HashSet<Vector2Int> Remove(IEnumerable<GridNode> gridNodes)
    {
      HashSet<Vector2Int> removedNodes = new HashSet<Vector2Int>();

      foreach (var gridNode in gridNodes)
      {
        // Find root quad and remove node from it
        var key = GetKey(forCoordinates: gridNode.Coordinates);
        if (_quads.ContainsKey(key))
        {
          bool removed = _quads[key].Remove(gridNode);
          if (removed)
            removedNodes.Add(gridNode.Coordinates);
        }
      }

      return removedNodes;
    }

    /// Returns the grid node at the specified location if exists.
    /// @param atPosition The location of the grid node.
    /// @param result The element stored at the specified location.
    /// @returns True, if the grid node could be located.
    public bool GetElement(Vector2Int atPosition, out GridNode result)
    {
      var key = GetKey(atPosition);
      if (!_quads.ContainsKey(key))
      {
        result = default;
        return false;
      }

      return _quads[key].GetElement(atPosition, out result);
    }

    public IEnumerable<GridNode> Query(Bounds withinBounds)
    {
      if (_quads.Count < 1)
        return null;

      // Aggregate quads that overlap the specified bounds
      var quads =
        _quads.Select(entry => entry.Value)
        .Where(quad => quad.Bounds.Intersects(withinBounds));

      // Extract points within bounds
      var result = new List<GridNode>();
      foreach (var entry in quads)
        result.AddRange(entry.GetElements(withinBounds));

      return result;
    }

    public IEnumerable<GridNode> Query(Vector2Int neighboursTo)
    {
      if (_quads.Count < 1)
        return null;

      // Find the quad enclosing the reference point
      var key = GetKey(neighboursTo);
      var source = _quads.ContainsKey(key) ? _quads[key] : null;

      // Find neighbours
      var elements = source?.GetNeighbours(neighboursTo);
      if (elements != null) return elements;

      // At this point, the reference is outside the mapped area.
      // Find the closest quad to the reference point.
      Quad nearest = FindNearestQuad(_quads.Values, neighboursTo);

      var nodes = nearest?.GetNeighbours(neighboursTo);
      return nodes ?? new GridNode[0];
    }

    public void Clear()
    {
      _quads.Clear();
    }

    /// Returns a key for finding the appropriate quad for the specified tile.
    private long GetKey(Vector2Int forCoordinates)
    {
      // Correct for 0-based indexing
      int xCoordinate = forCoordinates.x > 0 ? forCoordinates.x : forCoordinates.x + 1;
      int yCoordinate = forCoordinates.y > 0 ? forCoordinates.y : forCoordinates.y + 1;

      // Calculate indices on the quad grid.
      int n = xCoordinate / _halfSize;
      int m = yCoordinate / _halfSize;
      int x = _halfSize * (n + n % 2) / _rootSize;
      int y = _halfSize * (m + m % 2) / _rootSize;

      // Hash results
      ulong a = (ulong)(x >= 0 ? 2 * (long)x : -2 * (long)x - 1);
      ulong b = (ulong)(y >= 0 ? 2 * (long)y : -2 * (long)y - 1);
      long c = (long)((a >= b ? a * a + a + b : a + b * b) / 2);

      return x < 0 && y < 0 || x >= 0 && y >= 0 ? c : -c - 1;
    }

    /// Snaps the specified coordinates to the quad grid.
    private Vector2Int SnapToGrid(Vector2Int position)
    {
      // Correct for 0-based indexing
      int xCoordinate = position.x > 0 ? position.x : position.x + 1;
      int yCoordinate = position.y > 0 ? position.y : position.y + 1;

      int n = xCoordinate / _halfSize;
      int m = yCoordinate / _halfSize;

      return new Vector2Int(_halfSize * (n + n % 2), _halfSize * (m + m % 2));
    }

    private static Quad FindNearestQuad(IEnumerable<Quad> candidates, Vector2Int reference)
    {
      var minDistance = float.MaxValue;
      Quad nearest = null;
      foreach (var current in candidates)
      {
        var distance = Vector2Int.Distance(current.Bounds.Center, reference);
        if (distance < minDistance)
        {
          minDistance = distance;
          nearest = current;
        }
      }

      return nearest;
    }

    /// Visualizes the quad-tree in the editor's scene view.
    /// @param setting The LightshipNavMesh's configuration.
    public void DrawGizmos(ModelSettings settings)
    {
      var queue = new Queue<Quad>();

      // Draw spatial map
      foreach (var quad in _quads.Values)
        queue.Enqueue(quad);

      while (queue.Count > 0)
      {
        var quad = queue.Dequeue();

        // Draw root quad
        quad.Bounds.DrawGizmos(settings.TileSize, Color.yellow);

        if (!quad.IsLeaf)
        {
          var children = quad.GetChildren();
          foreach (var child in children)
            queue.Enqueue(child);
        }
      }
    }
  }
}
