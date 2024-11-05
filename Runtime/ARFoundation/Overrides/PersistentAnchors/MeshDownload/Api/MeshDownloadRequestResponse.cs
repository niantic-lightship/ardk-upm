// Copyright 2022 - 2024 Niantic.

using System;
using System.Collections.Generic;

using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems
{
    // Defines serializable objects to use as request/response json formats for VPS requests
    public static class MeshDownloadRequestResponse
    {
        // GetMeshUrl request enums
        public enum MeshAlgorithm : int
        {
            UNSPECIFIED = 0,

            /// Uses a decimated vertex colored mesh for rendering. Lower quality, but faster to download and render.
            VERTEX_COLORED = 3,

            /// Uses a textured mesh for rendering. Higher quality, but slower to download and render.
            TEXTURED = 5,
        }

        internal enum MeshStatusCode : int
        {
            STATUS_CODE_UNSPECIFIED = 0,
            STATUS_CODE_SUCCESS = 1,
            STATUS_CODE_NODE_NOT_FOUND = 2,
            STATUS_CODE_MESH_NOT_FOUND = 3,
        }

        // GetMesh and GetMeshUrl request responses

        [Serializable]
        internal class GetMeshUrlRequest
        {
            public string[] nodeIdentifiers;
            public int meshAlgorithm;
            public string arCommonMetadata;
            public string requestIdentifier;
        }

        [Serializable]
        internal class GetMeshUrlResponse
        {
            public NodeMeshData[] nodeMeshData;
            public string requestIdentifier;
            public string statusCode;
        }

        // GetGraph and GetNodeSpace request responses

        [Serializable]
        internal class NodeMeshData
        {
            public string nodeId;
            public MeshAlgorithm meshAlgorithm;
            public string url;
            public string textureUrl;
            public MeshStatusCode statusCode;
        }

        [Serializable]
        internal class VPSLocalToSpaceTransform
        {
            public Vector3 translation;
            public Vector4 rotation;
            public float scale;

            public VPSLocalToSpaceTransform()
            {
                translation = new Vector3();
                rotation = new Vector4();
            }
        };

        [Serializable]
        internal class TargetGraphNode {
            public string nodeId;
            public bool restrictResultsToNodeSpace;
        }

        [Serializable]
        internal class GetGraphRequest {
            public string requestIdentifier;
            public TargetGraphNode targetGraphNode;
            public string nodeIdentifier;
            public UInt32 radius;
            public UInt32 maxNodes; // TODO: Figure out a more effective way of capping data returned
        }

        [Serializable]
        internal class GetGraphResponse {
            public string requestIdentifier;
            public Node[] nodes;
            public Edge[] edges;
            public GraphStatusCode statusCode;
            public string targetNodeId;
        }

        [Serializable]
        internal class GetSpaceDataRequest {
            public string requestIdentifier;
            public string[] spaceIdentifiers;
        }

        [Serializable]
        internal class GetSpaceDataResponse {
            public string requestIdentifier;
            public GraphStatusCode statusCode;
            public SpaceData[] spaceDataList;
        }

        [Serializable]
        internal class SpaceData
        {
            public string spaceIdentifier;
            public double spaceQualityScore;
            public SpaceLabel[] spaceLabels;
        }

        [Serializable]
        internal class SpaceLabel
        {
            public string spaceIdentifier;
            public string label;
            public double totalArea;
            public long countObjects;
            public double[] polygonVerticesWrtOrigin;
            public float floatValue;
        }

        [Serializable]
        internal class Node {
            public string identifier;
            public UInt32 version;
            public string spaceIdentifier;
            public bool inDefaultSpace;
        }

        [Serializable]
        internal class Edge {
            public string source;
            public string destination;
            public VPSLocalToSpaceTransform sourceToDestination;   // transforms point in a’s coordinates to point in b’s coordinates
        }

        internal enum GraphStatusCode : int
        {
            STATUS_CODE_UNSPECIFIED = 0,
            STATUS_CODE_SUCCESS = 1,
            STATUS_CODE_FAIL = 2, // TODO: Expand on failure possibilities
            STATUS_CODE_LIMITED = 3,
            STATUS_CODE_NOT_FOUND = 4,
            STATUS_CODE_PERMISSION_DENIED = 5,
            STATUS_CODE_INVALID_ARGUMENT = 6,
            STATUS_CODE_INTERNAL = 7,
        }
    }
}
