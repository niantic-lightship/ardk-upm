// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh.Internal
{
  /// Encloses data for grid elements used during path finding.
  internal struct PathFindingNode: IEquatable<PathFindingNode>, IComparable<PathFindingNode>
  {
    /// The coordinates of this parent.
    public readonly Vector2Int Coordinates;

    /// The coordinates of this node's parent.
    public readonly Vector2Int ParentCoordinates;

    /// Elevation of the node.
    public readonly float Elevation;

    /// Whether this path node has a parent assign.
    public readonly bool HasParent;

    /// Cost to get to this node for the source in a path finding context.
    public int CostToThis;

    /// Cost to get from this node to the destination in a path finding context.
    public float CostToGoal;

    /// The number of continuous nodes without a surface.
    public int AggregateOffSurface;

    /// The surface this node belongs to. Could be null.
    public readonly Surface Surface;

    /// Combined cost of this node.
    public readonly float Cost
    {
      get
      {
        return CostToThis + CostToGoal;
      }
    }

    public PathFindingNode(Vector2Int coordinates, Surface surface)
      : this()
    {
      Coordinates = coordinates;
      Elevation = surface.Elevation;
      Surface = surface;
    }

    public PathFindingNode(Vector2Int coordinates, float elevation)
      : this()
    {
      Coordinates = coordinates;
      Elevation = elevation;
      Surface = null;
    }

    public PathFindingNode(Vector2Int coordinates, Surface surface, Vector2Int parentCoordinates)
      : this(coordinates, surface)
    {
      ParentCoordinates = parentCoordinates;
      HasParent = true;
    }

    public PathFindingNode(Vector2Int coordinates, float elevation, Vector2Int parentCoordinates)
      : this(coordinates, elevation)
    {
      ParentCoordinates = parentCoordinates;
      HasParent = true;
    }

    public bool Equals(PathFindingNode other)
    {
      return Coordinates.Equals(other.Coordinates);
    }

    public override int GetHashCode()
    {
      return Coordinates.GetHashCode();
    }

    public readonly int CompareTo(PathFindingNode other)
    {
      return Cost.CompareTo(other.Cost);
    }

    public override bool Equals(object obj)
    {
        return obj is PathFindingNode node && Equals(node);
    }
  }
}
