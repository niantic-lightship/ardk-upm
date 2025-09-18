// Copyright 2022-2025 Niantic.

using System;
using System.Collections;
using System.Collections.Generic;

using Niantic.Lightship.AR.MapStorageAccess;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

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
            private set;
        }

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
        /// Property access for whether map upload will be enabled during mapping
        /// Set this property before StartMapping()
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public bool MapUploadEnabled
        {
            get => _mapUploadEnabled;
            set
            {
                _mapUploadEnabled = value;
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

        /// <summary>
        /// Define if map upload will be enabled during mapping
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [SerializeField]
        [Experimental]
        private bool _mapUploadEnabled = false;

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

        private const float TimeoutToForceInvokeMapFinalizedEvent = 2.0f;

        private enum MappingState
        {
            Uninitialized,
            Mapping,
            Stopped,
        }
        private MappingState _state = MappingState.Uninitialized;

        // Monobehaviour methods
        private void Awake()
        {
            DeviceMapAccessController = DeviceMapAccessController.Instance;
            DeviceMappingController.Init();
        }

        private void OnEnable()
        {
            DeviceMappingController.UpdateConfiguration();
            DeviceMappingController.StartNativeModule();
        }

        private void OnDisable()
        {
            _state = MappingState.Uninitialized;
            DeviceMappingController.StopNativeModule();
        }

        private void Start()
        {
            _state = MappingState.Uninitialized;
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
            TryUpdateMapsAndSubgraphs();
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
            DeviceMappingController.StopNativeModule();
            yield return null;
            DeviceMappingController.StartNativeModule();
        }

        /// <summary>
        ///  Start map generation
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StartMapping()
        {
            _state = MappingState.Mapping;

            if (_mapUploadEnabled)
            {
                DeviceMapAccessController.StartUploadingMaps();
            }

            // Run mapping
            DeviceMappingController.StartMapping();
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

            if (_mapUploadEnabled)
            {
                DeviceMapAccessController.StopUploadingMaps();
            }

            _state = MappingState.Stopped;
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

        private void TryUpdateMapsAndSubgraphs()
        {
            var gotNewData = DeviceMapAccessController.GetLatestUpdates(out var maps, out var subGraphs);

            if (!gotNewData)
            {
                return;
            }

            bool gotNewMap = (maps != null && maps.Length != 0);
            bool gotNewSubGraph = (subGraphs != null && subGraphs.Length != 0);

            if (gotNewMap)
            {
                // Create a struct which is ready to serialize and invoke OnDeviceMapUpdated event
                for (var i = 0; i < maps.Length; i++)
                {

                    if (_mapUploadEnabled)
                    {
                        if (!DeviceMapAccessController.HasMapNodeBeenUploaded(maps[i].GetNodeId()))
                        {
                            if (!DeviceMapAccessController.MarkMapNodeForUpload(maps[i].GetNodeId()))
                            {
                                Log.Debug($"UploadExistingMaps(): MarkMapNodeForUpload failed");
                            }
                        }
                    }

                    if (_arDeviceMap.HasMapNode(maps[i].GetNodeId()))
                    {
                        continue;
                    }

                    // Create an anchor for the map node at the center of the map key points
                    DeviceMapAccessController.ExtractMapMetaData(
                        maps[i].GetData(),
                        out var points, // unused here
                        out var errors, // unused here
                        out var center,
                        out var mapType
                    );

                    // Additional validation before creating anchor transform
                    if (float.IsNaN(center.x) || float.IsNaN(center.y) || float.IsNaN(center.z) ||
                        float.IsInfinity(center.x) || float.IsInfinity(center.y) || float.IsInfinity(center.z))
                    {
                        Log.Error($"ARDeviceMappingManager: Center contains NaN or infinity values after ExtractMapMetaData: {center}. " +
                            "Skipping anchor creation for this map node.");
                        continue;
                    }

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
            }

            if (gotNewSubGraph)
            {
                MapSubGraph[] graphs;
                // If there is no graph data, just use the new graph array
                if (_arDeviceMap.DeviceMapGraph._graphData == null)
                {
                    graphs = subGraphs;
                }
                else
                {
                    // Otherwise, create a subgraph array with the existing graph data and the new graph data
                    graphs = new MapSubGraph[subGraphs.Length + 1];
                    Array.Copy(subGraphs, graphs, subGraphs.Length);
                    graphs[^1] = new MapSubGraph(_arDeviceMap.DeviceMapGraph._graphData);
                }

                // Merge the subgraphs into a single graph
                var mergeSucceeded = DeviceMapAccessController.MergeSubGraphs(
                    graphs,
                    true,
                    out var mergedGraph
                );

                // Copy the merged graph data to the ARDeviceMap
                if (mergeSucceeded)
                {
                    _arDeviceMap.SetDeviceMapGraph(mergedGraph.GetData());
                }
            }

            // Invoke events

            // invoke "update" event always
            DeviceMapUpdated?.Invoke(_arDeviceMap);

            // invoke "finalized" event if stopped and received (last) map
            if (_state == MappingState.Stopped && gotNewMap)
            {
                // TODO: what if second time? prevent envent?
                DeviceMapFinalized?.Invoke(_arDeviceMap);
                _state = MappingState.Uninitialized;
            }
        }

        private IEnumerator MonitorFinalizedEventCoroutine()
        {
            // TODO: modify native side to tell final map update or not, instead of this way
            yield return new WaitForSeconds(TimeoutToForceInvokeMapFinalizedEvent);

            if (_state == MappingState.Stopped)
            {
                DeviceMapFinalized?.Invoke(_arDeviceMap);
                _state = MappingState.Uninitialized;
            }
        }
    }
}
