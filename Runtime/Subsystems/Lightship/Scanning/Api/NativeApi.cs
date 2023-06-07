// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Niantic.Lightship.AR.ScanningSubsystem
{
    public class NativeApi : IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            Debug.Log("_NativeApi Lightship_ARDK_Unity_Scanner_Create Construct");
            var ptr = Native.Create(unityContext);
            Debug.Log("_NativeApi Construct done");
            return ptr;
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
            bool voxelVisualizationEnabled)
        {
            // No need to implement the Configure function for the mocking.
        }

        public bool TryGetRaycastBuffer(IntPtr handle, out IntPtr memoryBuffer, out int size, out int width,
            out int height)
        {
            size = 0;
            width = 0;
            height = 0;
            Native.TryGetRaycastBuffer(handle, out memoryBuffer, out size, out width, out height);
            return true;
        }

        public void ReleaseResource(IntPtr handle, IntPtr resource_handle)
        {
            Native.ReleaseResource(handle, resource_handle);
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

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Raycaster_Get_Raycast_Buffer")]
            public static extern void TryGetRaycastBuffer(IntPtr handle,
                out IntPtr memoryBuffer, out int size, out int width, out int height);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Scanner_ReleaseResource")]
            public static extern void ReleaseResource(IntPtr handle, IntPtr resource_handle);
        }
    }
}
