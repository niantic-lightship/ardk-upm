// Copyright 2022-2023 Niantic.
using System;
using Niantic.Lightship.AR.Utilities.Log;
using Niantic.Lightship.AR.Subsystems.Meshing;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;


namespace Niantic.Lightship.AR.Meshing
{
    /// <summary>
    /// This component allows configuration of the additional functionality available in
    /// Lightship's implementation of <see cref="XRMeshingSubsystem"/>.
    /// </summary>
    [RequireComponent(typeof(ARMeshManager))]
    [PublicAPI]
    public class LightshipMeshingExtension : MonoBehaviour
    {
      [SerializeField]
      [Tooltip("Target number of times per second to run the mesh update routine.")]
      [FormerlySerializedAs("_frameRate")]
      private int _targetFrameRate = 10;

      [Header("AR Fusion Parameters")]

      [SerializeField]
      [Tooltip("The far distance threshold (measured in m) for integrating depth samples into the 3D scene representation. New Mesh will not be generated beyond this distance from the camera.")]
      private float _maximumIntegrationDistance = 5.0f;

      [SerializeField]
      [Tooltip("The size (in m) of individual voxel elements in the scene representation. Setting this to higher values will reduce memory usage but reduce the precision of the surface.")]
      private float _voxelSize = 0.025f;

      [SerializeField]
      [Tooltip("Save memory and smooth latency by cleaning up already processed elements in the volumetric representation once they move outside the region where new mesh is currently being generated.")]
      private bool _enableDistanceBasedVolumetricCleanup = true;

      [Header("AR Meshing Parameters")]
      [SerializeField]
      [Tooltip("The size of the mesh blocks used for generating the mesh filter and mesh collider. This parameter is automatically rounded to the nearest multiple of the voxel size.")]
      private float _meshBlockSize = 1.4f;

      [SerializeField]
      [Tooltip("Mesh blocks that move beyond this distance from the camera are removed from the scene. A value of 0 indicates that mesh blocks will not be removed.")]
      private float _meshCullingDistance = 0.0f;

      [SerializeField]
      [Tooltip("Save memory by removing excess triangles from the mesh.")]
      private bool _enableMeshDecimation = true;

      private bool _isDirty;

      /// <summary>
      /// Get or set the frame rate that meshing will aim to run at.
      /// </summary>
      public int TargetFrameRate
      {
          get => _targetFrameRate;
          set
          {
              if (value != _targetFrameRate)
              {
                  _targetFrameRate = value;
                  _isDirty = true;
              }
          }
      }

      /// <summary>
      /// Get or set the maximum distance (in m) from the camera at which that the meshing system will integrate depth samples into the 3D scene representation.
      /// </summary>
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

      /// <summary>
      /// Get or set the size (in m) of individual voxel elements in the scene representation. Setting this to higher values will reduce memory usage but reduce the precision of the surface.
      /// </summary>
      public float VoxelSize
      {
          get => _voxelSize;
          set
          {
              const float tolerance = 0.0001f;
              if (Math.Abs(value - _voxelSize) > tolerance)
              {
                  _voxelSize = value;
                  _meshBlockSize = (float)Math.Round(_meshBlockSize / value) * value;
                  Log.Info("Mesh block size rounded to " + _meshBlockSize + " m as the voxel block size was changed.");
                  _isDirty = true;
              }
          }
      }

      /// <summary>
      /// Get or set the size (in m) of the Mesh Blocks used for generating the Mesh Filter and Mesh Collider. This value will be automatically rounded to be a multiple of the voxel size.
      /// </summary>
      public float MeshBlockSize
      {
          get => _meshBlockSize;
          set
          {
              const float tolerance = 0.0001f;
              if (Math.Abs(value - _meshBlockSize) > tolerance)
              {
                  _meshBlockSize = (float)Math.Round(value / _voxelSize) * _voxelSize;
                  Log.Info("Mesh block size rounded to " + _meshBlockSize + " m");
                  _isDirty = true;
              }
          }
      }

      /// <summary>
      /// Get or set the distance (in m) from the camera at which Mesh Blocks will be removed from the scene. A value of 0 indicates that Mesh Blocks will not be removed.
      /// </summary>
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

      /// <summary>
      /// Get or set whether excess triangles will be removed from the mesh.
      /// </summary>
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

      /// <summary>
      /// Get or set whether the volumetric representation will be cleaned up once it moves outside the region where new mesh is currently being generated. This saves memory and smooths latency.
      /// </summary>
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

      private void OnValidate()
      {
        _meshBlockSize = (float)Math.Round(_meshBlockSize / _voxelSize) * _voxelSize;
      }

      private void Start()
      {
          // If the subsystem is not loaded yet due to manual XR loading, we'll try configuring again in Update.
          Configure();
      }

      public void Configure()
      {
          // Only call into native code if an XRMeshSubsystem is loaded.
          // We should not cache the value of the validation because in the case of manual control of XR loaders,
          // this can also be called during Update after the loader has been shut down and lead to an assertion.
          if (!ValidateSubsystem())
              return;

          _isDirty = false;

          LightshipMeshingProvider.Configure
          (
              _targetFrameRate,
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

      /// <summary>
      /// Returns true if an XR loader is initialized and an XRMeshSubsystem is present.
      /// </summary>
      private bool ValidateSubsystem()
      {
          // If automatic XR loading is enabled, then subsystems will be available before the Awake call.
          // However, if XR loading is done manually, then this component needs to check if initialization is complete.
          var xrManager = XRGeneralSettings.Instance.Manager;
          if (!xrManager.isInitializationComplete)
              return false;

          var meshSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XRMeshSubsystem>();

          if (meshSubsystem == null)
          {
              Log.Warning
              (
                  "Destroying LightshipMeshingExtension component because " +
                  $"no active {typeof(XRMeshSubsystem).FullName} is available. " +
                  "Please ensure that a valid loader configuration exists in the XR project settings " +
                  "and that meshing is enabled."
              );

              Destroy(this);
              return false;
          }

          return true;
      }
    }
}
