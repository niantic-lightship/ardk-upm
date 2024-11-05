// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using System.Threading.Tasks;

using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;

using UnityEngine;

namespace Niantic.Lightship.AR.PersistentAnchors.Spaces
{
    /// <summary>
    /// Response object for LightshipVpsSpace.GetSpaceDataForNode, contains whether the request was
    /// successful and the space data for the node
    ///
    /// @note This is an experimental feature. Experimental features should not be used in
    /// production products as they are subject to breaking changes, not officially supported, and
    /// may be deprecated without notice
    /// </summary>
    [Experimental]
    public struct LightshipVpsSpaceResponse
    {
        [Experimental]
        public bool Success { get; internal set; }

        [Experimental]
        public LightshipVpsSpace Space { get; internal set; }

        public LightshipVpsSpaceResponse(bool success, LightshipVpsSpace space)
        {
            Success = success;
            Space = space;
        }
    }

    /// <summary>
    /// Represents a space in the Lightship VPS system. A space is a collection of nodes that are
    ///   connected through a series of edges. Each space has a origin node, which is the node that
    ///   is used as the reference point for the space.
    /// Also contains metadata about the space, such as labels and quality score.
    ///
    /// @note This is an experimental feature. Experimental features should not be used in
    /// production products as they are subject to breaking changes, not officially supported, and
    /// may be deprecated without notice
    /// </summary>
    [Experimental]
    public struct LightshipVpsSpace
    {
        public readonly struct LightshipVpsSpaceLabel
        {
            public const string BoundsLabel = "mesh_boundary";

            public readonly string SpaceIdentifier;
            public readonly string Label;
            public readonly double TotalArea;
            public readonly long CountObjects;
            public readonly double[] PolygonVerticesWrtOrigin;
            public readonly float FloatValue;

            public LightshipVpsSpaceLabel(string spaceIdentifier, string label, double totalArea, long countObjects, double[] polygonVerticesWrtOrigin, float floatValue)
            {
                SpaceIdentifier = spaceIdentifier;
                Label = label;
                TotalArea = totalArea;
                CountObjects = countObjects;
                PolygonVerticesWrtOrigin = polygonVerticesWrtOrigin;
                FloatValue = floatValue;
            }

            internal LightshipVpsSpaceLabel(MeshDownloadRequestResponse.SpaceLabel spaceLabel)
            {
                SpaceIdentifier = spaceLabel.spaceIdentifier;
                Label = spaceLabel.label;
                TotalArea = spaceLabel.totalArea;
                CountObjects = spaceLabel.countObjects;
                PolygonVerticesWrtOrigin = spaceLabel.polygonVerticesWrtOrigin;
                FloatValue = spaceLabel.floatValue;
            }
        }

        /// <summary>
        /// Id of the space, will be consistent across all nodes in the space
        ///
        /// @note This is an experimental feature. Experimental features should not be used in
        /// production products as they are subject to breaking changes, not officially supported, and
        /// may be deprecated without notice
        /// </summary>
        [Experimental]
        public string SpaceId { get; internal set; }

        /// <summary>
        /// Id of the origin node of the space
        ///
        /// @note This is an experimental feature. Experimental features should not be used in
        /// production products as they are subject to breaking changes, not officially supported, and
        /// may be deprecated without notice
        /// </summary>
        [Experimental]
        public string OriginNodeId { get; internal set; }

        /// <summary>
        /// List of nodes in the space
        ///
        /// @note This is an experimental feature. Experimental features should not be used in
        /// production products as they are subject to breaking changes, not officially supported, and
        /// may be deprecated without notice
        /// </summary>
        [Experimental]
        public List<LightshipVpsNode> Nodes { get; internal set; }

        /// <summary>
        /// List of space labels in the space. This contains metadata about the space, such as the
        ///  total area of the space, and various physical features.
        ///
        /// @note This is an experimental feature. Experimental features should not be used in
        /// production products as they are subject to breaking changes, not officially supported, and
        /// may be deprecated without notice
        /// </summary>
        [Experimental]
        public List<LightshipVpsSpaceLabel> SpaceLabels { get; internal set; }

        /// <summary>
        /// Quality score of the space. Generally, higher scores indicate better quality spaces.
        ///
        /// @note This is an experimental feature. Experimental features should not be used in
        /// production products as they are subject to breaking changes, not officially supported, and
        /// may be deprecated without notice
        /// </summary>
        [Experimental]
        public double SpaceQualityScore { get; internal set; }

        /// <summary>
        /// Get the transform from the specified node to the origin of the space
        ///
        /// @note This is an experimental feature. Experimental features should not be used in
        /// production products as they are subject to breaking changes, not officially supported, and
        /// may be deprecated without notice
        /// </summary>
        [Experimental]
        public readonly Pose NodeToSpaceOrigin(string nodeId)
        {
            foreach (var node in Nodes)
            {
                if (node.NodeId.Equals(nodeId))
                {
                    return node.NodeToSpaceOriginPose;
                }
            }

            return Pose.identity;
        }

        /// <summary>
        /// Determine if the specified node is in this space
        ///
        /// @note This is an experimental feature. Experimental features should not be used in
        /// production products as they are subject to breaking changes, not officially supported, and
        /// may be deprecated without notice
        /// </summary>
        [Experimental]
        public readonly bool IsNodeInSpace(string nodeId)
        {
            foreach (var node in Nodes)
            {
                if (node.NodeId.Equals(nodeId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get the mesh bounds of the space, relative to the origin of the space. The bounds are
        /// 4 vertices that draw a quad around the space.
        ///
        /// @note This is an experimental feature. Experimental features should not be used in
        /// production products as they are subject to breaking changes, not officially supported, and
        /// may be deprecated without notice
        /// </summary>
        [Experimental]
        public readonly List<Pose> GetMeshBoundsRelativeToOrigin()
        {
            var bounds = new List<Pose>();
            if(SpaceLabels == null)
            {
                return bounds;
            }

            foreach (var label in SpaceLabels)
            {
                if (label.Label.Equals(LightshipVpsSpaceLabel.BoundsLabel))
                {
                    // Check that there are n*3 entries
                    if (label.PolygonVerticesWrtOrigin.Length % 3 != 0)
                    {
                        Debug.LogError("Invalid number of vertices for mesh boundary");
                        continue;
                    }

                    for (int i = 0; i < label.PolygonVerticesWrtOrigin.Length; i += 3)
                    {
                        var position = new Vector3(
                            (float)label.PolygonVerticesWrtOrigin[i],
                            (float)label.PolygonVerticesWrtOrigin[i + 1],
                            (float)label.PolygonVerticesWrtOrigin[i + 2]);
                        bounds.Add(new Pose(position, Quaternion.identity));
                    }
                }
            }

            return bounds;
        }

        /// <summary>
        /// Asynchronously get the space data for the specified node. This will return a
        /// struct containing the space data for the node, if the request was successful.
        ///
        /// @note This is an experimental feature. Experimental features should not be used in
        /// production products as they are subject to breaking changes, not officially supported, and
        /// may be deprecated without notice
        /// </summary>
        [Experimental]
        public static async Task<LightshipVpsSpaceResponse> GetSpaceDataForNode(string nodeId)
        {
            return await MeshDownloadHelper.GetSpaceDataForNode(nodeId);
        }
    }
}
