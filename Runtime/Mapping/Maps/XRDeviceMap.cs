// Copyright 2022-2024 Niantic.

using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Mapping
{
    public readonly struct XRDeviceMap
    {
        private readonly TrackableId _nodeId;
        private readonly byte[] _data;

        public XRDeviceMap(TrackableId nodeId, byte[] data)
        {
            _nodeId = nodeId;
            _data = data;
        }

        public TrackableId GetNodeId()
        {
            return _nodeId;
        }

        public byte[] CopyData()
        {
            return _data;
        }
    }
}
