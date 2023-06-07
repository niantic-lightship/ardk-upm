// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.Lightship.AR.Subsystems;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Niantic.Lightship.AR.ScanningSubsystem
{
    public class MockApi : IApi
    {
        private IntPtr _handle;
        private XRScanningConfiguration _currentConfiguration;
        bool _started = false;

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
            _started = true;
        }

        public void Stop(IntPtr handle)
        {
            _started = false;
        }

        public void Configure(IntPtr handle, int framerate, bool raycastVisualizationEnabled,
            int raycastVisualizationWidth, int raycastVisualizationHeight,
            bool voxelVisualizationEnabled)
        {
            _currentConfiguration.Framerate = framerate;
            _currentConfiguration.RaycasterVisualizationEnabled = raycastVisualizationEnabled;
            _currentConfiguration.RaycasterVisualizationResolution =
                new Vector2(raycastVisualizationWidth, raycastVisualizationHeight);
            _currentConfiguration.VoxelVisualizationEnabled = voxelVisualizationEnabled;
        }

        public bool TryGetRaycastBuffer(IntPtr handle, out IntPtr memoryBuffer, out int size, out int width,
            out int height)
        {
            size = 0;
            width = 0;
            height = 0;
            throw new NotImplementedException();
            return false;
        }

        public void ReleaseResource(IntPtr handle, IntPtr resource_handle)
        {
            throw new NotImplementedException();
        }
    }
}
