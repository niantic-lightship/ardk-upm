using System;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


namespace Niantic.Lightship.AR.ARFoundation.Meshing
{
    [RequireComponent(typeof(ARMeshManager))]
    public class LightshipMeshingExtension : MonoBehaviour
    {
      [SerializeField]
      [Tooltip("Target number of times per second to run the mesh update routine.")]
      private int _frameRate = 10;

      [Header("AR Fusion Parameters")]

      [SerializeField]
      [Tooltip("The far distance threshold (measured in m) for integrating depth samples into the 3D scene representation. New Mesh will not be generated beyond this distance from the camera.")]
      private float _maximumIntegrationDistance = 5.0f;

      [SerializeField]
      [Tooltip("The size (in m) of individual voxel elements in the scene representation. Setting this to higher values will reduce memory usage but reduce the precision of the surface.")]
      private float _voxelSize = 0.025f;

      [SerializeField]
      [Tooltip("Save memory and smooth latency by cleaning up already processed elements in the volumetric representation once they move outside the region where new mesh is currently being generated.")]
      private bool _enableDistanceBasedVolumetricCleanup = false;

      [Header("AR Meshing Parameters")]
      [SerializeField]
      [Tooltip("The size of the mesh blocks used for generating the mesh filter and mesh collider.")]
      private float _meshBlockSize = 1.4f;

      [SerializeField]
      [Tooltip("Mesh blocks that move beyond this distance from the camera are removed from the scene. A value of 0 indicates that mesh blocks will not be removed.")]
      private float _meshCullingDistance = 0.0f;

      [SerializeField]
      [Tooltip("Save memory by removing excess triangles from the mesh.")]
      private bool _enableMeshDecimation = true;

      private bool _isDirty;

      public int TargetFrameRate
      {
          get => _frameRate;
          set
          {
              if (value != _frameRate)
              {
                  _frameRate = value;
                  _isDirty = true;
              }
          }
      }

      public float MaximumIntegrationDistance
      {
          get => _maximumIntegrationDistance;
          set
          {
              const float tolerance = 0.0001f;
              if (Math.Abs(value - _maximumIntegrationDistance) > tolerance)
              {
                  _maximumIntegrationDistance = value;
                  _isDirty = true;
              }
          }
      }

      public float VoxelSize
      {
          get => _voxelSize;
          set
          {
              const float tolerance = 0.0001f;
              if (Math.Abs(value - _voxelSize) > tolerance)
              {
                  _voxelSize = value;
                  _isDirty = true;
              }
          }
      }

      public float MeshBlockSize
      {
          get => _meshBlockSize;
          set
          {
              const float tolerance = 0.0001f;
              if (Math.Abs(value - _meshBlockSize) > tolerance)
              {
                  _meshBlockSize = value;
                  _isDirty = true;
              }
          }
      }

      public float MeshCullingDistance
      {
          get => _meshCullingDistance;
          set
          {
              const float tolerance = 0.0001f;
              if (Math.Abs(value - _meshCullingDistance) > tolerance)
              {
                  _meshCullingDistance = value;
                  _isDirty = true;
              }
          }
      }

      public bool EnableMeshDecimation
      {
          get => _enableMeshDecimation;
          set
          {
              if (value != _enableMeshDecimation)
              {
                  _enableMeshDecimation = value;
                  _isDirty = true;
              }
          }
      }

      public bool EnableDistanceBasedVolumetricCleanup
      {
          get => _enableDistanceBasedVolumetricCleanup;
          set
          {
              if (value != _enableDistanceBasedVolumetricCleanup)
              {
                  _enableDistanceBasedVolumetricCleanup = value;
                  _isDirty = true;
              }
          }
      }

      private void Start()
      {
         Configure();
      }

      public void Configure()
      {
          _isDirty = false;

          LightshipMeshingProvider.Configure
          (
              _frameRate,
              _maximumIntegrationDistance,
              _voxelSize,
              _enableDistanceBasedVolumetricCleanup,
              _meshBlockSize,
              _meshCullingDistance,
              _enableMeshDecimation
          );
      }

      public void Update()
      {
          if (_isDirty)
          {
              Configure();
          }
      }
    }
}
