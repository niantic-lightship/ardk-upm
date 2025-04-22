// Copyright 2022-2025 Niantic.

using System;
using System.Buffers;
using System.Linq;

using Niantic.Protobuf;
using ManagedPoses;

using MathTypes;

using Niantic.Lightship.AR.PersistentAnchors;
using Niantic.Lightship.AR.Utilities;

using UnityEngine;
using UnityEngine.XR.ARSubsystems;

using Quaternion = UnityEngine.Quaternion;
using Transform = MathTypes.Transform;
using Vector3 = UnityEngine.Vector3;

namespace Niantic.Lightship.AR.Subsystems
{
    public static class ARPersistentAnchorDeserializationUtility
    {
        public static string GetOriginNodeIdAssociatedWithPayload(string payload)
        {
            var mpData = DeserializeAnchorToManagedPoseData(payload);

            UUID assoc = default;
            if (mpData.NodeAssociations.Count == 0)
            {
                return null;
            }
            else if (mpData.NodeAssociations.Count > 1)
            {
                foreach (var nodeAssociation in mpData.NodeAssociations)
                {
                    if (!IsNodeAssociationApproxOrigin(nodeAssociation))
                    {
                        continue;
                    }

                    assoc = nodeAssociation.Identifier;
                    break;
                }
            }
            else
            {
                assoc = mpData.NodeAssociations.First().Identifier;
            }

            if (assoc == default)
            {
                return null;
            }

            return new TrackableId(assoc.Upper, assoc.Lower).ToLightshipHexString();
        }

        public static string GetOriginNodeIdAssociatedWithPayload(ARPersistentAnchorPayload payload)
        {
            return GetOriginNodeIdAssociatedWithPayload(payload.ToBase64());
        }

        public static ManagedPoseData DeserializeAnchorToManagedPoseData(string payload)
        {
            var bytes = new Span<byte>(new byte[payload.Length]);
            bool valid = Convert.TryFromBase64String(payload, bytes, out int bytesWritten);
            if (!valid)
            {
                return null;
            }

            return ManagedPoseData.Parser.ParseFrom(bytes[..bytesWritten].ToArray());
        }

        public static ManagedPoseData DeserializeAnchorToManagedPoseData(ARPersistentAnchorPayload payload)
        {
            return ManagedPoseData.Parser.ParseFrom(payload.Data);
        }

        public static string DeserializeAnchorToJson(ARPersistentAnchorPayload payload)
        {
            var proto = ManagedPoseData.Parser.ParseFrom(payload.Data);

            if (proto == null)
            {
                return null;
            }

            return JsonFormatter.ToDiagnosticString(proto);
        }

        public static string DeserializeAnchorToJson(string payload)
        {
            var bytes = new Span<byte>(new byte[payload.Length]);
            bool valid = System.Convert.TryFromBase64String(payload, bytes, out int bytesWritten);
            if (!valid)
            {
                return null;
            }

            var proto = ManagedPoseData.Parser.ParseFrom(bytes[..bytesWritten].ToArray());

            if (proto == null)
            {
                return null;
            }

            return JsonFormatter.ToDiagnosticString(proto);
        }

        public static Vector3 GetTranslation(this MathTypes.Vector3 vec)
        {
            return new Vector3(vec.X, vec.Y, vec.Z);
        }

        public static Quaternion GetRotation(this MathTypes.Quaternion quat)
        {
            return new Quaternion(quat.X, quat.Y, quat.Z, quat.W);
        }

        public static string CreateAnchorPayloadFromNodeId(string nodeId)
        {
            if (!TrackableIdExtension.LightshipHexStringToUlongs
                (
                    nodeId,
                    out var upper,
                    out var lower
                ))
            {
                return null;
            }

            var uuid = new UUID
            {
                Upper = upper,
                Lower = lower
            };

            var nodeAssociation = new NodeAssociation
            {
                Identifier = uuid,
                ManagedPoseToNode = new Transform()
                {
                    Translation = new MathTypes.Vector3(),
                    Rotation = new MathTypes.Quaternion
                    {
                        W = 1
                    },
                },
                Weight = 1
            };

            var anchorGuid = Guid.NewGuid().ToString("N");
            if (!TrackableIdExtension.LightshipHexStringToUlongs
                (
                    anchorGuid,
                    out var anchorUpper,
                    out var anchorLower
                ))
            {
                return null;
            }

            var anchorUuid = new UUID
            {
                Upper = anchorUpper,
                Lower = anchorLower
            };

            var managedPoseData = new ManagedPoseData
            {
                Identifier = anchorUuid,
                NodeAssociations = { nodeAssociation },
                CreationTimeMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };

            // Serialize proto to byte
            return Convert.ToBase64String(managedPoseData.ToByteArray());
        }

        private static bool IsNodeAssociationApproxOrigin(NodeAssociation nodeAssociation)
        {
            var translation = nodeAssociation.ManagedPoseToNode.Translation;
            if(translation.X > 0.001f || translation.Y > 0.001f || translation.Z > 0.001f)
            {
                return false;
            }

            var rotation = nodeAssociation.ManagedPoseToNode.Rotation;
            if(rotation.X > 0.001f || rotation.Y > 0.001f || rotation.Z > 0.001f || rotation.W - 1 > 0.001f)
            {
                return false;
            }

            return true;
        }
    }
}
