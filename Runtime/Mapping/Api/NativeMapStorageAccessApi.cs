// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Niantic.Lightship.AR.PersistentAnchors;

namespace Niantic.Lightship.AR.MapStorageAccess
{
    internal class NativeMapStorageAccessApi :
        IMapStorageAccessApi
    {
        private IntPtr _nativeHandle;

        public IntPtr Create(IntPtr unityContext)
        {
            _nativeHandle = Lightship_ARDK_Unity_MapStorageAccess_Create(unityContext);
            return _nativeHandle;
        }

        public void Dispose()
        {
            if (!CheckNativeHandle())
            {
                return;
            }

            Lightship_ARDK_Unity_MapStorageAccess_Release(_nativeHandle);
            _nativeHandle = IntPtr.Zero;
        }

        public void Start()
        {
            if (!CheckNativeHandle())
            {
                return;
            }

            Lightship_ARDK_Unity_MapStorageAccess_Start(_nativeHandle);
        }

        public void Stop()
        {
            if (!CheckNativeHandle())
            {
                return;
            }

            Lightship_ARDK_Unity_MapStorageAccess_Stop(_nativeHandle);
        }

        /// <summary>
        /// Defined in ardk_map_storage_access_configuration.h file.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MapStorageAccessConfigurationCStruct
        {
            public UInt64 outputEdgeType;
        }

        public void Configure(OutputEdgeType edgeType)
        {
            if (!CheckNativeHandle())
            {
                return;
            }

            var configurationCStruct = new MapStorageAccessConfigurationCStruct();
            configurationCStruct.outputEdgeType = (UInt64)edgeType;
            Lightship_ARDK_Unity_MapStorageAccess_Configure(_nativeHandle, configurationCStruct);
        }


        public void AddMapNode(byte[] dataBytes)
        {
            if (!CheckNativeHandle())
            {
                return;
            }

            IntPtr dataPtr = IntPtr.Zero;
            int dataSize = dataBytes.Length;
            unsafe
            {
                fixed (byte* bytePtr = dataBytes)
                {
                    dataPtr = (IntPtr)bytePtr;
                    Lightship_ARDK_Unity_MapStorageAccess_AddMap(_nativeHandle, dataPtr, dataSize);
                }
            }

        }

        public void AddSubGraph(byte[] dataBytes)
        {
            if (!CheckNativeHandle())
            {
                return;
            }

            IntPtr dataPtr = IntPtr.Zero;
            int dataSize = dataBytes.Length;
            unsafe
            {
                fixed (byte* bytePtr = dataBytes)
                {
                    dataPtr = (IntPtr)bytePtr;
                    Lightship_ARDK_Unity_MapStorageAccess_AddGraph(_nativeHandle, dataPtr, dataSize);
                }
            }

        }

        public void Clear()
        {
            if (!CheckNativeHandle())
            {
                return;
            }

            Lightship_ARDK_Unity_MapStorageAccess_Clear(_nativeHandle);
        }

        public bool GetMapNodes(out MapNode[] maps)
        {
            if (!CheckNativeHandle())
            {
                maps = default;
                return false;
            }

            var handle =
                Lightship_ARDK_Unity_MapStorageAccess_AcquireMaps(_nativeHandle, out var mapList, out var listCount);
            if (!handle.IsValidHandle())
            {
                Log.Warning("Invalid handle returned when attempt to acquire map data");
                maps = default;
                return false;
            }

            if (listCount == 0)
            {
                Lightship_ARDK_Unity_MapStorageAccess_ReleaseResource(handle);
                maps = default;
                return false;
            }

            try
            {
                maps = new MapNode[listCount];
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
                    maps[i] = GetMapNode(mapPtrList[i]);
                }
            }
            finally
            {
                if (!handle.IsValidHandle())
                {
                    Log.Error("Tried to release map handle with invalid pointer.");
                }

                Lightship_ARDK_Unity_MapStorageAccess_ReleaseResource(handle);
            }


            return true;
        }

        public bool GetSubGraphs(out MapSubGraph[] blobs)
        {
            if (!CheckNativeHandle())
            {
                blobs = default;
                return false;
            }

            var handle =
                Lightship_ARDK_Unity_MapStorageAccess_AcquireGraphs(_nativeHandle, out var blobList, out var listCount);

            if (listCount == 0)
            {
                blobs = default;

                if (handle.IsValidHandle())
                {
                    Lightship_ARDK_Unity_MapStorageAccess_ReleaseResource(handle);
                }
                else
                {
                    Log.Warning("Invalid graph handle");
                }

                return false;
            }

            try
            {
                blobs = new MapSubGraph[listCount];
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
                    blobs[i] = GetMapSubGraph(blobPtrList[i]);
                }
            }
            finally
            {
                if (!handle.IsValidHandle())
                {
                    Log.Error("Tried to release map handle with invalid pointer.");
                }

                Lightship_ARDK_Unity_MapStorageAccess_ReleaseResource(handle);
            }

            return true;
        }

        public bool MergeSubGraphs(MapSubGraph[] subgraphs, bool onlyKeepLatestEdges, out MapSubGraph mergedSubgraph)
        {
            if (subgraphs.Length == 0)
            {
                Log.Error("Cannot MergeSubGraphs with no input");
                mergedSubgraph = default;
                return false;
            }

            // Get data from subgraphs array
            var ptrHandles = new GCHandle[subgraphs.Length];
            var ptrsArray = new IntPtr[subgraphs.Length];
            var sizesArray = new UInt32[subgraphs.Length];
            for (var i = 0; i < subgraphs.Length; i++)
            {
                var subgraphData = subgraphs[i].GetData();
                sizesArray[i] = (UInt32)subgraphData.Length;
                ptrHandles[i] = GCHandle.Alloc(subgraphData, GCHandleType.Pinned);
                unsafe
                {
                    fixed (byte* ptr = subgraphData)
                    {
                        ptrsArray[i] = (IntPtr)ptr;
                    }
                }
            }

            // Merge
            IntPtr mergedSubGraphHandle = IntPtr.Zero;
            UInt32 subgraphsCount = (UInt32)subgraphs.Length;
            unsafe
            {
                IntPtr dataPtr = IntPtr.Zero;
                IntPtr sizesPtr = IntPtr.Zero;
                fixed (IntPtr* dataRawPtr = ptrsArray)
                {
                    dataPtr = (IntPtr) dataRawPtr;
                    fixed (UInt32* sizesRawPtr = sizesArray)
                    {
                        sizesPtr = (IntPtr)sizesRawPtr;
                        mergedSubGraphHandle =
                            Lightship_ARDK_Unity_MapStorageAccess_MergeSubGraphs(dataPtr, sizesPtr, subgraphsCount,
                                onlyKeepLatestEdges);
                    }
                }
            }

            // Clean up pinned subgraph pointer handles
            foreach (var ptrHandle in ptrHandles)
            {
                ptrHandle.Free();
            }

            // Extract
            mergedSubgraph = GetMapSubGraph(mergedSubGraphHandle);

            // Clean Up merged graph resource
            Lightship_ARDK_Unity_MapStorageAccess_ReleaseResource(mergedSubGraphHandle);

            return true;
        }

        public void CreateAnchorPayloadFromMapNode(MapNode map, Matrix4x4 pose, out byte[] anchorPayload)
        {
            var mapNodeId = map.GetNodeId();
            var poseArray = MatrixConversionHelper.Matrix4x4ToInternalArray(pose.FromUnityToArdk());
            var anchorHandle = Lightship_ARDK_Unity_MapStorageAccess_CreateAnchor(ref mapNodeId, poseArray);

            Lightship_ARDK_Unity_MapStorageAccess_ExtractAnchor(anchorHandle, out IntPtr dataPtr, out UInt32 dataSize);

            anchorPayload = new byte[dataSize];
            Marshal.Copy(dataPtr, anchorPayload, 0, (int)dataSize);
            Lightship_ARDK_Unity_MapStorageAccess_ReleaseResource(anchorHandle);
        }

        public void ExtractMapMetaData(byte[] mapBlob, out Vector3[] points, out float[] errors, out Vector3 center, out string mapType)
        {

            IntPtr mapBlobPtr = IntPtr.Zero;
            unsafe
            {
                fixed (byte* bytePtr = mapBlob)
                {
                    mapBlobPtr = (IntPtr)bytePtr;
                    var handle = Lightship_ARDK_Unity_MapStorageAccess_ExtractMapMetadata
                    (
                        mapBlobPtr,
                        (UInt64)mapBlob.Length,
                        out var pointsXyzPtr,
                        out var errorsPtr,
                        out var pointsCount,
                        out var centerX,
                        out var centerY,
                        out var centerZ,
                        out var decriptor_name
                    );
                    var pointsArray = new float[pointsCount * 3];
                    errors = new float[pointsCount];
                    Marshal.Copy(pointsXyzPtr, pointsArray, 0, pointsArray.Length);
                    Marshal.Copy(errorsPtr, errors, 0, errors.Length);
                    points = new Vector3[pointsCount];
                    for (var i = 0; i < (int)pointsCount; i++)
                    {
                        points[i].x = pointsArray[i * 3] - centerX;
                        points[i].y = -(pointsArray[i * 3 + 1] - centerY); // convert to unity coords
                        points[i].z = pointsArray[i * 3 + 2] - centerZ;
                    }

                    center = new Vector3(centerX, -centerY, centerZ);  // convert to unity coords
                    mapType = Marshal.PtrToStringAnsi(decriptor_name);

                    // clean up the native side resource for the metadata
                    Lightship_ARDK_Unity_MapStorageAccess_ReleaseMapMetadata(handle);
                }
            }
        }

        private MapNode GetMapNode(IntPtr handle)
        {
            // handle validity has to be checked in the caller side

            // Get data bytes
            var success = Lightship_ARDK_Unity_MapStorageAccess_ExtractMap
                (handle, out var nodeId, out var dataPtr, out var dataSize);
            if (!success || dataPtr == IntPtr.Zero)
            {
                Debug.Log("GetMapNode(): Couldn't extract device map!");
                return default;
            }

            var dataBytes = new byte[dataSize];
            Marshal.Copy(dataPtr, dataBytes, 0, (int)dataSize);

            return new MapNode(nodeId, dataBytes);
        }

        private MapSubGraph GetMapSubGraph(IntPtr handle)
        {
            // handle validity has to be checked in the caller side

            // Get data bytes
            bool success =
                Lightship_ARDK_Unity_MapStorageAccess_ExtractGraph(handle, out var dataPtr, out var dataSize,
                    out var edgeType);
            if (!success || dataPtr == IntPtr.Zero)
            {
                Log.Error("GetMapNode(): Couldn't extract device graph blob!");
                return default;
            }

            var dataBytes = new byte[dataSize];
            Marshal.Copy(dataPtr, dataBytes, 0, (int)dataSize);

            return new MapSubGraph(dataBytes, (OutputEdgeType)edgeType);
        }

        private bool CheckNativeHandle()
        {
            if (!_nativeHandle.IsValidHandle())
            {
                Debug.LogWarning("No valid MapStorageAccess module handle");
                return false;
            }

            return true;
        }

        // Native function declarations

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_MapStorageAccess_Create(IntPtr unity_context);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapStorageAccess_Release(IntPtr feature_handle);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapStorageAccess_Start(IntPtr feature_handle);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapStorageAccess_Stop(IntPtr feature_handle);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapStorageAccess_Configure(IntPtr feature_handle,
            MapStorageAccessConfigurationCStruct config);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapStorageAccess_AddMap(IntPtr feature_handle, IntPtr dataPtr,
            int dataSize);


        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapStorageAccess_AddGraph(IntPtr feature_handle, IntPtr dataPtr,
            int dataSize);

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapStorageAccess_Clear(IntPtr feature_handle);

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_MapStorageAccess_AcquireMaps
        (
            IntPtr feature_handle,
            out IntPtr elements,
            out UInt32 count
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_MapStorageAccess_AcquireGraphs
        (
            IntPtr handle,
            out IntPtr elements,
            out UInt32 count
        );

        [DllImport(LightshipPlugin.Name)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Lightship_ARDK_Unity_MapStorageAccess_ExtractMap
        (
            IntPtr map_handle,
            out TrackableId node_id_out,
            out IntPtr map_data_out,
            out UInt32 map_data_size_out
        );

        [DllImport(LightshipPlugin.Name)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Lightship_ARDK_Unity_MapStorageAccess_ExtractGraph
        (
            IntPtr blob_handle,
            out IntPtr blob_data_out,
            out UInt32 blob_data_size_out,
            out UInt64 output_edge_type
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_MapStorageAccess_MergeSubGraphs
        (
            IntPtr dataPtr,
            IntPtr sizesPtr,
            UInt32 subgraphsCount,
            [MarshalAs(UnmanagedType.I1)] bool onlyKeepLatestEdges
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_MapStorageAccess_CreateAnchor
        (
            ref TrackableId mapNodeId,
            float[] pose
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapStorageAccess_ExtractAnchor
        (
            IntPtr anchorHandle,
            out IntPtr anchorPayloadPtr,
            out UInt32 anchorPayloadSize
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapStorageAccess_ReleaseResource
        (
            IntPtr anchorHandle
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern IntPtr Lightship_ARDK_Unity_MapStorageAccess_ExtractMapMetadata
        (
            IntPtr map_blob,
            UInt64 map_blob_size,
            out IntPtr points_xyz,
            out IntPtr errors,
            out UInt64 points_count,
            out float center_x,
            out float center_y,
            out float center_z,
            out IntPtr decriptor_name
        );

        [DllImport(LightshipPlugin.Name)]
        private static extern void Lightship_ARDK_Unity_MapStorageAccess_ReleaseMapMetadata
        (
            IntPtr map_metadata_handle
        );
    }
}
