// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.MapStorageAccess
{
    internal interface IMapStorageAccessApi :
        IDisposable
    {
        IntPtr Create(IntPtr moduleManager);

        void Start();

        void Stop();

        void Configure(OutputEdgeType edgeType);

        void AddMapNode(byte[] dataBytes);

        void AddSubGraph(byte[] dataBytes);

        void Clear();

        bool GetMapNodes(out MapNode[] maps);

        bool GetSubGraphs(out MapSubGraph[] blobs);

        bool MergeSubGraphs(MapSubGraph[] subgraphs, bool onlyKeepLatestEdges, out MapSubGraph mergedSubgraph);

        void CreateAnchorPayloadFromMapNode(MapNode map, Matrix4x4 pose, out byte[] anchorPayload);

        void ExtractMapMetaData(byte[] mapBlob, out Vector3[] points, out float[] errors, out Vector3 center, out string mapType);
    }
}
