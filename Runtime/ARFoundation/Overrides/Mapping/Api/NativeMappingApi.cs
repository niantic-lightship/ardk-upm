// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Mapping
{
    internal class NativeMappingApi :
        IMappingApi
    {
        private IntPtr _nativeProviderHandle;

        public IntPtr Create(IntPtr unityContext)
        {
            _nativeProviderHandle = Lightship_ARDK_Unity_Mapping_Create(unityContext);
            return _nativeProviderHandle;
        }

        public void Dispose()
        {
            Lightship_ARDK_Unity_Mapping_Release(_nativeProviderHandle);
        }

        public void Start()
        {
            Lightship_ARDK_Unity_Mapping_Start(_nativeProviderHandle);
        }

        public void Stop()
        {
            Lightship_ARDK_Unity_Mapping_Stop(_nativeProviderHandle);
        }

        public void Configure()
        {
            Lightship_ARDK_Unity_Mapping_Configure(_nativeProviderHandle);
        }

        public void StartMapping()
        {
            Lightship_ARDK_Unity_Mapping_StartMapping(_nativeProviderHandle);
        }

        public void StopMapping()
        {
            Lightship_ARDK_Unity_Mapping_StopMapping(_nativeProviderHandle);
        }

        public bool GetDeviceMaps(out XRDeviceMap[] maps)
        {
            if (!_nativeProviderHandle.IsValidHandle())
            {
                maps = Array.Empty<XRDeviceMap>();
                return false;
            }

            var handle = Lightship_ARDK_Unity_Mapping_AcquireMaps(_nativeProviderHandle, out var mapList, out var listCount);
            if (!handle.IsValidHandle())
            {
                Log.Warning("Invalid handle returned when attempt to acquire map data");
                maps = Array.Empty<XRDeviceMap>();
                return false;
            }

            if (listCount == 0)
            {
                maps = Array.Empty<XRDeviceMap>();
                Lightship_ARDK_Unity_Mapping_ReleaseResource(handle);
                return false;
            }

            try
            {
                maps = new XRDeviceMap[listCount];
                NativeArray<IntPtr> mapPtrList;
                unsafe
                {
                    mapPtrList = NativeCopyUtility.PtrToNativeArrayWithDefault
                    (
                        IntPtr.Zero,
                        mapList.ToPointer(),
                        sizeof(IntPtr),
                        (int)listCount,
                        Allocator.Temp
                    );
                }

                for (int i = 0; i < listCount; i++)
                {
                    maps[i] = GetDeviceMap(mapPtrList[i]);
                }
            }
            finally
            {
                if (!handle.IsValidHandle())
                {
                    Log.Error("Tried to release map handle with invalid pointer.");
                }

                Lightship_ARDK_Unity_Mapping_ReleaseResource(handle);
            }


            return true;
        }

        public bool GetDeviceGraphBlobs(out XRDeviceMapGraph[] blobs)
        {
            if (!_nativeProviderHandle.IsValidHandle())
            {
                blobs = Array.Empty<XRDeviceMapGraph>();
                return false;
            }

            var handle = Lightship_ARDK_Unity_Mapping_AcquireGraphs(_nativeProviderHandle, out var blobList, out var listCount);

            if (listCount == 0)
            {
                blobs = Array.Empty<XRDeviceMapGraph>();

                if (!handle.IsValidHandle())
                {
                    Log.Error("Tried to release network status handle with invalid pointer.");
                }

                Lightship_ARDK_Unity_Mapping_ReleaseResource(handle);
                return false;
            }

            try
            {
                blobs = new XRDeviceMapGraph[listCount];
                NativeArray<IntPtr> blobPtrList;
                unsafe
                {
                    blobPtrList = NativeCopyUtility.PtrToNativeArrayWithDefault
                    (
                        IntPtr.Zero,
                        blobList.ToPointer(),
                        sizeof(IntPtr),
                        (int)listCount,
                        Allocator.Temp
                    );
                }

                for (int i = 0; i < listCount; i++)
                {
                    blobs[i] = GetDeviceGraphBlob(blobPtrList[i]);
                }
            }
            finally
            {
                if (!handle.IsValidHandle())
                {
                    Log.Error("Tried to release map handle with invalid pointer.");
                }

                Lightship_ARDK_Unity_Mapping_ReleaseResource(handle);
            }

            return true;
        }

        private XRDeviceMap GetDeviceMap(IntPtr handle)
        {
            if (!handle.IsValidHandle())
            {
                Log.Error("Tried to extract device map with invalid status pointer.");
            }

            // Get data bytes
            IntPtr dataPtr = IntPtr.Zero;
            UInt32 dataSize = 0;
            bool success = Lightship_ARDK_Unity_Mapping_ExtractMap(handle, out dataPtr, out dataSize);
            if (!success)
            {
                Debug.Log("GetDeviceMap(): Couldn't extract device map!");
                return default;
            }
            byte[] dataBytes = new byte[dataSize];
            Marshal.Copy(dataPtr, dataBytes, 0, (int)dataSize);

            return new XRDeviceMap(dataBytes);
        }

        private XRDeviceMapGraph GetDeviceGraphBlob(IntPtr handle)
        {
            if (!handle.IsValidHandle())
            {
                Log.Error("Tried to extract device graph blob with invalid status pointer.");
            }


            // Get data bytes
            IntPtr dataPtr = IntPtr.Zero;
            UInt32 dataSize = 0;
            bool success = Lightship_ARDK_Unity_Mapping_ExtractGraph(handle, out dataPtr, out dataSize);
            if (!success)
            {
                Log.Error("GetDeviceMap(): Couldn't extract device graph blob!");
                return default;
            }
            byte[] dataBytes = new byte[dataSize];
            Marshal.Copy(dataPtr, dataBytes, 0, (int)dataSize);

            return new XRDeviceMapGraph(dataBytes);
        }

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Mapping_Create(IntPtr unity_context);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_Mapping_Release(IntPtr feature_handle);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_Mapping_Start(IntPtr feature_handle);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_Mapping_Stop(IntPtr feature_handle);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_Mapping_ReleaseResource(IntPtr resourceHandle);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_Mapping_StartMapping(IntPtr feature_handle);


        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_Mapping_StopMapping(IntPtr feature_handle);

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Mapping_AcquireMaps
        (
            IntPtr feature_handle,
            out IntPtr elements,
            out UInt32 count
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Mapping_AcquireGraphs
        (
            IntPtr handle,
            out IntPtr elements,
            out UInt32 count
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern bool Lightship_ARDK_Unity_Mapping_ExtractMap
        (
            IntPtr map_handle,
            out IntPtr map_data_out,
            out UInt32 map_data_size_out
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern bool Lightship_ARDK_Unity_Mapping_ExtractGraph
        (
            IntPtr blob_handle,
            out IntPtr blob_data_out,
            out UInt32 blob_data_size_out
        );

        // Unused
        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_Mapping_Configure(IntPtr feature_handle);

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Mapping_RestoreMap
            (IntPtr data_ptr, UInt32 data_size);

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Mapping_RestoreGraph
        (
            IntPtr data_ptr,
            UInt32 data_size
        );
    }
}
