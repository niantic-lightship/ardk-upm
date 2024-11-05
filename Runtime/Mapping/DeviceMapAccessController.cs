// Copyright 2022-2024 Niantic.

using System;

using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.MapStorageAccess;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

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
        private static IntPtr _unityContextHandleCache = IntPtr.Zero;
        private static DeviceMapAccessController _instance;

        private static object _instanceLock = new object();
        public static DeviceMapAccessController Instance {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        _instance = new DeviceMapAccessController();
                        LightshipUnityContext.OnDeinitialized += DestroyNativeInstance;
                    }

                    if (_unityContextHandleCache == IntPtr.Zero)
                    {
                        if (LightshipUnityContext.UnityContextHandle == IntPtr.Zero)
                        {
                            Log.Error("Unity context handle is not initialized yet. DeviceMapAccessController Instance cannot be created");
                            _instance = null;
                            return null;
                        }

                        _unityContextHandleCache = LightshipUnityContext.UnityContextHandle;
                        _instance.Init();
                    }
                    else if (_unityContextHandleCache != LightshipUnityContext.UnityContextHandle)
                    {
                        Log.Error
                        (
                            "Unity context handle has changed. Reinitializing DeviceMapAccessController Instance"
                        );

                        _instance.Destroy();
                        _unityContextHandleCache = LightshipUnityContext.UnityContextHandle;
                        _instance.Init();
                    }
                }

                return _instance;
            }
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
                // We can configure anytime so SetConfiguration whenever a value is changed
                SetConfiguration();
            }
        }

        private IMapStorageAccessApi _api;

        private bool _isRunning;

        internal void Init()
        {
            if (LightshipUnityContext.UnityContextHandle == IntPtr.Zero)
            {
                Log.Error("Unity context handle is not initialized yet. " +
                    "DeviceMapAccessController cannot be initialized");
                return;
            }

            _api = new NativeMapStorageAccessApi();
            _api.Create(LightshipUnityContext.UnityContextHandle);
            _isRunning = false;
        }

        internal void Destroy()
        {
            if (_api == null)
            {
                return;
            }

            if (_isRunning)
            {
                // stop before dispose if still running
                _api.Stop();
                _isRunning = false;
            }
            _api.Dispose();
            _api = null;
        }

        internal void StartNativeModule()
        {
            if (_api == null)
            {
                return;
            }

            _api.Start();
            _isRunning = true;
        }

        internal void StopNativeModule()
        {
            if (_api == null)
            {
                return;
            }

            _api.Stop();
            _isRunning = false;
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
        /// Get the map data generated
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="maps">an array of map data</param>
        /// <returns>True if any maps generated. False if no map has been generated so far</returns>
        [Experimental]
        public bool GetMapNodes(out MapNode[] maps)
        {
            if (_api == null)
            {
                maps = Array.Empty<MapNode>();
                return false;
            }

            return _api.GetMapNodes(out maps);
        }

        /// <summary>
        /// Get graph data of map nodes
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="blobs">an array of graphs</param>
        /// <returns>True if any graph generated. False if no graph has been generated so far</returns>
        [Experimental]
        public bool GetSubGraphs(out MapSubGraph[] blobs)
        {
            if (_api == null)
            {
                blobs = Array.Empty<MapSubGraph>();
                return false;
            }

            return _api.GetSubGraphs(out blobs);
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
        private void SetConfiguration()
        {
            _api?.Configure(_outputEdgeType);
        }

        private static void DestroyNativeInstance()
        {
            lock (_instanceLock)
            {
                if (_instance != null &&
                    _instance._api != null &&
                    _unityContextHandleCache != IntPtr.Zero)
                {
                    _instance.Destroy();
                    _unityContextHandleCache = IntPtr.Zero;
                }
            }
        }
    }
}
