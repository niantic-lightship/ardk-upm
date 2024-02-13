// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
    /// <summary>
    /// Encloses data for grid elements used during scanning for walkable areas.
    /// </summary>
    [PublicAPI]
    public struct GridNode : IEquatable<GridNode>
    {
        /// <summary>
        /// Coordinates of this node on the grid.
        /// </summary>
        public readonly Vector2Int Coordinates;

        /// <summary>
        /// Height of the node.
        /// </summary>
        public float Elevation;

        /// <summary>
        /// Standard deviation in the area around the node.
        /// </summary>
        public float Deviation;

        /// <summary>
        /// The calculated minimum difference in elevation from a neighbouring node.
        /// </summary>
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

        public override bool Equals(object obj)
        {
            return obj is GridNode node && Equals(node);
        }

        public static bool operator ==(GridNode left, GridNode right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridNode left, GridNode right)
        {
            return !(left == right);
        }
    }
}
