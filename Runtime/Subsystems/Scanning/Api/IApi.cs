// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;

namespace Niantic.Lightship.AR.Subsystems.Scanning
{
    internal interface IApi
    {
        public IntPtr Construct(IntPtr unityContext);

        public void Destruct(IntPtr handle);

        public void Start(IntPtr handle);

        public void Stop(IntPtr handle);

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
            bool useEstimatedDepth,
            bool fullResolutionEnabled
        );

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
        );

        public void SaveCurrentScan(IntPtr handle);

        public void DiscardCurrentScan(IntPtr handle);

        public bool TryGetRecordingInfo(IntPtr handle, out string scanId, out RecordingStatus status);

        public IntPtr TryGetVoxelBuffer
        (
            IntPtr handle,
            out IntPtr positionBuffer,
            out IntPtr colorBuffer,
            out int pointCount
        );

        public void ComputeVoxels(IntPtr handle);

        public void ReleaseResource(IntPtr handle, IntPtr resourceHandle);
    }
}
