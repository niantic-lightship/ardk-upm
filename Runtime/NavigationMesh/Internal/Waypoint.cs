// Copyright 2022-2024 Niantic.

using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
    public readonly struct Waypoint
    {
        /// The type of movement of a waypoint
        public enum MovementType
        {
            /// Walk node.
            Walk = 0,

            /// The first node of a new surface on the path.
            SurfaceEntry = 1
        }

        /// The position of this point in world coordinates.
        public readonly Vector3 WorldPosition;

        /// The type of movement of this waypoint.
        public readonly MovementType Type;

        public readonly Vector2Int Coordinates;

        public Waypoint(Vector3 worldPosition, MovementType type, Vector2Int coordinates)
        {
            WorldPosition = worldPosition;
            Type = type;
            Coordinates = coordinates;
        }
    }
}
