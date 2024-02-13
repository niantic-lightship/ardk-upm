// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Linq;

using Niantic.Lightship.AR.Utilities.Logging;
//using Niantic.ARDK.Utilities.Collections;

using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
  public partial class SpatialTree
  {
    private class Quad
    {
      // The bounding area of this quad
      public Bounds Bounds { get; }

      // Store for items
      private List<GridNode> _elements;

      // Store for child quads, if any
      private Quad[] _children;

      // The number of items a quad can store before subdivision
      private const int AtomicCapacity = 4;

      // The possible depth of this quad
      private readonly int _rank;

      // The parent of this quadrant
      private readonly WeakReference<Quad> _parent;

      /// Returns whether this quad is a leaf bucket.
      public bool IsLeaf
      {
        get => _children == null;
      }

      public bool HasElements
      {
        get => _elements != null && _elements.Count > 0;
      }

      /// Returns the child quadrants of this parent.
      public IEnumerable<Quad> GetChildren()
      {
        return _children;
      }

      /// Allocates a new quad.
      /// @param origin The center of the quad.
      /// @param rank Determines the number of subdivisions possible (rank - 1)
      public Quad(Vector2Int origin, int rank)
      {
        // Minimum rank is 1 (stores 4 items)
        _rank = rank < 1 ? 1 : rank;

        // Calculate the size of the bounding square's size
        var size = (int)Mathf.Pow(2, _rank);
        var halfSize = size / 2;

        // Create the bounding square
        Bounds = new Bounds
        (
          // Top left corner position (origin - size/2)
          bottomLeft: origin - new Vector2Int(halfSize, halfSize),
          size: size
        );

        // Every quad is a leaf until it gets subdivided
        _elements = new List<GridNode>(AtomicCapacity);
        _children = null;

        // Root node
        _parent = new WeakReference<Quad>(null);
      }

      private Quad(Bounds bounds, int rank, Quad parent)
      {
        // Minimum rank is 1 (stores 4 items)
        _rank = rank;

        // Set bounds
        Bounds = bounds;

        // Every quad is a leaf until it gets subdivided
        _elements = new List<GridNode>(AtomicCapacity);
        _children = null;

        // Set parent
        _parent = new WeakReference<Quad>(parent);
      }

      /// Inserts a new item into this quad.
      public bool Insert(GridNode node)
      {
        // Discard points that are outside of bounds
        if (!Bounds.ContainsPoint(node.Coordinates))
          return false;

        if (IsLeaf)
        {
          // Point is already registered (at position)
          if (_elements.Contains(node))
          {
            // Update node
            var idx = _elements.FindIndex(n => n.Equals(node));
            _elements[idx] = node;

            // Node updated
            return true;
          }

          // Maximum capacity is not yet reached
          if (_elements.Count < AtomicCapacity)
          {
            // Add item
            _elements.Add(node);

            // Node inserted
            return true;
          }

          // Maximum capacity is reached
          if (!Subdivide())
            return false;
        }

        // Add item to to a child node
        var itemInserted =
          _children[0].Insert(node) ||
          _children[1].Insert(node) ||
          _children[2].Insert(node) ||
          _children[3].Insert(node);

        return itemInserted;
      }

      /// Removes an item from this quad, if contained.
      public bool Remove(GridNode node)
      {
        var quad = FindLeaf(node.Coordinates);
        if (quad == null)
          return false;

        var elements = quad._elements;
        if (elements.Contains(node))
        {
          // Remove node from elements
          elements.Remove(node);

          // Merge containing quad with siblings if necessary
          if (elements.Count == 0)
            if (quad._parent.TryGetTarget(out var parent))
              MergeIfEmpty(parent);

          return true;
        }

        return false;
      }

      /// Returns all points in the area defined by the quad boundaries.
      public IEnumerable<GridNode> GetElements()
      {
        if (IsLeaf)
          return _elements;

        var result = new List<GridNode>();
        var stack = new Stack<Quad>();
        stack.Push(this);

        while (stack.Count > 0)
        {
          // Get next quad
          var current = stack.Pop();

          // If this quad is not a leaf, it does not store any elements
          if (!current.IsLeaf)
          {
            // Add children to the queue
            for (int i = 0; i < current._children.Length; i++)
              stack.Push(current._children[i]);
          }
          // If it is a leaf, collect its elements
          else
            result.AddRange(current._elements);
        }

        return result;
      }

      /// Returns elements of this quad within the specified boundaries.
      /// @param withinBounds The enclosing boundaries.
      /// @returns A new list of stored elements within the specified bounds.
      public IEnumerable<GridNode> GetElements(Bounds withinBounds)
      {
        var result = new List<GridNode>();
        var stack = new Stack<Quad>();
        stack.Push(this);

        while (stack.Count > 0)
        {
          // Get next quad
          var current = stack.Pop();

          // If this quad shares no common area with the search bounds, skip
          if (!current.Bounds.Intersects(withinBounds))
            continue;

          // If this quad is not a leaf, it does not store any elements
          if (!current.IsLeaf)
          {
            // Add children to the queue
            for (int i = 0; i < current._children.Length; i++)
              stack.Push(current._children[i]);
          }
          else
          {
            // Add elements within bounds
            for (int i = 0; i < current._elements.Count; i++)
            {
              var entry = current._elements[i];
              if (withinBounds.ContainsPoint(entry.Coordinates))
                result.Add(entry);
            }
          }
        }

        return result;
      }

      public bool GetElement(Vector2Int atCoordinates, out GridNode result)
      {
        var quad = FindLeaf(atCoordinates);
        if (quad == null)
        {
          result = default;
          return false;
        }

        for (int i = 0; i < quad._elements.Count; i++)
        {
          result = quad._elements[i];
          if (result.Coordinates.Equals(atCoordinates))
            return true;
        }

        result = default;
        return false;
      }

      public IEnumerable<GridNode> GetNeighbours(Vector2Int toCoordinates)
      {
        var leaf = Bounds.ContainsPoint(toCoordinates)
          ? FindLeaf(toCoordinates)
          : FindNearestLeaf(toCoordinates);

        return leaf.HasElements
          ? leaf.GetElements()
          : leaf.FindNearestElementsInSiblings(toCoordinates);
      }

      private Quad FindLeaf(Vector2Int forCoordinates)
      {
        var stack = new Stack<Quad>();
        stack.Push(this);

        while (stack.Count > 0)
        {
          var current = stack.Pop();
          if (!current.Bounds.ContainsPoint(forCoordinates))
            continue;

          if (current.IsLeaf)
            return current;

          for (int i = 0; i < current._children.Length; i++)
            stack.Push(current._children[i]);
        }

        return null;
      }

      private Quad FindNearestLeaf(Vector2Int toCoordinates)
      {
        var stack = new Stack<Quad>();
        stack.Push(this);

        while (stack.Count > 0)
        {
          var current = stack.Pop();
          if (current.IsLeaf)
            return current;

          var minDistance = float.MaxValue;
          Quad nearest = current._children[0];
          for (int i = 0; i < current._children.Length; i++)
          {
            var child = current._children[i];
            var dist = Vector2Int.Distance(child.Bounds.Center, toCoordinates);
            if (dist < minDistance)
            {
              minDistance = dist;
              nearest = child;
            }
          }

          stack.Push(nearest);
        }

        return null;
      }

      private IEnumerable<GridNode> FindNearestElementsInSiblings(Vector2Int reference)
      {
        if (!_parent.TryGetTarget(out Quad parent))
          return new List<GridNode>();

        var stack = new Stack<Quad>();
        var siblings = parent._children;

        for (var i = 0; i < siblings.Length; i++)
        {
          var child = siblings[i];
          if (child != this)
            stack.Push(child);
        }

        var result = new List<GridNode>();
        while (stack.Count > 0)
        {
          var current = stack.Pop();
          if (current.IsLeaf)
          {
            // Add elements to the list
            if (current.HasElements)
              result.AddRange(current._elements);

            // Discard empty
            continue;
          }

          GetNearestChildrenWithElements
          (
            withinQuad: current,
            toPoint: reference,
            out Quad near1,
            out Quad near2
          );

          if (near1 != null)
            stack.Push(near1);

          if (near2 != null)
            stack.Push(near2);
        }

        return result;
      }

      /// Finds the two nearest quadrants to the reference point.
      private static void GetNearestChildrenWithElements
        (Quad withinQuad, Vector2Int toPoint, out Quad firstNearest, out Quad secondNearest)
      {
        // Helpers
        var min1 = float.MaxValue;
        var min2 = float.MaxValue;

        // Defaults
        firstNearest = null;
        secondNearest = null;

        for (int i = 0; i < withinQuad._children.Length; i++)
        {
          var entry = withinQuad._children[i];
          if (entry.IsLeaf && !entry.HasElements)
            continue;

          var dist = Vector2Int.Distance(entry.Bounds.Center, toPoint);
          if (dist < min1)
          {
            // Transfer first nearest to second
            min2 = min1;
            secondNearest = firstNearest;

            // Assign new first quadrant
            min1 = dist;
            firstNearest = entry;
          }
          else if (dist < min2)
          {
            // Assign new second quadrant
            min2 = dist;
            secondNearest = entry;
          }
        }
      }

      private bool Subdivide()
      {
        if (_rank < 2)
          return false;

        var childRank = _rank - 1;
        var childSize = Bounds.Size / 2;
        var anchor = Bounds.BottomLeft;

        // Allocate child store
        _children = new Quad[4];

        // Helpers
        var childIndex = 0;
        var itemsSorted = 0;

        for (int x = 0; x < 2; x++)
        {
          for (int y = 0; y < 2; y++)
          {
            // Create child node
            _children[childIndex] = new Quad
            (
              bounds: new Bounds(anchor + new Vector2Int(x * childSize, y * childSize), childSize),
              rank: childRank,
              parent: this
            );

            // Insert items
            itemsSorted += _elements.Count(item => _children[childIndex].Insert(item));

            // Done
            childIndex++;
          }
        }

        if (itemsSorted != _elements.Count)
        {
          // This error is for when nodes in this quad fail to
          // be assigned to child quadrants during subdivision.
          Log.Error("Quad failed to sort its elements during subdivision.");
          return false;
        }

        // Release items
        _elements = null;

        // Done
        return true;
      }

      private static void MergeIfEmpty(Quad quad)
      {
        while (true)
        {
          if (quad.IsLeaf)
            return;

          // Merge if all children are empty leaves
          var merge = !quad._children.Any(child => !child.IsLeaf || child.HasElements);
          if (merge)
          {
            // Merge
            quad._children = null;
            quad._elements = new List<GridNode>(AtomicCapacity);

            // Try to merge enclosing quad
            if (quad._parent.TryGetTarget(out var parent))
            {
              quad = parent;
              continue;
            }
          }

          break;
        }
      }
    }
  }
}
