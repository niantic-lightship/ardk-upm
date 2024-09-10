// Copyright 2022-2024 Niantic.

using System;
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
        /// Define how fast to run device mapping. Default is 0, meaning process every frame.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [SerializeField]
        [Experimental]
        private uint _mappingTargetFrameRate = 0;

        /// <summary>
        /// Define device map split based on how far the user traveled.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [SerializeField]
        [Experimental]
        private float _mappingSplitterMaxDistanceMeters = 30.0f;

        /// <summary>
        /// Define device map split based on how long in time the user mapped.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [SerializeField]
        [Experimental]
        private float _mappingSplitterMaxDurationSeconds = 30.0f;

        // Events

        /// <summary>
        /// An event when device map data has been updated
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public event Action<ARDeviceMap> OnDeviceMapUpdated;

        // private vars

        private IMappingApi _mapper;
        private IMapStorageAccessApi _api;
        private OutputEdgeType _outputEdgeType = OutputEdgeType.All;

        private ARDeviceMap _arDeviceMap = new ();

        // Monobehaviour methods
        private void Awake()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }

            DeviceMapAccessController.Init();
            DeviceMappingController.Init();
        }

        private void Start()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }

            DeviceMappingController.TargetFrameRate = _mappingTargetFrameRate;
            DeviceMappingController.SplitterMaxDistanceMeters = _mappingSplitterMaxDistanceMeters;
            DeviceMappingController.SplitterMaxDurationSeconds = _mappingSplitterMaxDurationSeconds;
            DeviceMapAccessController.StartNativeModule();
            DeviceMappingController.StartNativeModule();
        }

        private void OnDestroy()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }
            DeviceMapAccessController.Destroy();
            DeviceMappingController.Destroy();
        }

        private void Update()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }
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
        ///  Start map generation
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StartMapping()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }

            DeviceMappingController.TargetFrameRate = _mappingTargetFrameRate;
            DeviceMappingController.SplitterMaxDistanceMeters = _mappingSplitterMaxDistanceMeters;
            DeviceMappingController.SplitterMaxDurationSeconds = _mappingSplitterMaxDurationSeconds;
            DeviceMappingController.StartMapping();
        }

        /// <summary>
        /// Stop map generation
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StopMapping()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }
            DeviceMappingController.StopMapping();
        }

        /// <summary>
        /// Set a Device Map to track. Use when loading a serialized device map and track it.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="arDeviceMap"> ARDeviceMap to track</param>
        [Experimental]
        public void SetDeviceMap(ARDeviceMap arDeviceMap)
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }

            _arDeviceMap = arDeviceMap;

            // set map nodes to localizer
            DeviceMapAccessController.ClearDeviceMap();
            foreach (var mapNode in _arDeviceMap.DeviceMapNodes)
            {
                DeviceMapAccessController.AddMapNode(mapNode._mapData);
            }
        }

        // Private methods

        private void TryToGetMapNodes()
        {
            var mapCreated = DeviceMapAccessController.GetMapNodes(out var maps);

            if (!mapCreated)
            {
                return;
            }

            if (maps.Length == 0)
            {
                return;
            }

            Log.Debug($"Map Generated {maps.Length}");

            // Create a struct which is ready to serialize and invoke OnDeviceMapUpdated event
            for (var i = 0; i < maps.Length; i++)
            {
                // Create an anchor at current camera position
                // TODO: map data should contain map center info and use that
                var pos = Camera.main.transform.position;
                DeviceMapAccessController.CreateAnchorFromMapNode(
                    maps[i], Matrix4x4.Translate(pos) , out var anchorPayload);

                // add the new map node to ARDeviceMap
                _arDeviceMap.AddDeviceMapNode(
                    maps[i].GetNodeId().subId1,
                    maps[i].GetNodeId().subId2,
                    maps[i].GetData(),
                    anchorPayload
                );
            }

            // Invoke an event
            OnDeviceMapUpdated?.Invoke(_arDeviceMap);
        }
    }
}
