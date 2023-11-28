// Copyright 2022-2023 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
    /// Encloses data for grid elements used during scanning for walkable areas.
    [PublicAPI]
    public struct GridNode : IEquatable<GridNode>
    {
        /// Coordinates of this node on the grid.
        public readonly Vector2Int Coordinates;

        /// Height of the node.
        public float Elevation;

        /// Standard deviation in the area around the node.
        public float Deviation;

        /// The calculated minimum difference in elevation from a neighbouring node.
        public float DiffFromNeighbour;

        public GridNode(Vector2Int coordinates) : this()
        {
            Coordinates = coordinates;
        }

        public bool Equals(GridNode other)
        {
            return Coordinates.Equals(other.Coordinates);
        }

        public override int GetHashCode()
        {
            return Coordinates.GetHashCode();
        }
    }
}
