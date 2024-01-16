// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Random = UnityEngine.Random;

namespace Niantic.Lightship.AR.NavigationMesh
{
  public class NavMeshModel
  {
    public SpatialTree SpatialTree { get; }

    // Internal container for all surfaces.
    public List<Surface> Surfaces { get; }
    private int _nextSurfaceId = 0;
    private bool _visualise;

    public readonly ModelSettings Settings;

    public NavMeshModel(ModelSettings settings, bool visualise)
    {
      Settings = settings;
      _visualise = visualise;
      Surfaces = new List<Surface>();
      SpatialTree = new SpatialTree(Mathf.FloorToInt(settings.SpatialChunkSize / settings.TileSize));
    }

    public void ToggleVisualisation()
    {
      _visualise = !_visualise;
    }

    public HashSet<Vector2Int> Scan(Vector3 origin, float range)
    {
      // Cache parameters
      var kernelSize = Settings.KernelSize;
      var kernelHalfSize = kernelSize / 2;
      var tileSize = Settings.TileSize;
      var tileHalfSize = tileSize / 2.0f;
      const float rayLength = 100.0f;

      float halfRange = range / 2;

      // Calculate bounds for this scan on the grid
      var lowerBoundPosition = new Vector2(origin.x - halfRange, origin.z - halfRange);
      var upperBoundPosition = new Vector2(origin.x + halfRange, origin.z + halfRange);
      var lowerBounds = Utils.PositionToTile(lowerBoundPosition, Settings.TileSize);
      var upperBounds = Utils.PositionToTile(upperBoundPosition, Settings.TileSize);
      if (upperBounds.x - lowerBounds.x < kernelSize ||
        upperBounds.y - lowerBounds.y < kernelSize)
      {
        throw new ArgumentException("Range is too short for the specified tile size.");
      }

      // Bounds of the search area
      var w = upperBounds.x - lowerBounds.x;
      var h = upperBounds.y - lowerBounds.y;

      // Array to store information on the nodes resulting from this scan
      var scanArea = new GridNode[w * h];

      // Scan heights
      for (var x = 0; x < w; x++)
      {
        for (var y = 0; y < h; y++)
        {

          // Calculate the world position of the ray
          var coords = new Vector2Int(lowerBounds.x + x, lowerBounds.y + y);
          var position = new Vector3
          (
            coords.x * tileSize + tileHalfSize,
            origin.y,
            coords.y * tileSize + tileHalfSize
          );

          var arrayIndex = y * w + x;

          if (_visualise)
            Debug.DrawLine(position + Vector3.down, position + 2*Vector3.down, Color.green, 0.5f);

          // Raycast for height
          var elevation =
            Physics.Raycast
            (
              new Ray(position, Vector3.down),
              out RaycastHit hit,
              rayLength,
              layerMask: Settings.LayerMask
            )
              ? hit.point.y
              : -100;

          scanArea[arrayIndex] = new GridNode(coords)
          {
            DiffFromNeighbour = float.MaxValue, Elevation = elevation
          };
        }
      }

      // This set is used to register nodes that are obviously occupied
      var invalidate = new HashSet<GridNode>();

      // Calculate areal properties
      var kernel = new Vector3[kernelSize * kernelSize];
      for (var x = kernelHalfSize; x < w - kernelHalfSize; x++)
      {
        for (var y = kernelHalfSize; y < h - kernelHalfSize; y++)
        {
          // Construct kernel for this grid cell using its neighbours
          var kernelIndex = 0;
          for (var kx = -kernelHalfSize; kx <= kernelHalfSize; kx++)
          {
            for (var ky = -kernelHalfSize; ky <= kernelHalfSize; ky++)
            {
              var x1 = Mathf.Clamp(kx + x, 0, w - 1);
              var y1 = Mathf.Clamp(ky + y, 0, h - 1);
              kernel[kernelIndex++] = Utils.GridNodeToPosition(scanArea[y1 * w + x1], Settings.TileSize);
            }
          }

          var idx = y * w + x;

          // Try to fit a plane on the neighbouring points
          Utils.FastFitPlane(kernel, out Vector3 _, out Vector3 normal);

          // Assign standard deviation and slope angle
          var slope = Mathf.Abs(90.0f - Vector3.Angle(Vector3.forward, normal));
          var std = Utils.CalculateStandardDeviation(kernel.Select(pos => pos.y));
          scanArea[idx].Deviation = std;

          // Collect nodes that are occupied
          var isWalkable = std < Settings.KernelStdDevTol &&
            slope < Settings.MaxSlope &&
            scanArea[idx].Elevation > Settings.MinElevation;

          if (!isWalkable)
            invalidate.Add(scanArea[idx]);
        }
      }

      // Remove nodes that are occupied from existing planes
      HashSet<Vector2Int> removedNodes = InvalidateNodes(invalidate);

      var open = new Queue<GridNode>();
      var closed = new HashSet<GridNode>();
      var eligible = new HashSet<GridNode>();

      // Define seed as the center of the search area
      open.Enqueue(scanArea[(h / 2) * w + (w / 2)]);
      while (open.Count > 0)
      {
        // Extract current tile
        var currentNode = open.Dequeue();

        // Consider this node to be visited
        closed.Add(currentNode);

        if (invalidate.Contains(currentNode))
          continue; // Skip this node as it is occupied

        // Register this tile as unoccupied...
        eligible.Add(currentNode);

        var neighbours = Utils.GetNeighbours(currentNode.Coordinates);
        foreach (var neighbour in neighbours)
        {

          // Get the coordinates transformed to our local scan area
          var transformedNeighbour = neighbour - lowerBounds;
          if (transformedNeighbour.x < kernelHalfSize ||
            transformedNeighbour.x >= w - kernelHalfSize ||
            transformedNeighbour.y < kernelHalfSize ||
            transformedNeighbour.y >= h - kernelHalfSize)
          {
            continue; // Out of bounds
          }

          var arrayIndex = transformedNeighbour.y * w + transformedNeighbour.x;

          // If we've been here before
          if (closed.Contains(scanArea[arrayIndex]))
            continue;

          var diff = Mathf.Abs(currentNode.Elevation - scanArea[arrayIndex].Elevation);
          if (scanArea[arrayIndex].DiffFromNeighbour > diff)
          {
            scanArea[arrayIndex].DiffFromNeighbour = diff;
          }

          // Can we walk from the current node to this neighbour?
          var isEligible = !open.Contains(scanArea[arrayIndex]) &&
            scanArea[arrayIndex].DiffFromNeighbour <= Settings.StepHeight;

          if (isEligible)
            open.Enqueue(scanArea[arrayIndex]);
        }
      }

      if (eligible.Count >= 2)
      {
        // Merge newly found unoccupied areas with existing planes
        MergeNodes(eligible);
      }

      return removedNodes;
    }

    /// Removes all surfaces from the board.
    public void Clear()
    {
      if (Surfaces.Count == 0)
        return ;

      Surfaces.Clear();
      SpatialTree.Clear();
    }

    public void Prune(Vector3 keepNodesOrigin, float range)
    {
      if (Surfaces.Count == 0)
        return;

      float halfRange = range / 2;

      var topRight = keepNodesOrigin +
        Vector3.right * halfRange +
        Vector3.forward * halfRange;

      var bottomLeft = keepNodesOrigin +
        Vector3.left * halfRange +
        Vector3.back * halfRange;

      var min = Utils.PositionToGridNode(bottomLeft, Settings.TileSize);
      var max = Utils.PositionToGridNode(topRight, Settings.TileSize);

      var bounds = new Bounds(min.Coordinates, max.Coordinates.x - min.Coordinates.x);
      var toKeep = SpatialTree.Query(withinBounds: bounds).ToList();

      SpatialTree.Clear();
      SpatialTree.Insert(toKeep);

      // Remove tiles for surfaces
      Surfaces.ForEach(surface => surface.Intersect(toKeep));

      // Clean empty surfaces
      Surfaces.RemoveAll(surface => surface.IsEmpty);
    }

    /// Invalidates the specified nodes of existing planes.
    private HashSet<Vector2Int> InvalidateNodes(HashSet<GridNode> nodes)
    {
      // Remove nodes from registry
      HashSet<Vector2Int> removedNodes = SpatialTree.Remove(nodes);

      // Remove nodes from its respective surfaces
      Surfaces.ForEach(entry => entry.Except(nodes));

      // Clean up empty planes
      Surfaces.RemoveAll(entry =>entry.IsEmpty);

      return removedNodes;
    }

    /// Merges new unoccupied nodes with existing planes. If the nodes cannot be merged, a new plane is created.
    private void MergeNodes(HashSet<GridNode> nodes)
    {
      // Register new unoccupied nodes
      SpatialTree.Insert(nodes);

      // Create a new planes from the provided (unoccupied) nodes
      var candidate = new Surface(nodes, _nextSurfaceId);
      _nextSurfaceId++;

      // Just add the candidate plane to the list if this is the first one we found
      if (Surfaces.Count == 0)
      {
        Surfaces.Add(candidate);
        return;
      }

      // Gather overlapping planes
      var overlappingPlanes = Surfaces.Where(entry => entry.Overlaps(candidate)).ToList();

      // No overlap, add candidate as a new plane
      if (!overlappingPlanes.Any())
      {
        Surfaces.Add(candidate);
        return;
      }

      // Find an overlapping plane that satisfies the merging conditions
      var anchorPlane = overlappingPlanes.FirstOrDefault
      (
        entry =>
          entry.CanMerge(candidate, Settings.StepHeight * 2.0f)
      );

      // No such plane
      if (anchorPlane == null)
      {
        // Exclude its nodes from existing planes
        foreach (var surface in overlappingPlanes)
        {
          surface.Except(candidate);
        }

        // Remove planes that were a subset of the candidate
        Surfaces.RemoveAll(surface => surface.IsEmpty);

        // Add candidate as a new plane
        Surfaces.Add(candidate);
        return;
      }

      // Base plane found to merge the new nodes to
      anchorPlane.Merge(candidate);

      // Iterate through other overlapping planes except this base plane
      overlappingPlanes.Remove(anchorPlane);
      foreach (var entry in overlappingPlanes)
      {
        // Either merge or exclude nodes
        if (anchorPlane.CanMerge(entry, Settings.StepHeight * 2.0f))
        {
          anchorPlane.Merge(entry);
          Surfaces.Remove(entry);
        }
        else
        {
          entry.Except(candidate);
        }
      }
    }

    public bool FindRandomPosition(out Vector3 randomPosition)
    {
      if (Surfaces.Count == 0)
      {
        randomPosition = Vector3.zero;
        return false;
      }

      int randomSurface = Random.Range(0, Surfaces.Count-1);
      int randomNode = Random.Range(0, Surfaces[randomSurface].Elements.Count());

      randomPosition = Utils.GridNodeToPosition
        (Surfaces[randomSurface].Elements.ElementAt(randomNode), Settings.TileSize);

      return true;
    }
  }
}
