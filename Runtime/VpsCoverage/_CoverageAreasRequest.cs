// Copyright 2022 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.Lightship.AR.Loader;
using UnityEngine;

namespace Niantic.Lightship.AR
{
    [Serializable]
    internal class _CoverageAreasRequest
    {
        [SerializeField]
        private LatLng query_location;

        [SerializeField]
        private int query_radius_in_meters;

        [SerializeField]
        private int user_distance_to_query_location_in_meters;

        [SerializeField]
        internal ARCommonMetadataStruct ar_common_metadata;

        public _CoverageAreasRequest(LatLng queryLocation, int queryRadiusInMeters,
            int userDistanceToQueryLocationInMeters, ARCommonMetadataStruct arCommonMetadata) :
            this(queryLocation, queryRadiusInMeters, arCommonMetadata)
        {
            user_distance_to_query_location_in_meters = userDistanceToQueryLocationInMeters;
        }

        public _CoverageAreasRequest(LatLng queryLocation, int queryRadiusInMeters,
            ARCommonMetadataStruct arCommonMetadata)
        {
            query_location = queryLocation;
            query_radius_in_meters = queryRadiusInMeters;
            ar_common_metadata = arCommonMetadata;
        }
    }
}
