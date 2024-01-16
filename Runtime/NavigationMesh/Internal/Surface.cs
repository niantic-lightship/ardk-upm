// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using System.Linq;

//using Niantic.ARDK.Utilities.Collections;

using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
  /// Represents a subset on the grid with cells of approximately the same elevation.
  public class Surface
  {
    private int Id { get; }

    /// Elevation of the grid.
    public float Elevation { get; private set; }

    /// Whether this planes does not contain any valid elements on the grid.
    public bool IsEmpty
    {
      get
      {
        return _elements.Count == 0;
      }
    }

    /// Internal container for the nodes that make up this plane.
    private readonly HashSet<GridNode> _elements;

    /// Grid nodes that  make up this plane (readonly).
    public IEnumerable<GridNode> Elements
    {
      get
      {
        return _elements;
      }
    }

    public Surface(IEnumerable<GridNode> gridElements, int id)
    {
      _elements = new HashSet<GridNode>(gridElements);
      Elevation = CalculateElevation(_elements);

      Id = id;
    }

    public bool ContainsElement(GridNode gridCell)
    {
      return _elements.Contains(gridCell);
    }

    /// Merges the other plane's grid elements to this plane.
    public void Merge(Surface other)
    {
      // Union, but prefer to keep the shared cells of the new plane
      _elements.ExceptWith(other._elements);
      _elements.UnionWith(other._elements);
      Elevation = CalculateElevation(_elements);
    }

    public bool CanMerge(Surface other, float elevationTreshold)
    {
      var otherElements = new HashSet<GridNode>(other._elements);
      otherElements.IntersectWith(_elements);
      if (otherElements.Count == 0)
      {
        return false;
      }

      var thisElements = new HashSet<GridNode>(_elements);
      thisElements.IntersectWith(other._elements);

      var foreignElevation = CalculateElevation(otherElements);
      return Mathf.Abs(foreignElevation - CalculateElevation(thisElements)) <= elevationTreshold &&
        Mathf.Abs(foreignElevation - Elevation) < elevationTreshold;
    }

    public bool Overlaps(Surface other)
    {
      return _elements.Overlaps(other._elements);
    }

    public void Except(Surface other)
    {
      _elements.ExceptWith(other._elements);
    }

    public void Except(IEnumerable<GridNode> nodes)
    {
      _elements.ExceptWith(nodes);
    }

    public void Intersect(IEnumerable<GridNode> nodes)
    {
      _elements.IntersectWith(nodes);
    }

    public GridNode GetClosestElement(Vector2Int reference)
    {
      return _elements.Aggregate
      (
        (closest, entry) =>
          Vector2Int.Distance
            (entry.Coordinates, reference) <
          Vector2Int.Distance(closest.Coordinates, reference)
            ? entry
            : closest
      );
    }

    private static float CalculateElevation(IEnumerable<GridNode> nodes)
    {
      var groupElevation = 0.0f;
      var groupElevationAvgCount = 0;
      foreach (var entry in nodes)
      {
        // Standard deviation tells us the general flatness around this cell
        // When calculating the elevation of the plane,
        // flat areas contribute more to the combined value
        var elevationWeight = Mathf.FloorToInt((0.1f - entry.Deviation) * 100);
        groupElevation += entry.Elevation * elevationWeight;
        groupElevationAvgCount += elevationWeight;
      }

      // AVG elevation
      groupElevation /= groupElevationAvgCount;

      return groupElevation;
    }
  }
}
