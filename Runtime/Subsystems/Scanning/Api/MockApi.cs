// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Niantic.Lightship.AR.Subsystems.Scanning
{
    internal class MockApi : IApi
    {
        private IntPtr _handle;
        private XRScanningConfiguration _currentConfiguration;

        public IntPtr Construct(IntPtr unityContext)
        {
            _handle = new IntPtr((int)(Random.value * int.MaxValue));
            return _handle;
        }

        public void Destruct(IntPtr handle)
        {
            if (handle == _handle)
            {
                _handle = IntPtr.Zero;
            }
        }

        public void Start(IntPtr handle)
        {
        }

        public void Stop(IntPtr handle)
        {
        }

        public void Configure
        (
            IntPtr handle,
            ScannerConfigurationCStruct config
        )
        {
            _currentConfiguration.Framerate = config.Framerate;
            _currentConfiguration.RaycasterVisualizationEnabled = config.EnableRaycastVisualization;
            _currentConfiguration.RaycasterVisualizationResolution =
                new Vector2(config.RaycastWidth, config.RaycastHeight);
            _currentConfiguration.VoxelVisualizationEnabled = config.EnableVoxelVisualization;
            _currentConfiguration.ScanBasePath = config.BasePath;
            _currentConfiguration.ScanTargetId = config.ScanTargetId;
            _currentConfiguration.UseEstimatedDepth = config.UseMultidepth;
            _currentConfiguration.FullResolutionEnabled = config.EnableFullResolution;
        }

        public IntPtr TryGetRaycastBuffer
        (
            IntPtr handle,
            out IntPtr colorBuffer,
            out IntPtr normalBuffer,
            out IntPtr positionBuffer,
            out int colorSize,
            out int normalSize,
            out int positionSize,
            out int width,
            out int height
        )
        {
            throw new NotImplementedException();
        }

        public void SaveCurrentScan(IntPtr handle)
        {
            throw new NotImplementedException();
        }

        public void DiscardCurrentScan(IntPtr handle)
        {
            throw new NotImplementedException();
        }

        public bool TryGetRecordingInfo(IntPtr handle, out string scanId, out RecordingStatus status)
        {
            throw new NotImplementedException();
        }

        public IntPtr TryGetVoxelBuffer
        (
            IntPtr handle,
            out IntPtr positionBuffer,
            out IntPtr colorBuffer,
            out int pointCount
        )
        {
            throw new NotImplementedException();
        }

        public void ComputeVoxels(IntPtr handle)
        {
            throw new NotImplementedException();
        }

        public void ReleaseResource(IntPtr handle, IntPtr resourceHandle)
        {
            throw new NotImplementedException();
        }
    }
}
