// Copyright 2022-2024 Niantic.

using System;
using System.Collections;
using Niantic.Lightship.AR.MapStorageAccess;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.Mapping
{
    /// <summary>
    /// ARDeviceMappingManager can be used to generate device map and set the device map to track
    /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
    /// </summary>
    [Experimental]
    [PublicAPI]
    public class ARDeviceMappingManager : MonoBehaviour
    {

        // Public properties

        /// <summary>
        /// Get DeviceMapAccessController, which provides primitive access to the device map and related info
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public DeviceMapAccessController DeviceMapAccessController
        {
            get;
        } = new();

        /// <summary>
        /// Get DeviceMappingController, which provides primitive API for device mapping
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public DeviceMappingController DeviceMappingController
        {
            get;
        } = new();

        /// <summary>
        /// Get the ARDeviceMap object in this manager
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public ARDeviceMap ARDeviceMap
        {
            get => _arDeviceMap;
        }

        /// <summary>
        /// Property access for mapping speed
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public uint MappingTargetFrameRate
        {
            get => _mappingTargetFrameRate;
            set
            {
                _mappingTargetFrameRate = value;
            }
        }

        /// <summary>
        /// Property access for map splitting criteria by distance
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public float MappingSplitterMaxDistanceMeters
        {
            get => _mappingSplitterMaxDistanceMeters;
            set
            {
                _mappingSplitterMaxDistanceMeters = value;
            }
        }

        /// <summary>
        /// Property access for map splitting criteria by time
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public float MappingSplitterMaxDurationSeconds
        {
            get => _mappingSplitterMaxDurationSeconds;
            set
            {
                _mappingSplitterMaxDurationSeconds = value;
            }
        }

        /// <summary>
        /// A state if mapping is in progress or not. True is mapping is ongoing. Becomes false after calling StopMapping()
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public bool IsMappingInProgress
        {
            get => DeviceMappingController.IsMapping;
        }

        /// <summary>
        /// Define how fast to run device mapping. Default is 0, meaning process every frame.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [SerializeField]
        [Experimental]
        private uint _mappingTargetFrameRate = DeviceMappingController.DefaultTargetFrameRate;

        /// <summary>
        /// Define device map split based on how far the user traveled.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [SerializeField]
        [Experimental]
        private float _mappingSplitterMaxDistanceMeters = DeviceMappingController.DefaultSplitterMaxDistanceMeters;

        /// <summary>
        /// Define device map split based on how long in time the user mapped.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [SerializeField]
        [Experimental]
        private float _mappingSplitterMaxDurationSeconds = DeviceMappingController.DefaultSplitterMaxDurationSeconds;

        // Events

        /// <summary>
        /// An event when device map data has been updated
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public event Action<ARDeviceMap> DeviceMapUpdated;

        /// <summary>
        /// An event when device map is finalized and ready to save
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public event Action<ARDeviceMap> DeviceMapFinalized;

        // private vars

        private ARDeviceMap _arDeviceMap = new ();

        private bool _mapFinalizedEventInvoked = false;

        private const float TimeoutToForceInvokeMapFinalizedEvent = 2.0f;

        // Monobehaviour methods
        private void Awake()
        {
            DeviceMapAccessController.Init();
            DeviceMappingController.Init();
        }

        private void OnEnable()
        {
            DeviceMapAccessController.StartNativeModule();
            DeviceMappingController.UpdateConfiguration();
            DeviceMappingController.StartNativeModule();
        }

        private void OnDisable()
        {
            DeviceMapAccessController.StopNativeModule();
            DeviceMappingController.StopNativeModule();

        }

        private void Start()
        {
            DeviceMappingController.TargetFrameRate = _mappingTargetFrameRate;
            DeviceMappingController.SplitterMaxDistanceMeters = _mappingSplitterMaxDistanceMeters;
            DeviceMappingController.SplitterMaxDurationSeconds = _mappingSplitterMaxDurationSeconds;
        }

        private void OnDestroy()
        {
            DeviceMapAccessController.Destroy();
            DeviceMappingController.Destroy();
        }

        private void Update()
        {
            // Check map/graph generated and sync every 10 frames
            // TODO: make it configurable how often processing map/graph sync
            if (Time.frameCount % 10 != 0)
            {
                return;
            }

            // collect map
            TryToGetMapNodes();
        }

        // public methods

        /// <summary>
        /// Asynchronously restarts the underlying module with the current configuration.
        /// </summary>
        public IEnumerator RestartModuleAsyncCoroutine()
        {
            DeviceMappingController.TargetFrameRate = _mappingTargetFrameRate;
            DeviceMappingController.SplitterMaxDistanceMeters = _mappingSplitterMaxDistanceMeters;
            DeviceMappingController.SplitterMaxDurationSeconds = _mappingSplitterMaxDurationSeconds;
            DeviceMappingController.UpdateConfiguration();

            // restart native modules to enable new configs
            yield return null;
            DeviceMapAccessController.StopNativeModule();
            DeviceMappingController.StopNativeModule();
            yield return null;
            DeviceMapAccessController.StartNativeModule();
            DeviceMappingController.StartNativeModule();
        }

        /// <summary>
        ///  Start map generation
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StartMapping()
        {
            // Run mapping
            DeviceMappingController.StartMapping();
            _mapFinalizedEventInvoked = false;
        }

        /// <summary>
        /// Stop map generation
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StopMapping()
        {
            DeviceMappingController.StopMapping();
            StartCoroutine(MonitorFinalizedEventCoroutine());
        }

        /// <summary>
        /// Set a Device Map to track. Use when loading a serialized device map and track it.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="arDeviceMap"> ARDeviceMap to track</param>
        [Experimental]
        public void SetDeviceMap(ARDeviceMap arDeviceMap)
        {
            _arDeviceMap = arDeviceMap;

            // set map nodes to localizer
            DeviceMapAccessController.ClearDeviceMap();
            foreach (var mapNode in _arDeviceMap.DeviceMapNodes)
            {
                DeviceMapAccessController.AddMapNode(mapNode._mapData);
            }
            // set graphs to the localizer
            if (_arDeviceMap.DeviceMapGraph._graphData != null)
            {
                DeviceMapAccessController.AddSubGraph(_arDeviceMap.DeviceMapGraph._graphData);
            }
        }

        /// <summary>
        /// Extract map metadata from the currently set device map. Could be used for debugging and/or visual user feedback
        /// where map is
        /// </summary>
        /// <param name="points">feature points coordinates relative to the anchor/map center</param>
        /// <param name="errors">estimated errors of each points. Smaller error points could be more significant feature points</param>
        /// <param name="center">center point coordinates in the mapped coordinate system</param>
        [Experimental]
        public void ExtractMapMetadata(
            out Vector3[] points,
            out float[] errors,
            out Vector3 center,
            out string mapType
        )
        {
            // Get the points for the first node only
            DeviceMapAccessController.ExtractMapMetaData(
                _arDeviceMap.DeviceMapNodes[_arDeviceMap.DefaultAnchorIndex]._mapData,
                out points,
                out errors,
                out center,
                out mapType
            );
        }

        // Private methods

        private void TryToGetMapNodes()
        {
            var mapCreated = DeviceMapAccessController.GetMapNodes(out var maps);

            if (!mapCreated)
            {
                // Ignore if there is no additional map nodes
                return;
            }

            if (maps.Length == 0)
            {
                // Ignore if there is no additional map nodes
                return;
            }

            Log.Debug($"Map Generated {maps.Length}");

            // Create a struct which is ready to serialize and invoke OnDeviceMapUpdated event
            for (var i = 0; i < maps.Length; i++)
            {
                // Create an anchor for the map node at the center of the map key points
                DeviceMapAccessController.ExtractMapMetaData(
                    maps[i].GetData(),
                    out var points, // unused here
                    out var errors, // unused here
                    out var center,
                    out var mapType
                );
                DeviceMapAccessController.CreateAnchorFromMapNode(
                    maps[i], Matrix4x4.Translate(center) , out var anchorPayload);

                // add the new map node to ARDeviceMap
                _arDeviceMap.AddDeviceMapNode(
                    maps[i].GetNodeId().subId1,
                    maps[i].GetNodeId().subId2,
                    maps[i].GetData(),
                    anchorPayload,
                    mapType
                );
                Log.Debug($"map type = {mapType}");
            }

            // Add graphs if available
            var graphCreated = DeviceMapAccessController.GetSubGraphs(out var newGraphs);
            if (graphCreated && newGraphs.Length > 0)
            {
                MapSubGraph[] graphs;
                if (_arDeviceMap.DeviceMapGraph._graphData == null)
                {
                    graphs = newGraphs;
                }
                else
                {
                    graphs = new MapSubGraph[newGraphs.Length + 1];
                    Array.Copy(newGraphs, graphs, newGraphs.Length);
                    graphs[^1] = new MapSubGraph(_arDeviceMap.DeviceMapGraph._graphData);
                }
                var mergeSucceeded = DeviceMapAccessController.MergeSubGraphs(
                    graphs,
                    true,
                    out var mergedGraph
                );
                if (mergeSucceeded)
                {
                    _arDeviceMap.SetDeviceMapGraph(mergedGraph.GetData());
                }
            }

            // Invoke events

            // invoke "update" event always
            DeviceMapUpdated?.Invoke(_arDeviceMap);

            // invoke "finalized" event if not mapping
            if (!DeviceMappingController.IsMapping)
            {
                // TODO: what if second time? prevent envent?
                DeviceMapFinalized?.Invoke(_arDeviceMap);
                _mapFinalizedEventInvoked = true;
            }
        }

        private IEnumerator MonitorFinalizedEventCoroutine()
        {
            // TODO: modify native side to tell final map update or not, instead of this way
            yield return new WaitForSeconds(TimeoutToForceInvokeMapFinalizedEvent);

            if (!_mapFinalizedEventInvoked)
            {
                DeviceMapFinalized?.Invoke(_arDeviceMap);
            }
        }
    }
}
