// Copyright 2022-2024 Niantic.

namespace Niantic.Lightship.AR.NavigationMesh
{
  public enum PathFindingBehaviour
  {
    /// The calculated route will navigate to the destination
    /// or the closest point to it within a single surface.
    SingleSurface = 0,

    /// The calculated route can contain jumps to other surfaces.
    /// @note
    ///   The agent will only consider immediate nodes during the search.
    ///   This method is faster, but does not always find an existing path.
    InterSurfacePreferPerformance = 1,

    /// The calculated route can contain jumps to other surfaces.
    /// @note This method is slower, but it finds a path if it exists.
    InterSurfacePreferResults = 2
  }
}
