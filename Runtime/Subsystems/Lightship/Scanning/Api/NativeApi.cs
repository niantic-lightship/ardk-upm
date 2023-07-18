// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Niantic.Lightship.AR.ScanningSubsystem
{
    public class NativeApi : IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            return  Native.Create(unityContext);
        }

        public void Destruct(IntPtr handle)
        {
            Native.Release(handle);
        }

        public void Start(IntPtr handle)
        {
            Native.Start(handle);
        }

        public void Stop(IntPtr handle)
        {
            Native.Stop(handle);
        }

        public void Configure(IntPtr handle, int framerate, bool raycastVisualizationEnabled,
            int raycastVisualizationWidth, int raycastVisualizationHeight,
            bool voxelVisualizationEnabled, string scanBasePath)
        {
            Native.Configure(handle, framerate, raycastVisualizationEnabled, raycastVisualizationWidth,
                raycastVisualizationHeight, scanBasePath, scanBasePath.Length);
        }

        public IntPtr TryGetRaycastBuffer(IntPtr handle, out IntPtr colorBuffer, out IntPtr normalBuffer, out IntPtr positionBuffer,
            out int colorSize, out int normalSize, out int positionSize, out int width, out int height)
        {
            colorSize = 0;
            width = 0;
            height = 0;
            normalSize = 0;
            positionSize = 0;
            return Native.TryGetRaycastBuffer(handle, out colorBuffer, out normalBuffer, out positionBuffer,
                out colorSize, out normalSize, out positionSize, out width, out height);
        }

        public void SaveCurrentScan(IntPtr handle)
        {
            Native.SaveCurrentScan(handle);
        }

        public void DiscardCurrentScan(IntPtr handle)
        {
            Native.DiscardCurrentScan(handle);
        }

        private StringBuilder _scanIdBuffer = new StringBuilder(128);

        public bool TryGetRecordingInfo(IntPtr handle, out string scanId, out RecordingStatus status)
        {
            _scanIdBuffer.Clear();
            if (handle == IntPtr.Zero ||
                !Native.GetRecordingInfo(handle, _scanIdBuffer, _scanIdBuffer.Capacity, out status))
            {
                scanId = null;
                status = RecordingStatus.Unknown;
                return false;
            }
            scanId = _scanIdBuffer.ToString();
            return true;
        }

        public IntPtr TryGetVoxelBuffer(IntPtr handle, out IntPtr positionBuffer, out IntPtr colorBuffer,
            out int pointCount)
        {
            pointCount = 0;
            return Native.TryGetVoxelBuffer(handle, out positionBuffer, out colorBuffer, out pointCount);
        }

        public void ComputeVoxels(IntPtr handle)
        {
            Native.ComputeVoxels(handle);
        }

        public void ReleaseResource(IntPtr handle, IntPtr resourceHandle)
        {
            Native.ReleaseResource(handle, resourceHandle);
        }

        private static class Native
        {
            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Create")]
            public static extern IntPtr Create(IntPtr unityContext);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Release")]
            public static extern void Release(IntPtr handle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Start")]
            public static extern void Start(IntPtr handle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Stop")]
            public static extern void Stop(IntPtr handle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Get_Raycast_Buffer")]
            public static extern IntPtr TryGetRaycastBuffer(IntPtr handle,
                out IntPtr colorBuffer, out IntPtr normalBuffer, out IntPtr positionBuffer,
                out int colorSize, out int normalSize, out int positionSize, out int width, out int height);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Save_Current_Scan")]
            public static extern void SaveCurrentScan(IntPtr handle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Discard_Current_Scan")]
            public static extern void DiscardCurrentScan(IntPtr handle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Get_Recording_Info")]
            public static extern bool GetRecordingInfo(IntPtr handle, StringBuilder scanId, int maxScanIdLen, out RecordingStatus status);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Release_Resource")]
            public static extern void ReleaseResource(IntPtr handle, IntPtr resourceHandle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Compute_Voxels")]
            public static extern void ComputeVoxels(IntPtr handle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Get_Voxel_Buffer")]
            public static extern IntPtr TryGetVoxelBuffer(IntPtr handle,
                out IntPtr positionBuffer, out IntPtr colorBuffer, out int pointCount);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_Configure")]
            public static extern void Configure(IntPtr handle, int framerate,
                bool enableRaycastVisualization,
                int raycastWidth,
                int raycastHeight,
                string path,
                int pathLength);
        }
    }
}
