// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Niantic.Lightship.AR.PersistentAnchors;

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
            if (!CheckNativeHandle())
            {
                return;
            }
            Lightship_ARDK_Unity_Mapping_Release(_nativeProviderHandle);
            _nativeProviderHandle = IntPtr.Zero;
        }

        public void Start()
        {
            if (!CheckNativeHandle())
            {
                return;
            }
            Lightship_ARDK_Unity_Mapping_Start(_nativeProviderHandle);
        }

        public void Stop()
        {
            if (!CheckNativeHandle())
            {
                return;
            }
            Lightship_ARDK_Unity_Mapping_Stop(_nativeProviderHandle);
        }

        /// <summary>
        /// Defined in ardk_mapping_configuration.h file.
        /// Note: We need this because passing configurations as arguments breaks down
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MappingConfigurationCStruct
        {
            public float splitterMaxDistanceMeters;

            public float splitterMaxDurationSeconds;
        }

        public void Configure
        (
            float splitterMaxDistanceMeters,
            float splitterMaxDurationSeconds
        )
        {
            if (!CheckNativeHandle())
            {
                return;
            }

            var configurationCStruct = new MappingConfigurationCStruct();
            configurationCStruct.splitterMaxDistanceMeters = splitterMaxDistanceMeters;
            configurationCStruct.splitterMaxDurationSeconds = splitterMaxDurationSeconds;
            Lightship_ARDK_Unity_Mapping_Configure(_nativeProviderHandle, configurationCStruct);
        }

        public void StartMapping()
        {
            if (!CheckNativeHandle())
            {
                return;
            }
            Lightship_ARDK_Unity_Mapping_StartMapping(_nativeProviderHandle);
        }

        public void StopMapping()
        {
            if (!CheckNativeHandle())
            {
                return;
            }
            Lightship_ARDK_Unity_Mapping_StopMapping(_nativeProviderHandle);
        }

        public bool GetDeviceMaps(out XRDeviceMap[] maps)
        {
            if (!CheckNativeHandle())
            {
                maps = default;
                return false;
            }

            var handle = Lightship_ARDK_Unity_Mapping_AcquireMaps(_nativeProviderHandle, out var mapList, out var listCount);
            if (!handle.IsValidHandle())
            {
                Log.Warning("Invalid handle returned when attempt to acquire map data");
                maps = default;
                return false;
            }

            if (listCount == 0)
            {
                Lightship_ARDK_Unity_Mapping_ReleaseResource(handle);
                maps = default;
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
            if (!CheckNativeHandle())
            {
                blobs = default;
                return false;
            }

            var handle = Lightship_ARDK_Unity_Mapping_AcquireGraphs(_nativeProviderHandle, out var blobList, out var listCount);

            if (listCount == 0)
            {
                blobs = default;

                if (handle.IsValidHandle())
                {
                    Lightship_ARDK_Unity_Mapping_ReleaseResource(handle);
                }
                else
                {
                    Log.Warning("Invalid graph handle");
                }
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

        public void CreateAnchorPayloadFromDeviceMap(XRDeviceMap map, Matrix4x4 pose, out byte[] anchorPayload)
        {
            var mapNodeId = map.GetNodeId();
            var poseArray = MatrixConversionHelper.Matrix4x4ToInternalArray(pose.FromUnityToArdk());
            var anchorHandle = Lightship_ARDK_Unity_MapDataShare_CreateAnchor(ref mapNodeId, poseArray);

            Lightship_ARDK_Unity_MapDataShare_ExtractAnchor(anchorHandle, out IntPtr dataPtr, out UInt32 dataSize);

            anchorPayload = new byte[dataSize];
            Marshal.Copy(dataPtr, anchorPayload, 0, (int)dataSize);
            Lightship_ARDK_Unity_MapDataShare_ReleaseAnchor(anchorHandle);
        }

        private XRDeviceMap GetDeviceMap(IntPtr handle)
        {
            // handle validity has to be checked in the caller side

            // Get data bytes
            var success = Lightship_ARDK_Unity_Mapping_ExtractMap
                (handle, out var nodeId, out var dataPtr, out var dataSize);
            if (!success || dataPtr == IntPtr.Zero)
            {
                Debug.Log("GetDeviceMap(): Couldn't extract device map!");
                return default;
            }

            var dataBytes = new byte[dataSize];
            Marshal.Copy(dataPtr, dataBytes, 0, (int)dataSize);

            return new XRDeviceMap(nodeId, dataBytes);
        }

        private XRDeviceMapGraph GetDeviceGraphBlob(IntPtr handle)
        {
            // handle validity has to be checked in the caller side

            // Get data bytes
            bool success = Lightship_ARDK_Unity_Mapping_ExtractGraph(handle, out var dataPtr, out var dataSize);
            if (!success || dataPtr == IntPtr.Zero)
            {
                Log.Error("GetDeviceMap(): Couldn't extract device graph blob!");
                return default;
            }

            var dataBytes = new byte[dataSize];
            Marshal.Copy(dataPtr, dataBytes, 0, (int)dataSize);

            return new XRDeviceMapGraph(dataBytes);
        }

        private bool CheckNativeHandle()
        {
            if (!_nativeProviderHandle.IsValidHandle())
            {
                Debug.LogWarning("No valid Mapping module handle");
                return false;
            }
            return true;
        }

        // Native function declarations

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
            out TrackableId node_id_out,
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
        private static extern void Lightship_ARDK_Unity_Mapping_Configure
        (
            IntPtr feature_handle,
            MappingConfigurationCStruct config
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Mapping_RestoreMap
            (IntPtr data_ptr, UInt32 data_size);

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_Mapping_RestoreGraph
        (
            IntPtr data_ptr,
            UInt32 data_size
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_MapDataShare_CreateAnchor
        (
            ref TrackableId mapNodeId,
            float[] pose
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapDataShare_ExtractAnchor
        (
            IntPtr anchorHandle,
            out IntPtr anchorPayloadPtr,
            out UInt32 anchorPayloadSize
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapDataShare_ReleaseAnchor
        (
            IntPtr anchorHandle
        );

    }
}
