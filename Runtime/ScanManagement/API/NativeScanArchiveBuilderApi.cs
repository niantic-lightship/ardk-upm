// Copyright 2022-2024 Niantic.
using System;
using System.Runtime.InteropServices;
using System.Text;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Core;
using UnityEngine;

namespace Niantic.Lightship.AR.Scanning
{
    internal class NativeScanArchiveBuilderApi : IScanArchiveBuilderApi
    {
        public IntPtr Create(IntPtr unityContext, string scanPath, string scanId, string userDataStr, int maxFramesPerChunk)
        {
            if (string.IsNullOrEmpty(scanPath) || string.IsNullOrEmpty(scanId))
            {
                Log.Error("basePath and or scanId is null, can't create ScanArchiveBuilder");
                return IntPtr.Zero;
            }

            return Native.Lightship_ARDK_Unity_Scanning_Archive_Builder_Create(
                unityContext, scanPath, scanId, userDataStr, maxFramesPerChunk);
        }

        public void Release(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Log.Error("handle is nullptr, lightship is disabled");
                return;
            }

            Native.Lightship_ARDK_Unity_Scanning_Archive_Builder_Release(handle);
        }

        public bool HasMoreChunks(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Log.Error("handle is nullptr, lightship is disabled");
                return false;
            }

            return Native.Lightship_ARDK_Unity_Scanning_Archive_Builder_Has_More_Chunks(handle);
        }

        public bool IsValid(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Log.Error("handle is nullptr, lightship is disabled");
                return false;
            }

            return Native.Lightship_ARDK_Unity_Scanning_Archive_Builder_Is_Valid(handle);
        }

        public string GetNextChunk(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Log.Error("handle is nullptr, lightship is disabled");
                return "";
            }

            StringBuilder result = new StringBuilder(256);
            Native.Lightship_ARDK_Unity_Scanning_Archive_Builder_Get_Next_Chunk(handle, result, result.Capacity);
            return result.ToString();
        }

        public string GetNextChunkUuid(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Log.Error("handle is nullptr, lightship is disabled");
                return "";
            }

            StringBuilder result = new StringBuilder(40);
            Native.Lightship_ARDK_Unity_Scanning_Archive_Builder_Get_Next_Chunk_Uuid(handle, result, result.Capacity);
            return result.ToString();
        }

        public string GetScanTargetId(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Log.Error("handle is nullptr, lightship is disabled");
                return "";
            }

            StringBuilder result = new StringBuilder(256);
            Native.Lightship_ARDK_Unity_Scanning_Archive_Builder_Get_Scan_Poi_Id(handle, result, result.Capacity);
            return result.ToString();
        }

        public void CancelGetNextChunk(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Log.Error("handle is nullptr, lightship is disabled");
                return;
            }

            Native.Lightship_ARDK_Unity_Scanning_Archive_Builder_Cancel_Get_Next_Chunk(handle);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_Scanning_Archive_Builder_Create(
                IntPtr unityContext, string scanPath, string scanId, string userDataStr, int maxFramesPerChunk);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Scanning_Archive_Builder_Release(IntPtr handle);

            [DllImport(LightshipPlugin.Name)]
            [return:MarshalAs(UnmanagedType.I1)]
            public static extern bool Lightship_ARDK_Unity_Scanning_Archive_Builder_Has_More_Chunks(IntPtr handle);

            [DllImport(LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_Scanning_Archive_Builder_Is_Valid(IntPtr handle);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Scanning_Archive_Builder_Get_Next_Chunk(IntPtr handle,
                StringBuilder result, int stringMaxLength);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Scanning_Archive_Builder_Get_Next_Chunk_Uuid(IntPtr handle,
                StringBuilder result, int stringMaxLength);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Scanning_Archive_Builder_Get_Scan_Poi_Id(
                IntPtr handle, StringBuilder result, int stringMaxLength);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Scanning_Archive_Builder_Cancel_Get_Next_Chunk(
                IntPtr handle);
        }
    }
}
