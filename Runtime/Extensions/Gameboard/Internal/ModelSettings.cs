// Copyright 2022 Niantic, Inc. All Rights Reserved.

using UnityEngine;

namespace Niantic.Lightship.AR.Extensions.Gameboard {
  public struct ModelSettings {

    
    /// Metric size of a grid cell.
    public float TileSize;

    /// Size of a spatial partition in square meters.
    /// Grid cells within the same area will be stored together.
    public float SpatialChunkSize;

    /// The size of the kernel used to compute areal properties for each cell.
    /// @note This needs to be an odd integer.
    public int KernelSize;

    /// The standard deviation tolerance value to use when determining node noise within a cell,
    /// outside of which the cell is considered too noisy to be walkable.
    public float KernelStdDevTol;

    /// Maximum slope angle (degrees) of an area to be considered flat.
    public float MaxSlope;

    /// Minimum elevation (meters) a GridNode is expected to have in order to be walkable
    public float MinElevation;
    
    /// The maximum amount two cells can differ in elevation to be considered on the same plane.
    public float StepHeight;

    /// Specifies the layer of the environment to raycast.
    public LayerMask LayerMask;
    
    public ModelSettings(float tileSize, float kernelStdDevTol, float maxSlope, float stepHeight, LayerMask layerMask)
      : this()
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