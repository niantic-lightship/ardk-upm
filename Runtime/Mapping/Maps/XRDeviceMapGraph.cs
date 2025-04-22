// Copyright 2022-2025 Niantic.

namespace Niantic.Lightship.AR.Mapping
{
    public readonly struct XRDeviceMapGraph
    {
        private readonly byte[] _data;

        public XRDeviceMapGraph(byte[] data)
        {
            _data = data;
        }

        public byte[] CopyData()
        {
            return _data;
        }
    }
}
