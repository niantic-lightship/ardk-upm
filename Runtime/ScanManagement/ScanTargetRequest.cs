using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Loader;
using UnityEngine;

namespace Niantic.ARDK.AR.Scanning
{
    [Serializable]
    internal class ScanTargetRequest
    {
        [SerializeField] internal LatLng query_location;

        [SerializeField] internal int query_radius_in_meters;

        [SerializeField] internal int user_distance_to_query_location_in_meters;

        [SerializeField] internal ARCommonMetadataStruct ar_common_metadata;


        public ScanTargetRequest(LatLng queryLocation, int queryRadiusInMeters, int userDistanceToQueryLocationInMeters,
            ARCommonMetadataStruct arCommonMetadata) :
            this(queryLocation, queryRadiusInMeters, arCommonMetadata)
        {
            user_distance_to_query_location_in_meters = userDistanceToQueryLocationInMeters;
        }

        public ScanTargetRequest(LatLng queryLocation, int queryRadiusInMeters, ARCommonMetadataStruct arCommonMetadata)
        {
            query_location = queryLocation;
            query_radius_in_meters = queryRadiusInMeters;
            ar_common_metadata = arCommonMetadata;
        }
    }
}
