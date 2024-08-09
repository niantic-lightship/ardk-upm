// Copyright 2022-2024 Niantic.

namespace Niantic.Lightship.AR.MapStorageAccess
{
    public readonly struct MapSubGraph
    {
        private readonly byte[] _data;

        private readonly OutputEdgeType _edgeType;

        public MapSubGraph(byte[] data, OutputEdgeType edgeType = OutputEdgeType.All)
        {
            _data = data;
            _edgeType = edgeType;
        }

        public byte[] GetData()
        {
            return _data;
        }

        public OutputEdgeType GetEdgeType()
        {
            return _edgeType;
        }
    }
}
