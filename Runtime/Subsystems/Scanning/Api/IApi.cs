// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;

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
            ScannerConfigurationCStruct config
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

    /// <summary>
    /// C struct for C# to send frame data to C++. Defined in ardk_scanning_configuration.h file.
    /// Note: It is not that great as we have both XRScanningConfiguration and this ScannerConfigurationCStruct.
    /// The reason why we don't move ScannerConfigurationCStruct into XRScanningConfiguratiis for the benefits
    /// of decoupling internal and public code, and avoid easily breaking existing public contracts.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ScannerConfigurationCStruct
    {
        // FPS for scanner's recording.
        public int Framerate;

        // Flag to indicate if raycast visualization should be enabled during scanning.
        [MarshalAs(UnmanagedType.U1)]
        public bool EnableRaycastVisualization;

        // Width of the resolution used by the raycast visualization.
        public int RaycastWidth;

        // Height of the resolution used by the raycast visualization.
        public int RaycastHeight;

        // Flag to indicate if voxel visualization should be enabled during scanning.
        [MarshalAs(UnmanagedType.U1)]
        public bool EnableVoxelVisualization;

        // Base path string.
        public string BasePath;

        // Length of the base path string.
        public int BasePathLen;

        // Scan target ID string.
        public string ScanTargetId;

        // Length of the scan target ID String.
        public int ScanTargetIdLen;

        // Flag to indicate if multidepth will be used during scanning.
        [MarshalAs(UnmanagedType.U1)]
        public bool UseMultidepth;

        // Flag to indicate if full resolution JPEG image will be used during scanning.
        [MarshalAs(UnmanagedType.U1)]
        public bool EnableFullResolution;
    }
}
