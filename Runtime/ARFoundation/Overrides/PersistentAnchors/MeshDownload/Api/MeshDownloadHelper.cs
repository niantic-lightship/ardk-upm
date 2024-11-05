// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.PersistentAnchors.Spaces;
using Niantic.Lightship.AR.VpsCoverage;

using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems
{
    // Static APIs for downloading meshes from the meshing service
    internal static class MeshDownloadHelper
    {
        // Hardcode default if LightshipUnityContext is not available
        private static string prodVpsEndpoint = "https://vps-frontend.nianticlabs.com/web";
        private static string configEndpointFormatter = "{0}/vps_frontend.protogen.Localizer/{1}";
        private static string meshingMethod = "GetMeshUrl";
        private static string graphMethod = "GetGraph";
        private static string spaceDataMethod = "GetSpaceData";
        private const int KB = 1024;

        // Downloads a mesh from a signed url
        // The resulting byte[] can be fed into a draco mesh loader
        public static async Task<byte[]> DownloadMeshFromSignedUrl
        (
            string url
        )
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            using var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                return request.downloadHandler.data;
            }
            else
            {
                // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                Debug.LogError("Failed to download mesh");
                return null;
            }
        }

        // Returns a list of nodes to load, with mesh urls populated
        // If maxDownloadSizeKb is set, the total download size will be checked before downloading any meshes,
        //  and no download will be started if the total download size exceeds maxDownloadSizeKb
        // Method summary:
        // 1. Get nodes in space as a List<NodeToLoad>
        // 2. Get mesh urls for nodes as a Dictionary<string, string>, where the key is the nodeId and the value is the mesh url
        // 3. Populate the mesh urls in the List<NodeToLoad> from 1
        // 4. If maxDownloadSizeKb is set, check the total download size of the meshes
        // 5. Return the List<NodeToLoad> with node transforms and mesh urls
        public static async Task<List<NodeToLoad>> GetMeshUrlsForNode
        (
            string nodeId,
            ulong maxDownloadSizeKb = 0,
            MeshDownloadRequestResponse.MeshAlgorithm meshFormat =
                MeshDownloadRequestResponse.MeshAlgorithm.VERTEX_COLORED,
            CancellationToken cancellationToken = default
        )
        {
            var apiKey = LightshipSettingsHelper.ActiveSettings.ApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                Debug.LogError("No API key set");
                return null;
            }

            var authHeaderDict = new Dictionary<string, string>();
            authHeaderDict["Authorization"] = apiKey;

            // Get all of the nodes in the space of the target node
            // This will populate all of the nodes' transforms relative to the space
            var nodesToLoad = await GetNodesInSpace(nodeId, authHeaderDict);
            if (cancellationToken.IsCancellationRequested)
            {
                // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                Debug.Log("Mesh Download cancelled after getting nodes in space");
                return null;
            }

            if (nodesToLoad == null)
            {
                // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                Debug.LogError("Failed to get nodes to load");
                return null;
            }

            // Temporary list of nodeIds to get mesh urls for
            var nodeIdList = new List<string>();
            foreach (var node in nodesToLoad)
            {
                if (!string.IsNullOrWhiteSpace(node.nodeId))
                {
                    nodeIdList.Add(node.nodeId);
                }
            }

            // Get the mesh urls for the nodes in the space
            var meshUrls = await GetMeshUrlsForNodes(nodeIdList, authHeaderDict, meshFormat);
            if (cancellationToken.IsCancellationRequested)
            {
                // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                Debug.Log("Mesh Download cancelled after getting mesh urls");
                return null;
            }

            if (meshUrls == null)
            {
                return null;
            }

            var listOfNodesWithMeshes = new List<NodeToLoad>();
            // Populate the mesh urls in the nodes to load
            foreach (var node in nodesToLoad)
            {
                if (meshUrls.TryGetValue(node.nodeId, out NodeToLoad nodeUrls))
                {
                    node.meshUrl = nodeUrls.meshUrl;
                    node.textureUrl = nodeUrls.textureUrl;
                    listOfNodesWithMeshes.Add(node);
                }
            }

            // Check the total download size of the meshes
            // If maxDownloadSizeKb is set, the total download size will be checked before downloading any meshes,
            //  and no download will be started if the total download size exceeds maxDownloadSizeKb
            var totalDownloadSize = await GetTotalDownloadSize(listOfNodesWithMeshes);
            if (maxDownloadSizeKb != 0 && totalDownloadSize > maxDownloadSizeKb * KB)
            {
                // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                Debug.LogError("Total download size exceeds max download size");
                return null;
            }

            return listOfNodesWithMeshes;
        }

        // Returns a dictionary of nodeIds to mesh urls
        // If a node has no mesh, it will not be included in the dictionary
        // This is a helper method for GetMeshUrlsForNode
        private static async Task<Dictionary<string, NodeToLoad>> GetMeshUrlsForNodes
        (
            List<string> nodeIds,
            Dictionary<string, string> headers,
            MeshDownloadRequestResponse.MeshAlgorithm meshFormat =
                MeshDownloadRequestResponse.MeshAlgorithm.VERTEX_COLORED
        )
        {
            MeshDownloadRequestResponse.GetMeshUrlRequest request = new MeshDownloadRequestResponse.GetMeshUrlRequest();
            var meshUrls = new Dictionary<string, NodeToLoad>();
            request.nodeIdentifiers = nodeIds.ToArray();
            request.meshAlgorithm = (int)meshFormat;
            request.requestIdentifier = Guid.NewGuid().ToString("N").ToUpper();

            var endpoint = GetUrlForMethod(meshingMethod);
            var response =
                await HttpClient.SendPostAsync<
                    MeshDownloadRequestResponse.GetMeshUrlRequest,
                    MeshDownloadRequestResponse.GetMeshUrlResponse>
                (
                    endpoint,
                    request,
                    headers
                );

            if (response.Status == ResponseStatus.Success)
            {
                var isTexturedMesh =
                    meshFormat == MeshDownloadRequestResponse.MeshAlgorithm.TEXTURED;

                // Populate the mesh urls in the dictionary
                // If a node has no mesh, it will not be included in the output
                foreach (var node in response.Data.nodeMeshData)
                {
                    if (string.IsNullOrWhiteSpace(node.url))
                    {
                        continue;
                    }

                    // If the mesh is textured, but the texture url is empty, skip the node
                    if (isTexturedMesh && string.IsNullOrWhiteSpace(node.textureUrl))
                    {
                        continue;
                    }

                    // Only populate the textureUrl if the mesh is textured
                    var nodeToLoad = new NodeToLoad()
                    {
                        nodeId = node.nodeId,
                        meshUrl = node.url,
                        textureUrl = isTexturedMesh ? node.textureUrl : null
                    };

                    meshUrls.Add(node.nodeId, nodeToLoad);
                }
            }
            else
            {
                // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                Debug.LogError("Failed to get mesh urls");
            }

            return meshUrls;
        }

        // Returns a list of nodes to load, with transforms populated
        // This is a helper method for GetMeshUrlsForNode
        // The mesh urls will be populated by GetMeshUrlsForNodes
        // Method summary:
        // 1. Get all of the nodes in the space of the target node
        // 2. Populate a temporary list of nodes in the space of the target node
        // 3. Get the edges in the space of the target node
        // 4. Add an entry for the each node and edge pair to the return list
        // 6. Add the last remaining node with no edge (origin of the space)
        // 7. Return the List<NodeToLoad> with node transforms and mesh urls
        private static async Task<List<NodeToLoad>> GetNodesInSpace(string nodeId, Dictionary<string, string> headers)
        {
            var nodesToLoad = new List<NodeToLoad>();

            // Get all of the nodes in the space of the target node
            var targetGraphNode = new MeshDownloadRequestResponse.TargetGraphNode
            {
                nodeId = nodeId,
                restrictResultsToNodeSpace = true
            };

            MeshDownloadRequestResponse.GetGraphRequest graphRequest =
                new MeshDownloadRequestResponse.GetGraphRequest
                {
                    requestIdentifier = Guid.NewGuid().ToString("N").ToUpper(),
                    targetGraphNode = targetGraphNode
                };

            var endpoint = GetUrlForMethod(graphMethod);
            var nodesToLoadResponse =
                await HttpClient.SendPostAsync<
                    MeshDownloadRequestResponse.GetGraphRequest,
                    MeshDownloadRequestResponse.GetGraphResponse>
                (
                    endpoint,
                    graphRequest,
                    headers
                );

            if (nodesToLoadResponse.Status == ResponseStatus.Success )
            {
                var response = nodesToLoadResponse.Data;

                // Validate that the response is valid
                if (response.targetNodeId != nodeId ||
                    response.nodes == null ||
                    response.nodes.Length == 0)
                {
                    // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                    Debug.LogWarning("Failed to get nodes to load");
                    return null;
                }

                // Get the space of the target node
                String space = "";

                // Populate a temporary list of nodes in the space of the target node
                var nodesInSpaceList = new List<string>();
                foreach (var node in response.nodes)
                {
                    if (node.identifier.Equals(nodeId))
                    {
                        space = node.spaceIdentifier;
                    }
                }

                // If the target node has no space, return the target node as a single node to load
                if (string.IsNullOrWhiteSpace(space))
                {
                    nodesToLoad.Add
                    (
                        new NodeToLoad
                        (
                            nodeId,
                            space,
                            Vector3.zero,
                            new Vector4(0, 0, 0, 1),
                            true
                        )
                    );

                    return nodesToLoad;
                }

                // Filtering code to validate that nodes and edges are as expected

                // Get the nodes in the space of the target node
                // This populates a temporary list of nodeIds to find edges for
                // If a node does not have an associated edge, assume it is the origin of the space
                foreach (var node in response.nodes)
                {
                    if (node.spaceIdentifier.Equals(space))
                    {
                        nodesInSpaceList.Add(node.identifier);
                    }
                }

                // Get the edges in the space of the target node
                foreach (var edge in response.edges)
                {
                    // If the edge is not in the space of the target node, skip it
                    if (!nodesInSpaceList.Contains(edge.source))
                    {
                        continue;
                    }

                    // Add an entry for the node and edge to the return list
                    nodesToLoad.Add
                    (
                        new NodeToLoad
                        (
                            edge.source,
                            space,
                            edge.sourceToDestination.translation,
                            edge.sourceToDestination.rotation
                        )
                    );

                    // Remove the node from the temporary list of nodes in the space
                    nodesInSpaceList.Remove(edge.source);
                }

                // Add the last remaining node (origin of the space)
                if (nodesInSpaceList.Count == 1)
                {
                    nodesToLoad.Add
                    (
                        new NodeToLoad
                        (
                            nodesInSpaceList.First(),
                            space,
                            Vector3.zero,
                            new Vector4(0, 0, 0, 1),
                            true
                        )
                    );
                }
                else
                {
                    // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                    // Just log for now, not sure if this is possible
                    Debug.LogError("Multiple nodes in space have no associated edge");
                }
            }
            else
            {
                // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                Debug.LogError("Failed to get nodes to load");
            }

            return nodesToLoad;
        }

        // Use a HEAD request to get the total download size of the meshes
        private static async Task<ulong> GetTotalDownloadSize
        (
            List<NodeToLoad> nodes
        )
        {
            ulong totalBytesToDownload = 0;
            foreach (var node in nodes)
            {
                using var headerRequest = new UnityWebRequest(node.meshUrl, UnityWebRequest.kHttpVerbHEAD);
                await headerRequest.SendWebRequest();
                if (headerRequest.result == UnityWebRequest.Result.Success)
                {
                    string size = headerRequest.GetResponseHeader("Content-Length");
                    totalBytesToDownload += ulong.TryParse(size, out var sizeLong) ? sizeLong : 0;
                }

                if (string.IsNullOrWhiteSpace(node.textureUrl))
                {
                    continue;
                }

                using var textureHeaderRequest = new UnityWebRequest(node.textureUrl, UnityWebRequest.kHttpVerbHEAD);
                await textureHeaderRequest.SendWebRequest();
                if (textureHeaderRequest.result == UnityWebRequest.Result.Success)
                {
                    string size = textureHeaderRequest.GetResponseHeader("Content-Length");
                    totalBytesToDownload += ulong.TryParse(size, out var sizeLong) ? sizeLong : 0;
                }
            }

            return totalBytesToDownload;
        }

        internal static async Task<LightshipVpsSpaceResponse> GetSpaceDataForNode(string nodeId)
        {
            var apiKey = LightshipSettingsHelper.ActiveSettings.ApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
                Debug.LogError("No API key set");
            }

            var authHeaderDict = new Dictionary<string, string>();
            authHeaderDict["Authorization"] = apiKey;

            var nodeRepresentation = await GetNodesInSpace(nodeId, authHeaderDict);
            if (nodeRepresentation == null || nodeRepresentation.Count == 0)
            {
                return default;
            }

            var spaceId = nodeRepresentation.First().spaceId;

            var request = new MeshDownloadRequestResponse.GetSpaceDataRequest()
            {
                spaceIdentifiers = new []{spaceId},
                requestIdentifier = Guid.NewGuid().ToString("N").ToUpper()
            };

            var endpoint = GetUrlForMethod(spaceDataMethod);
            var response =
                await HttpClient
                    .SendPostAsync<MeshDownloadRequestResponse.GetSpaceDataRequest,
                            MeshDownloadRequestResponse.GetSpaceDataResponse>
                        (endpoint, request, authHeaderDict);

            if (response.Status == ResponseStatus.Success)
            {
                if (response.Data.spaceDataList.Length != 1)
                {
                    Debug.LogError($"GetSpaceData expected 1 space, but got {response.Data.spaceDataList.Length}. Using first space.");
                }

                var spaceData = new LightshipVpsSpace();
                spaceData.Nodes = new List<LightshipVpsNode>();
                spaceData.SpaceId = spaceId;
                foreach (var node in nodeRepresentation)
                {
                    if (node.spaceId != spaceId)
                    {
                        Debug.LogError($"Node {node.nodeId} is not in space {spaceId}");
                        return default;
                    }

                    var lightshipNode = new LightshipVpsNode
                    {
                        NodeId = node.nodeId,
                        NodeToSpaceOriginPose = new Pose(node.position, node.rotation),
                        IsOrigin = node.isOrigin
                    };

                    if (lightshipNode.IsOrigin)
                    {
                        spaceData.OriginNodeId = lightshipNode.NodeId;
                    }

                    spaceData.Nodes.Add(lightshipNode);
                }

                spaceData.SpaceLabels = response.Data.spaceDataList.First().spaceLabels.Select(label => new LightshipVpsSpace.LightshipVpsSpaceLabel(label)).ToList();
                spaceData.SpaceQualityScore = response.Data.spaceDataList.First().spaceQualityScore;
                var res = new LightshipVpsSpaceResponse(true, spaceData);
                return res;
            }

            // This class uses Debug instead of ARLog to support editor logging without Lightship Native loaded
            Debug.LogError("Failed to get space data");
            return default;
        }

        private static string GetUrlForMethod(string method)
        {
            // Default
            var configEndpoint = prodVpsEndpoint;

            var settings = LightshipSettingsHelper.ActiveSettings.EndpointSettings;
            if (!string.IsNullOrWhiteSpace(settings.VpsEndpoint))
            {
                configEndpoint = settings.VpsEndpoint;
            }

            return string.Format(configEndpointFormatter, configEndpoint, method);
        }
    }
}
