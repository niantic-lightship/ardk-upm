// Copyright 2022-2024 Niantic.
using Niantic.Lightship.AR.PersistentAnchors;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.VpsCoverage;

using UnityEngine;

namespace Niantic.Lightship.AR.LocationAR
{
    /// <summary>
    /// The ARLocation is the digital twin of the physical location
    /// </summary>
    [PublicAPI("apiref/Niantic/Lightship/AR/LocationAR/ARLocation")]
    public class ARLocation : MonoBehaviour
    {
        /// <summary>
        /// The payload associated with the ARLocation
        /// </summary>
        public ARPersistentAnchorPayload Payload;

        [SerializeField]
        internal GameObject MeshContainer;

        [SerializeField]
        internal bool IncludeMeshInBuild;

        [SerializeField]
        internal LatLng GpsLocation;

#if UNITY_EDITOR
        [SerializeField]
        internal ARLocationManifest ARLocationManifest;

        [SerializeField]
        internal string AssetGuid;
#endif
    }
}
