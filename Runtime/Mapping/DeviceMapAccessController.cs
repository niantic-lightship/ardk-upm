// Copyright 2022-2025 Niantic.

using System;

using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.MapStorageAccess;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Mapping
{
    /// <summary>
    /// Class to access primitive device map data and configs
    /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
    /// </summary>
    [Experimental]
    [PublicAPI]
    public class DeviceMapAccessController
    {
        private IntPtr _unityContextHandleCache = IntPtr.Zero;
        private static DeviceMapAccessController _instance;

        private static object _instanceLock = new object();
        public static DeviceMapAccessController Instance {
            get
            {
                lock (_instanceLock)
                {
                    // If LightshipUnityContext is not initialized, return null
                    if (LightshipUnityContext.UnityContextHandle == IntPtr.Zero)
                    {
                        if (_instance != null)
                        {
                            _instance.Destroy();
                            _instance = null;
                        }
                        return null;
                    }

                    // If the instance hasn't been created yet, create it and initialize it
                    if (_instance == null)
                    {
                        _instance = new DeviceMapAccessController();
                        // Deregister no-ops if we haven't registered yet. Prevents double registration
                        //  in case someone else destroys the instance
                        LightshipUnityContext.OnDeinitialized -= DestroyNativeInstance;
                        LightshipUnityContext.OnDeinitialized += DestroyNativeInstance;

                        _instance.Init();
                        return _instance;
                    }

                    // If the Unity context handle hasn't changed, return the instance
                    if (_instance._unityContextHandleCache == LightshipUnityContext.UnityContextHandle)
                    {
                        Debug.Log("Returning cached instance");
                        return _instance;
                    }

                    // If the Unity context handle has changed, destroy the native instance and create a new one
                    _instance.Destroy();
                    _instance.Init();

                    return _instance;
                }
            }
        }

        internal DeviceMapAccessController()
        {
            _api = new NativeMapStorageAccessApi();
        }

        /// <summary>
        /// Specifies what type of edges will be output by GetSubGraphs()
        /// When set, this config takes in effect immediately
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public OutputEdgeType OutputEdgeType
        {
            get => _outputEdgeType;
            set
            {
                _outputEdgeType = value;
            }
        }

        private IMapStorageAccessApi _api;

        internal void Init()
        {
            if (LightshipUnityContext.UnityContextHandle == IntPtr.Zero)
            {
                Log.Error("Unity context handle is not initialized yet. " +
                    "DeviceMapAccessController cannot be initialized");
                return;
            }

            if (_unityContextHandleCache == LightshipUnityContext.UnityContextHandle)
            {
                Log.Warning("DeviceMapAccessController is already initialized.");
                return;
            }

            _api = new NativeMapStorageAccessApi();
            _unityContextHandleCache = LightshipUnityContext.UnityContextHandle;
            _api.Create(_unityContextHandleCache);
        }

        internal void Destroy()
        {
            if (_api == null)
            {
                return;
            }

            _api.Dispose();
            _unityContextHandleCache = IntPtr.Zero;
            _api = null;
        }

        /// <summary>
        /// Clear map/graph node locally registered in the localizer
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void ClearDeviceMap() {
            _api?.Clear();
        }

        /// <summary>
        /// Starts uploading new maps generated from this call-on
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StartUploadingMaps() {
            _api?.StartUploadingMaps();
        }

        /// <summary>
        /// Stops uploading maps
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StopUploadingMaps() {
            _api?.StopUploadingMaps();
        }

        /// <summary>
        /// Starts downloading maps around to localize
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StartDownloadingMaps() {
            _api?.StartDownloadingMaps();
        }

        /// <summary>
        /// Stops downloading maps
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StopDownloadingMaps() {
            _api?.StopDownloadingMaps();
        }

        /// <summary>
        /// Starts getting cloud graph
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StartGettingGraphData() {
            _api?.StartGettingGraphData();
        }

        /// <summary>
        /// Stops getting cloud graph
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StopGettingGraphData() {
            _api?.StopGettingGraphData();
        }

        /// <summary>
        /// Marks map node for upload. Downloads are triggered by StartUploadingMaps. Returns false if op fails early.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public bool MarkMapNodeForUpload(TrackableId mapId) {
            if (_api == null)
            {
                return false;
            }
            return _api.MarkMapNodeForUpload(mapId);
        }

        /// <summary>
        /// Checks if map node was uploaded
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public bool HasMapNodeBeenUploaded(TrackableId mapId) {
            if (_api == null)
            {
                return false;
            }
            return _api.HasMapNodeBeenUploaded(mapId);
        }

        /// <summary>
        /// Add a map node to the localizer
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="dataBytes">map node blob data as a byte array</param>
        [Experimental]
        public void AddMapNode(byte[] dataBytes)
        {
            if (dataBytes == null || dataBytes.Length == 0)
            {
                Log.Error("Map node data is empty");
                return;
            }

            _api?.AddMapNode(dataBytes);
        }

        /// <summary>
        /// Add graph(s) to the localizer
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="dataBytes">graph blob data as a byte array</param>
        [Experimental]
        public void AddSubGraph(byte[] dataBytes)
        {
            if (dataBytes == null || dataBytes.Length == 0)
            {
                Log.Error("Subgraph data is empty");
                return;
            }

            _api?.AddSubGraph(dataBytes);
        }

        /// <summary>
        /// Get a list of current map nodes in the native map storage
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="mapIds">an array of map ids</param>
        /// <returns>True if any ids generated. False if no map has been generated so far</returns>
        [Experimental]
        public bool GetMapNodeIds(out TrackableId[] mapIds)
        {
            if (_api == null)
            {
                mapIds = Array.Empty<TrackableId>();
                return false;
            }

            return _api.GetMapNodeIds(out mapIds);
        }

        /// <summary>
        /// Get a list of current map nodes in the native map storage
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="subgraphIds">an array of map ids</param>
        /// <param name="outputEdgeType">specify what type of edges will be output</param>
        /// <returns>True if any ids generated. False if no map has been generated so far</returns>
        [Experimental]
        public bool GetSubGraphIds(out TrackableId[] subgraphIds, OutputEdgeType outputEdgeType = OutputEdgeType.All)
        {
            if (_api == null)
            {
                subgraphIds = Array.Empty<TrackableId>();
                return false;
            }

            return _api.GetSubGraphIds(out subgraphIds, outputEdgeType);
        }

        /// <summary>
        /// Get the map data generated
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="maps">an array of map data</param>
        /// <returns>True if any maps generated. False if no map has been generated so far</returns>
        [Experimental]
        public bool GetMapNodes(TrackableId[] mapIds, out MapNode[] maps)
        {
            if (_api == null)
            {
                maps = Array.Empty<MapNode>();
                return false;
            }

            return _api.GetMapNodes(mapIds, out maps);
        }

        /// <summary>
        /// Get graph data of map nodes
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="blobs">an array of graphs</param>
        /// <returns>True if any graph generated. False if no graph has been generated so far</returns>
        [Experimental]
        public bool GetSubGraphs(TrackableId[] subgraphIds, out MapSubGraph[] blobs)
        {
            if (_api == null)
            {
                blobs = Array.Empty<MapSubGraph>();
                return false;
            }

            return _api.GetSubGraphs(subgraphIds, out blobs);
        }

        [Experimental]
        public bool GetLatestUpdates(out MapNode[] mapNodes, out MapSubGraph[] subGraphs, OutputEdgeType outputEdgeType = OutputEdgeType.All)
        {
            if (_api == null)
            {
                mapNodes = Array.Empty<MapNode>();
                subGraphs = Array.Empty<MapSubGraph>();
                return false;
            }

            return _api.GetLatestUpdates(outputEdgeType, out mapNodes, out subGraphs);
        }

        /// <summary>
        /// Generates Anchor (as payload) from MapNode
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="map">A map node, device map</param>
        /// <param name="pose">A local pose of the anchor to create</param>
        /// <param name="anchorPayload"> anchor payload as byte array</param>
        /// <returns>True if byte array representing the anchor that can be wrapped by namespace Niantic.Lightship.AR.PersistentAnchors</returns>
        [Experimental]
        public bool CreateAnchorFromMapNode(MapNode map, Matrix4x4 pose, out byte[] anchorPayload)
        {
            if (_api == null)
            {
                anchorPayload = null;
                return false;
            }

            _api.CreateAnchorPayloadFromMapNode(map, pose, out anchorPayload);
            return true;
        }

        /// <summary>
        /// Merges Map Subgraphs
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="subgraphs">Array of subgraphs to merge</param>
        /// <param name="onlyKeepLatestEdges">If true, it only keeps latest edge between two given nodes</param>
        /// <param name="mergedSubgraph"> Output merged subgraph</param>
        /// <returns>True if merge succeeded</returns>
        [Experimental]
        public bool MergeSubGraphs(MapSubGraph[] subgraphs, bool onlyKeepLatestEdges, out MapSubGraph mergedSubgraph)
        {
            if (_api == null)
            {
                mergedSubgraph = default;
                return false;
            }

            _api.MergeSubGraphs(subgraphs, onlyKeepLatestEdges, out mergedSubgraph);
            return true;
        }

        /// <summary>
        /// Extract map metadata from the map blob data
        /// </summary>
        /// <param name="mapBlob">map blob data as byte array</param>
        /// <param name="points">feature points relative to the map center</param>
        /// <param name="errors">error of each points</param>
        /// <param name="center">map center in the mapping coordinate system</param>
        /// <param name="mapType">indicate type of the map data</param>
        [Experimental]
        public void ExtractMapMetaData(
            byte[] mapBlob,
            out Vector3[] points,
            out float[] errors,
            out Vector3 center,
            out string mapType
        )
        {
            if (_api == null)
            {
                points = Array.Empty<Vector3>();
                errors = Array.Empty<float>();
                center = Vector3.zero;
                mapType = string.Empty;
                return;
            }

            _api.ExtractMapMetaData(mapBlob, out points, out errors, out center, out mapType);
        }

        internal void UseFakeMapStorageAccessApi(IMapStorageAccessApi mapStorageAccessApi)
        {
            _api = mapStorageAccessApi;
        }

        private OutputEdgeType _outputEdgeType = OutputEdgeType.All;

        private static void DestroyNativeInstance()
        {
            lock (_instanceLock)
            {
                _instance?.Destroy();
            }
        }
    }
}
