// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.MapStorageAccess
{
    public class MapStorageAccessManager :
        MonoBehaviour
    {
        internal const string FeatureFlagName = "SlickMapping";

        private IMapStorageAccessApi _api;

        private bool _isRunning;

        /// <summary>
        /// Specifies what type of edges will be output by GetSubGraphs()
        /// When set, this config takes in effect immediately
        /// </summary>
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

        private void Awake()
        {
            _api = new NativeMapStorageAccessApi();
            _api.Create(LightshipUnityContext.UnityContextHandle);
            _isRunning = false;
        }

        private void OnDestroy()
        {
            if (_isRunning)
            {
                // stop before dispose if still running
                _api.Stop();
                _isRunning = false;
            }
            _api.Dispose();
        }

        private void Start()
        {
            if (!IsFeatureEnabled())
            {
                return;
            }
            _api.Start();
            _isRunning = true;
        }

        /// <summary>
        /// Clear map/graph node locally registered in the localizer
        /// </summary>
        public void Clear() {
            if (!IsFeatureEnabled())
            {
                return;
            }
            _api.Clear();
        }

        /// <summary>
        /// Add a map node to the localizer
        /// </summary>
        /// <param name="dataBytes">map node blob data as a byte array</param>
        public void AddMapNode(byte[] dataBytes)
        {
            if (!IsFeatureEnabled())
            {
                return;
            }

            if (dataBytes == null || dataBytes.Length == 0)
            {
                Log.Error("Map node data is empty");
                return;
            }

            _api.AddMapNode(dataBytes);
        }

        /// <summary>
        /// Add graph(s) to the localizer
        /// </summary>
        /// <param name="dataBytes">graph blob data as a byte array</param>
        public void AddSubGraph(byte[] dataBytes)
        {
            if (!IsFeatureEnabled())
            {
                return;
            }

            if (dataBytes == null || dataBytes.Length == 0)
            {
                Log.Error("Subgraph data is empty");
                return;
            }
            
            _api.AddSubGraph(dataBytes);
        }

        /// <summary>
        /// Get the map data generated
        /// </summary>
        /// <param name="maps">an array of map data</param>
        /// <returns>True if any maps generated. False if no map has been generated so far</returns>
        public bool GetMapNodes(out MapNode[] maps)
        {
            if (!IsFeatureEnabled())
            {
                maps = default;
                return false;
            }
            return _api.GetMapNodes(out maps);
        }

        /// <summary>
        /// Get graph data of map nodes
        /// </summary>
        /// <param name="blobs">an array of graphs</param>
        /// <returns>True if any graph generated. False if no graph has been generated so far</returns>
        public bool GetSubGraphs(out MapSubGraph[] blobs)
        {
            if (!IsFeatureEnabled())
            {
                blobs = default;
                return false;
            }
            return _api.GetSubGraphs(out blobs);
        }

        /// <summary>
        /// Generates Anchor (as payload) from MapNode
        /// </summary>
        /// <param name="map">A map node, device map</param>
        /// <param name="pose">A local pose of the anchor to create</param>
        /// <param name="anchorPayload"> anchor payload as byte array</param>
        /// <returns>True if byte array representing the anchor that can be wrapped by namespace Niantic.Lightship.AR.PersistentAnchors</returns>
        public bool CreateAnchorFromMapNode(MapNode map, Matrix4x4 pose, out byte[] anchorPayload)
        {
            if (!IsFeatureEnabled())
            {
                anchorPayload = default;
                return false;
            }
            _api.CreateAnchorPayloadFromMapNode(map, pose, out anchorPayload);
            return true;
        }

        /// <summary>
        /// Merges Map Subgraphs
        /// </summary>
        /// <param name="subgraphs">Array of subgraphs to merge</param>
        /// <param name="onlyKeepLatestEdges">If true, it only keeps latest edge between two given nodes</param>
        /// <param name="mergedSubgraph"> Output merged subgraph</param>
        /// <returns>True if merge succeeded</returns>
        public bool MergeSubGraphs(MapSubGraph[] subgraphs, bool onlyKeepLatestEdges, out MapSubGraph mergedSubgraph)
        {
            if (!IsFeatureEnabled())
            {
                mergedSubgraph = default;
                return false;
            }
            _api.MergeSubGraphs(subgraphs, onlyKeepLatestEdges, out mergedSubgraph);
            return true;
        }

        internal void UseFakeMapStorageAccessApi(IMapStorageAccessApi mapStorageAccessApi)
        {
            _api = mapStorageAccessApi;
        }

        private bool IsFeatureEnabled()
        {
            if (!LightshipUnityContext.FeatureEnabled(FeatureFlagName))
            {
                Log.Debug($"{FeatureFlagName} is disabled. Enable in the feature flag file");
                return false;
            }

            return true;
        }


        private OutputEdgeType _outputEdgeType = OutputEdgeType.All;
        private void SetConfiguration()
        {
            if (!IsFeatureEnabled())
            {
                return;
            }
            _api.Configure(_outputEdgeType);
        }
    }
}
