// Copyright 2022 - 2024 Niantic.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems
{
    // Internal data holder

    [Serializable]
    internal class NodeToLoad
    {
        // Identifier of the node to load
        public string nodeId;

        // Identifier of the space the node belongs to
        public string spaceId;

        // Position of the node in the space, in RDF coordinate space
        public Vector3 position;

        // Rotation of the node in the space, in RDF coordinate space
        public Quaternion rotation;

        // URL of the mesh to download, contains a DRC compressed mesh
        public string meshUrl;

        // URL of the texture to download
        public string textureUrl;

        public bool isOrigin;

        public NodeToLoad()
        {
        }

        public NodeToLoad(string nodeId, string spaceId, Vector3 t, Vector4 r, bool isOrigin = false)
        {
            this.nodeId = nodeId;
            this.spaceId = spaceId;
            this.position = new Vector3(t.x, t.y, t.z);
            this.rotation = new Quaternion(r.x, r.y, r.z, r.w);
            this.isOrigin = isOrigin;
        }
    }
}
