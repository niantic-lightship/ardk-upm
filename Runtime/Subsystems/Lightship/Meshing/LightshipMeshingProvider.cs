using System;
using System.Runtime.InteropServices;

namespace Niantic.Lightship.AR
{
  public class LightshipMeshingProvider {

    public static IntPtr NativeProviderHandle;

    public LightshipMeshingProvider(IntPtr unityContext)
    {
        NativeProviderHandle = Lightship_ARDK_Unity_Meshing_Provider_Construct(unityContext);
    }

    public static bool Configure(
      int _frameRate,
      float _maximumIntegrationDistance,
      float _voxelSize,
      bool _enableDistanceBasedVolumetricCleanup,
      float _meshBlockSize,
      float _meshCullingDistance,
      bool _enableMeshDecimation)
    {
        return Lightship_ARDK_Unity_Meshing_Provider_Configure(
            _frameRate,
            _maximumIntegrationDistance,
            _voxelSize,
            _enableDistanceBasedVolumetricCleanup,
            _meshBlockSize,
            _meshCullingDistance,
            _enableMeshDecimation
        );
    }

    [DllImport(_LightshipPlugin.Name)]
    private static extern IntPtr Lightship_ARDK_Unity_Meshing_Provider_Construct(IntPtr unityContext);

    [DllImport(_LightshipPlugin.Name)]
    private static extern bool Lightship_ARDK_Unity_Meshing_Provider_Configure(
      int _frameRate,
      float _maximumIntegrationDistance,
      float _voxelSize,
      bool _enableDistanceBasedVolumetricCleanup,
      float _meshBlockSize,
      float _meshCullingDistance,
      bool _enableMeshDecimation
    );

  }
}
