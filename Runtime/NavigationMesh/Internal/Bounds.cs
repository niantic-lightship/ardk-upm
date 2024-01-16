// Copyright 2022-2024 Niantic.

using System;

using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
  /// Specifies an area in a top-down 2D grid using a square.
  public readonly struct Bounds: IEquatable<Bounds>
  {
    /// Bottom left point (from a top-down perspective)
    private readonly Vector2Int _anchor;

    /// Returns the center of the square.
    public Vector2Int Center
    {
      get => new Vector2Int(_anchor.x + Size / 2, _anchor.y + Size / 2);
    }

    /// Returns the bottom left corner point (this is also the anchor of this square).
    public Vector2Int BottomLeft
    {
      get => _anchor;
    }

    /// Returns the bottom right corner point.
    public Vector2Int BottomRight
    {
      get => new Vector2Int(_anchor.x + Size, _anchor.y);
    }

    /// Returns the top right corner point.
    public Vector2Int TopRight
    {
      get => new Vector2Int(_anchor.x + Size, _anchor.y + Size);
    }

    /// Returns the top left corner point.
    public Vector2Int TopLeft
    {
      get => new Vector2Int(_anchor.x, _anchor.y + Size);
    }

    /// The size of each side.
    public readonly int Size;

    public Bounds(Vector2Int bottomLeft, int size)
    {
      _anchor = bottomLeft;
      Size = size;
    }

    /// Returns true if the specified point is within bounds.
    public bool ContainsPoint(Vector2Int point)
    {
      return point.x >= _anchor.x &&
        point.x < _anchor.x + Size &&
        point.y >= _anchor.y &&
        point.y < _anchor.y + Size;
    }

    /// Returns true if the specified bounds overlap with this one.
    public bool Intersects(Bounds other)
    {
      var thisHorizontalRange = _anchor.x + Size;
      var thisVerticalRange = _anchor.y + Size;
      var otherHorizontalRange = other._anchor.x + other.Size;
      var otherVerticalRange = other._anchor.y + other.Size;

      var horizontalOverlap =
        _anchor.x >= other._anchor.x && _anchor.x <= otherHorizontalRange ||
        other._anchor.x >= _anchor.x && other._anchor.x <= thisHorizontalRange;

      var verticalOverlap =
        _anchor.y >= other._anchor.y && _anchor.y <= otherVerticalRange ||
        other._anchor.y >= _anchor.y && other._anchor.y <= thisVerticalRange;

      return horizontalOverlap && verticalOverlap;
    }

    public void DrawGizmos(float unitSize, Color color)
    {
      var metricSize = Size * unitSize;
      var metricAnchor = new Vector3(_anchor.x * unitSize, 0.0f, _anchor.y * unitSize);

      Gizmos.color = color;

      Gizmos.DrawLine(metricAnchor, metricAnchor + Vector3.right * metricSize);
      Gizmos.DrawLine(metricAnchor, metricAnchor + Vector3.forward * metricSize);
      Gizmos.DrawLine
      (
        metricAnchor + Vector3.right * metricSize,
        metricAnchor + new Vector3(metricSize, 0.0f, metricSize)
      );
      Gizmos.DrawLine
      (
        metricAnchor + Vector3.forward * metricSize,
        metricAnchor + new Vector3(metricSize, 0.0f, metricSize)
      );

      Gizmos.color = Color.white;
    }

    public bool Equals(Bounds other)
    {
      return _anchor.Equals(other._anchor) && Size == other.Size;
    }

    public override bool Equals(object obj)
    {
      return obj is Bounds other && Equals(other);
    }

    public override int GetHashCode()
    {
      unchecked
      {
        return (_anchor.GetHashCode() * 397) ^ Size;
      }
    }
  }
}
