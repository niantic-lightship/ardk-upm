using System;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


namespace Niantic.Lightship.AR.ARFoundation
{
    [RequireComponent(typeof(ARMeshManager))]
    public class NianticLightshipMeshingExtensionSettings : MonoBehaviour
    {

      [SerializeField]
      [Tooltip("Target number of times per second to run the mesh update routine.")]
      public int _frameRate = 10;

      [Header("AR Fusion Parameters")]

      [SerializeField]
      [Tooltip("The far distance threshold (measured in m) for integrating depth samples into the 3D scene representation. New Mesh will not be generated beyond this distance from the camera.")]
      public float _maximumIntegrationDistance = 5.0f;

      [SerializeField]
      [Tooltip("The size (in m) of individual voxel elements in the scene representation. Setting this to higher values will reduce memory usage but reduce the precision of the surface.")]
      public float _voxelSize = 0.025f;

      [SerializeField]
      [Tooltip("Save memory and smooth latency by cleaning up already processed elements in the volumetric representation once they move outside the region where new mesh is currently being generated.")]
      public bool _enableDistanceBasedVolumetricCleanup = false;

      [Header("AR Meshing Parameters")]
      [SerializeField]
      [Tooltip("The size of the mesh blocks used for generating the mesh filter and mesh collider.")]
      public float _meshBlockSize = 1.4f;

      [SerializeField]
      [Tooltip("Mesh blocks that move beyond this distance from the camera are removed from the scene. A value of 0 indicates that mesh blocks will not be removed.")]
      public float _meshCullingDistance = 0.0f;

      [SerializeField]
      [Tooltip("Save memory by removing excess triangles from the mesh.")]
      public bool _enableMeshDecimation = true;

      private void Start()
      {
         Configure();
      }

      public void Configure()
      {
          LightshipMeshingProvider.Configure(
              _frameRate,
              _maximumIntegrationDistance,
              _voxelSize,
              _enableDistanceBasedVolumetricCleanup,
              _meshBlockSize,
              _meshCullingDistance,
              _enableMeshDecimation
          );
      }

    }
}
