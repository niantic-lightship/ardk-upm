// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.NavigationMesh.Internal;
using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
  public static class Utils
  {
    /// Calculates the manhattan distance between two nodes.
    internal static int ManhattanDistance(PathFindingNode from, PathFindingNode to)
    {
      return Math.Abs
          (from.Coordinates.x - to.Coordinates.x) +
        Math.Abs(from.Coordinates.y - to.Coordinates.y);
    }

    /// Calculates the standard deviation of the provided sample.
    internal static float CalculateStandardDeviation(IEnumerable<float> samples)
    {
      var m = 0.0f;
      var s = 0.0f;
      var k = 1;
      foreach (var value in samples)
      {
        var tmpM = m;
        m += (value - tmpM) / k;
        s += (value - tmpM) * (value - m);
        k++;
      }

      return Mathf.Sqrt(s / Mathf.Max(1, k - 1));
    }

    /// Fits a plane to best align with the specified set of points.
    public static void FastFitPlane
    (
      Vector3[] points,
      out Vector3 position,
      out Vector3 normal
    )
    {
      position = default;
      normal = default;

      var n = points.Length;
      if (n < 3)
      {
        return;
      }

      var sum = Vector3.zero;
      for (var i = 0; i < points.Length; i++)
        sum += points[i];

      position = sum * (1.0f / n);

      var xx = 0.0f;
      var xy = 0.0f;
      var xz = 0.0f;
      var yy = 0.0f;
      var yz = 0.0f;
      var zz = 0.0f;

      for (var i = 0; i < points.Length; i++)
      {
        var r = points[i] - position;
        xx += r.x * r.x;
        xy += r.x * r.y;
        xz += r.x * r.z;
        yy += r.y * r.y;
        yz += r.y * r.z;
        zz += r.z * r.z;
      }

      xx /= n;
      xy /= n;
      xz /= n;
      yy /= n;
      yz /= n;
      zz /= n;

      var weightedDir = Vector3.zero;

      {
        var detX = yy * zz - yz * yz;
        var axisDir = new Vector3
        (
          x: detX,
          y: xz * yz - xy * zz,
          z: xy * yz - xz * yy
        );

        var weight = detX * detX;
        weightedDir += axisDir * weight;
      }

      {
        var detY = xx * zz - xz * xz;
        var axisDir = new Vector3
        (
          x: xz * yz - xy * zz,
          y: detY,
          z: xy * xz - yz * xx
        );

        var weight = detY * detY;
        weightedDir += axisDir * weight;
      }

      {
        var detZ = xx * yy - xy * xy;
        var axisDir = new Vector3
        (
          x: xy * yz - xz * yy,
          y: xy * xz - yz * xx,
          z: detZ
        );

        var weight = detZ * detZ;
        weightedDir += axisDir * weight;
      }

      float num = Vector3.Magnitude(weightedDir);
      normal = weightedDir / num;
    }

    /// Insert a value into an IList{T} that is presumed to be already sorted such that sort
    internal static void InsertIntoSortedList<T>(this IList<T> list, T value, Comparison<T> comparison)
    {
      var startIndex = 0;
      var endIndex = list.Count;
      while (endIndex > startIndex)
      {
        var windowSize = endIndex - startIndex;
        var middleIndex = startIndex + (windowSize / 2);
        var middleValue = list[middleIndex];
        var compareToResult = comparison(middleValue, value);
        if (compareToResult == 0)
        {
          list.Insert(middleIndex, value);
          return;
        }

        if (compareToResult < 0)
        {
          startIndex = middleIndex + 1;
        }
        else
        {
          endIndex = middleIndex;
        }
      }
      list.Insert(startIndex, value);
    }


    /// Returns the 8 neighbouring tiles of the specified coordinate.
    internal static IEnumerable<Vector2Int> GetNeighbours(Vector2Int vertex)
    {
      return new[]
      {
        new Vector2Int(vertex.x + 1, vertex.y),
        new Vector2Int(vertex.x - 1, vertex.y),
        new Vector2Int(vertex.x, vertex.y + 1),
        new Vector2Int(vertex.x, vertex.y - 1),
        new Vector2Int(vertex.x - 1, vertex.y + 1),
        new Vector2Int(vertex.x + 1, vertex.y + 1),
        new Vector2Int(vertex.x - 1, vertex.y - 1),
        new Vector2Int(vertex.x + 1, vertex.y - 1)
      };
    }

    /// Finds the closest node to the specified reference in candidates.
    internal static bool GetNearestNode(IList<GridNode> candidates, Vector2Int reference, out GridNode nearestNode)
    {
      // Helpers
      var minDistance = float.MaxValue;
      var success = false;

      // Initialize result
      nearestNode = default;

      if (candidates == null)
        return false;

      // Find nearest
      for (int i = 0; i < candidates.Count; i++)
      {
        var point = candidates[i];
        var distance = Vector2Int.Distance(point.Coordinates, reference);

        if (distance < minDistance)
        {
          // Found a candidate
          success = true;
          minDistance = distance;
          nearestNode = point;
        }
      }

      return success;
    }

    /// Converts a world position to grid coordinates.
    internal static Vector2Int PositionToTile(Vector2 position, float tileSize)
    {
      return new Vector2Int
      (
        Mathf.FloorToInt(position.x / tileSize),
        Mathf.FloorToInt(position.y / tileSize)
      );
    }

    /// Converts a world position to grid coordinates.
    /// @param position Position to convert.
    /// @param tileSize Metric size of each tile in the grid.
    public static Vector2Int PositionToTile(Vector3 position, float tileSize)
    {
      return new Vector2Int
      (
        Mathf.FloorToInt(position.x / tileSize),
        Mathf.FloorToInt(position.z / tileSize)
      );
    }

    /// Converts a grid coordinate to world position.
    internal static Vector2 TileToPosition(Vector2Int tile, float tileSize)
    {
      var halfSize = tileSize / 2.0f;
      return new Vector2
        (tile.x * tileSize + halfSize, tile.y * tileSize + halfSize);
    }

    /// Converts a grid coordinate to world position.
    internal static Vector3 TileToPosition(Vector2Int tile, float elevation, float tileSize)
    {
      var halfSize = tileSize / 2.0f;
      return new Vector3
      (
        tile.x * tileSize + halfSize,
        elevation,
        tile.y * tileSize + halfSize
      );
    }

    /// Converts a world position to a node on the LightshipNavMesh.
    internal static GridNode PositionToGridNode(Vector3 worldPosition, float tileSize)
    {
      return new GridNode(PositionToTile(worldPosition, tileSize));
    }

    /// Converts a node on the LightshipNavMesh to its corresponding world position.
    internal static Vector3 GridNodeToPosition(GridNode node, float tileSize)
    {
      return TileToPosition(node.Coordinates, node.Elevation, tileSize);
    }
  }
}
