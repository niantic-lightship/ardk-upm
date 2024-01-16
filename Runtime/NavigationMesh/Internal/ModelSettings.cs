// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.NavigationMesh
{
    /// <summary>
    /// The ModelSettings struct provides a configuration for how LightshipNavMesh scans the real environment and creates a navigable space.
    /// An instance of ModelSettings is created by the LightshipNavMeshManager using the parameters specified by the user in the Inspector,
    /// and that instance is subsequently used to create a LightshipNavMesh that is configured this way.
    /// </summary>
    [PublicAPI]
    public struct ModelSettings
    {
        /// <summary>
        /// Size of a grid cell in meters.
        /// </summary>
        public float TileSize;

        /// <summary>
        /// Size of a spatial partition in square meters.
        /// Grid cells within the same chunk will be stored together.
        /// </summary>
        public float SpatialChunkSize;

        /// <summary>
        /// The size of the kernel used to compute areal properties for each cell.
        /// @note This needs to be an odd integer.
        /// </summary>
        public int KernelSize;

        /// <summary>
        /// The standard deviation tolerance value to use when determining node noise within a cell,
        /// outside of which the cell is considered too noisy to be walkable.
        /// </summary>
        public float KernelStdDevTol;

        /// <summary>
        /// Maximum slope angle (degrees) of an area to be considered flat.
        /// </summary>
        public float MaxSlope;

        /// <summary>
        /// Minimum elevation (meters) a GridNode is expected to have in order to be walkable
        /// </summary>
        public float MinElevation;

        /// <summary>
        /// The maximum amount two cells can differ in elevation to be considered on the same plane.
        /// </summary>
        public float StepHeight;

        /// <summary>
        /// Specifies the layer of the environment to raycast.
        /// </summary>
        public LayerMask LayerMask;

        public ModelSettings
        (
            float tileSize,
            float kernelStdDevTol,
            float maxSlope,
            float stepHeight,
            LayerMask layerMask
        ) : this()
        {
            TileSize = tileSize;
            KernelStdDevTol = kernelStdDevTol;
            MaxSlope = maxSlope;
            StepHeight = stepHeight;
            LayerMask = layerMask;

            // Rest is default for now:
            SpatialChunkSize = 10.0f;
            KernelSize = 3;
            MinElevation = -10;
        }

        /// Constructs a configuration with default settings.
        public static ModelSettings Default
        {
            get =>
                new ModelSettings
                {
                    TileSize = 0.15f,
                    SpatialChunkSize = 10.0f,
                    KernelSize = 3,
                    KernelStdDevTol = 0.2f,
                    MaxSlope = 25.0f,
                    StepHeight = 0.1f,
                    LayerMask = 1,
                    MinElevation = -10.0f,
                };
        }
    }
}
