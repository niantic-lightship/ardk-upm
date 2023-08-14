// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.Lightship.AR.Subsystems;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Niantic.Lightship.AR.ScanningSubsystem
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
            int framerate,
            bool raycastVisualizationEnabled,
            int raycastVisualizationWidth,
            int raycastVisualizationHeight,
            bool voxelVisualizationEnabled,
            string scanBasePath,
            string scanTargetId,
            bool fullResolutionEnabled,
            bool useEstimatedDepth
        )
        {
            _currentConfiguration.Framerate = framerate;
            _currentConfiguration.RaycasterVisualizationEnabled = raycastVisualizationEnabled;
            _currentConfiguration.RaycasterVisualizationResolution =
                new Vector2(raycastVisualizationWidth, raycastVisualizationHeight);
            _currentConfiguration.VoxelVisualizationEnabled = voxelVisualizationEnabled;
            _currentConfiguration.ScanBasePath = scanBasePath;
            _currentConfiguration.ScanTargetId = scanTargetId;
            _currentConfiguration.FullResolutionEnabled = fullResolutionEnabled;
            _currentConfiguration.UseEstimatedDepth = useEstimatedDepth;
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
