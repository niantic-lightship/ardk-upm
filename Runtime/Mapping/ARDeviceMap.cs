// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.PersistentAnchors;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.Mapping
{
    /// <summary>
    /// ARDeviceMap encapsulates device map data generated from mapping process, and provides to serialize/deserialize
    /// device map for persistent or sharing purpose.
    /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
    /// </summary>
    [Experimental]
    [PublicAPI]
    public class ARDeviceMap
    {
        /// <summary>
        /// A struct representing a single device map node
        /// </summary>
        [Serializable]
        [Experimental]
        public struct SerializeableDeviceMapNode
        {
            public ulong _subId1;
            public ulong _subId2;
            public byte[] _mapData;
            public byte[] _anchorPayload;
            public string _mapType;
        }

        [Serializable]
        [Experimental]
        public struct SerializeableDeviceMapGraph
        {
            public byte[] _graphData;
        }

        /// <summary>
        /// A struct to serialize/desrialize entire ARDeviceMap
        /// </summary>
        [Serializable]
        [Experimental]
        public struct SerializableDeviceMap
        {
            public SerializeableDeviceMapNode[] _serializeableSingleDeviceMaps;

            public SerializeableDeviceMapGraph _graphData;
        }


        /// <summary>
        /// Get a list of SerializeableDeviceMapNode, either mapped on the device or deserialized
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public List<SerializeableDeviceMapNode> DeviceMapNodes
        {
            get => _deviceMapNodes;
        }

        /// <summary>
        /// Get a SerializeableDeviceMapGraph in this device map
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public SerializeableDeviceMapGraph DeviceMapGraph
        {
            get => _deviceMapGraph;
        }

        public int DefaultAnchorIndex
        {
            get => _defaultAnchorIndex;
        }

        public ARDeviceMap()
        {

        }

        protected List<SerializeableDeviceMapNode> _deviceMapNodes = new ();
        protected SerializeableDeviceMapGraph _deviceMapGraph = new();
        protected int _defaultAnchorIndex = 0;

        /// <summary>
        /// Add a device map node. This method is meant to be called by ARDeviceMappingManager when a device map is generated.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="subId1"></param>
        /// <param name="subId2"></param>
        /// <param name="mapData"></param>
        /// <param name="anchorPayload"></param>
        [Experimental]
        public void AddDeviceMapNode(
            ulong subId1,
            ulong subId2,
            byte[] mapData,
            byte[] anchorPayload,
            string mapType
        )
        {
            var mapNode = new SerializeableDeviceMapNode
            {
                _subId1 = subId1,
                _subId2 = subId2,
                _mapData = mapData,
                _anchorPayload = anchorPayload,
                _mapType = mapType
            };
            _deviceMapNodes.Add(mapNode);
        }

        /// <summary>
        /// Set graph blob data. This method is meant to be called by ARDeviceMappingManager when a device map and graph
        /// is generated.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="graphData"></param>
        [Experimental]
        public void SetDeviceMapGraph(byte[] graphData)
        {
            var graph = new SerializeableDeviceMapGraph()
            {
                _graphData = graphData
            };
            _deviceMapGraph = graph;
        }

        /// <summary>
        /// Get serialized device map
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <returns>Serailized device map as byte array</returns>
        [Experimental]
        public virtual byte[] Serialize()
        {
            // construct serializable DeviceMap
            var serialzableDeviceMapNodes = new SerializeableDeviceMapNode[_deviceMapNodes.Count];
            for (var i = 0; i < _deviceMapNodes.Count; i++)
            {
                serialzableDeviceMapNodes[i] = _deviceMapNodes[i];
            }

            var serialiableMapNode = new SerializableDeviceMap()
            {
                _serializeableSingleDeviceMaps = serialzableDeviceMapNodes,
                _graphData = _deviceMapGraph
            };

            //
            byte[] buf;
            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, serialiableMapNode);
                buf = stream.ToArray();
            }

            return buf;
        }

        /// <summary>
        /// Create an instance of ARDeviceMap from serialized device map data
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="serializedDeviceMap">Serialized device map as byte array</param>
        /// <returns>An instance of ARDeviceMap</returns>
        [Experimental]
        public static ARDeviceMap CreateFromSerializedData(byte[] serializedDeviceMap)
        {
            SerializableDeviceMap serialiableMapNode;
            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream(serializedDeviceMap))
            {
                serialiableMapNode = (SerializableDeviceMap)formatter.Deserialize(stream);
            }

            var deviceMap = new ARDeviceMap();
            for (var i = 0; i < serialiableMapNode._serializeableSingleDeviceMaps.Length; i++)
            {
                deviceMap._deviceMapNodes.Add(serialiableMapNode._serializeableSingleDeviceMaps[i]) ;
            }

            deviceMap._deviceMapGraph = serialiableMapNode._graphData;

            return deviceMap;
        }

        /// <summary>
        /// Get the anchor payload of this device map
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <returns>Persistent anchor payload as byte array</returns>
        [Experimental]
        public byte[] GetAnchorPayload()
        {
            if (_deviceMapNodes.Count > 0)
            {
                return _deviceMapNodes[_defaultAnchorIndex]._anchorPayload;
            }
            return null;
        }

        /// <summary>
        /// Check if this ARDeviceMap has valid device map data. In case not having valid map data, this ARDviceMap
        /// should not be used for serialization
        /// </summary>
        /// <returns>True if it has valid device map data. Otherwise false.</returns>
        [Experimental]
        public bool HasValidMap()
        {
            return _deviceMapNodes.Count > 0;
        }
    }
}
