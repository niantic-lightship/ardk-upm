// Copyright 2022-2025 Niantic.

using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.MapStorageAccess
{
    internal interface IMapStorageAccessApi :
        IDisposable
    {
        IntPtr Create(IntPtr moduleManager);

        void AddMapNode(byte[] dataBytes);

        void AddSubGraph(byte[] dataBytes);

        void Clear();

        void StartUploadingMaps();

        void StopUploadingMaps();

        void StartDownloadingMaps();

        void StopDownloadingMaps();

        void StartGettingGraphData();

        void StopGettingGraphData();

        bool MarkMapNodeForUpload(TrackableId mapId);

        bool HasMapNodeBeenUploaded(TrackableId mapId);

        bool GetMapNodeIds(out TrackableId[] ids);

        bool GetSubGraphIds(out TrackableId[] ids, OutputEdgeType outputEdgeType);

        bool GetMapNodes(TrackableId[] mapIds, out MapNode[] maps);

        bool GetSubGraphs(TrackableId[] subgraphIds, out MapSubGraph[] blobs);

        bool GetLatestUpdates(OutputEdgeType outputEdgeType, out MapNode[] mapNodes, out MapSubGraph[] subGraphs);

        bool MergeSubGraphs(MapSubGraph[] subgraphs, bool onlyKeepLatestEdges, out MapSubGraph mergedSubgraph);

        void CreateAnchorPayloadFromMapNode(MapNode map, Matrix4x4 pose, out byte[] anchorPayload);

        void ExtractMapMetaData(byte[] mapBlob, out Vector3[] points, out float[] errors, out Vector3 center, out string mapType);
    }
}
