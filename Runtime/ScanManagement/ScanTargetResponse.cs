using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR;
using UnityEngine;

namespace Niantic.ARDK.AR.Scanning
{
    [Serializable]
    internal class ScanTargetResponse
    {
        public ScanTargetData[] scan_targets;
        public string status;
    }

    [Serializable]
    internal class ScanTargetData
    {
        public string id;
        public Shape shape;
        public string name;
        public string image_url;
        public string vps_status;
    }

    [Serializable]
    internal struct Shape
    {
        public LatLng point;

        public Shape(LatLng point)
        {
            this.point = point;
        }
    }
}
