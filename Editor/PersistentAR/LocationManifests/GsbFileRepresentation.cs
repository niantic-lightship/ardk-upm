// Copyright 2022-2024 Niantic.

using System;

using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    internal static class GsbFileRepresentation
    {
        [Serializable]
        internal struct LocationData
        {
            public string NodeIdentifier;
            public string DefaultAnchorPayload;
            public string AnchorPayload;
            public string LocalizationTargetName;
            public string LocalizationTargetID;
            public EdgeRepresentation LocalToSpace;
            public GpsLocation GpsLocation;
        }

        [Serializable]
        internal struct MeshData
        {
            public Mesh GeneratedMesh;
            public string NodeIdentifier;
            public Vector3 TranslationToTarget;
            public Quaternion RotationToTarget;
            public Texture2D Texture;
        }

        [Serializable]
        internal struct NodeRepresentation
        {
            public string identifier;
        }

        [Serializable]
        internal struct EdgeRepresentation
        {
            public string source;
            public string destination;
            public TransformRepresentation sourceToDestination;
        }

        [Serializable]
        internal struct TransformRepresentation
        {
            public Translation translation;
            public Rotation rotation;
            public float scale;
        }

        [Serializable]
        internal struct Translation
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        internal struct Rotation
        {
            public float x;
            public float y;
            public float z;
            public float w;
        }

        [Serializable]
        internal struct GpsLocation
        {
            public float latitude;
            public float longitude;
        }
    }
}
