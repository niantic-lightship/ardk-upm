// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;

namespace Niantic.Lightship.AR.ScanningSubsystem
{
    public interface IApi
    {
        public IntPtr Construct(IntPtr unityContext);

        public void Destruct(IntPtr handle);

        public void Start(IntPtr handle);

        public void Stop(IntPtr handle);

        public void Configure(IntPtr handle, int framerate, bool raycastVisualizationEnabled,
            int raycastVisualizationWidth, int raycastVisualizationHeight,
            bool voxelVisualizationEnabled);

        public bool TryGetRaycastBuffer(IntPtr handle, out IntPtr memoryBuffer, out int size, out int width,
            out int height);

        public void ReleaseResource(IntPtr handle, IntPtr resource_handle);
    }
}
