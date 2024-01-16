// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Settings;
using UnityEngine;

namespace Niantic.Lightship.AR.VpsCoverage
{
    [Serializable]
    internal class CoverageAreasRequest
    {
        [SerializeField]
        private LatLng query_location;

        [SerializeField]
        private int query_radius_in_meters;

        [SerializeField]
        private int user_distance_to_query_location_in_meters;

        [SerializeField]
        internal LegacyMetadataHelper.ARCommonMetadataStruct ar_common_metadata;

        public CoverageAreasRequest(LatLng queryLocation, int queryRadiusInMeters,
            int userDistanceToQueryLocationInMeters, LegacyMetadataHelper.ARCommonMetadataStruct arCommonMetadata) :
            this(queryLocation, queryRadiusInMeters, arCommonMetadata)
        {
            user_distance_to_query_location_in_meters = userDistanceToQueryLocationInMeters;
        }

        public CoverageAreasRequest(LatLng queryLocation, int queryRadiusInMeters,
            LegacyMetadataHelper.ARCommonMetadataStruct arCommonMetadata)
        {
            query_location = queryLocation;
            query_radius_in_meters = queryRadiusInMeters;
            ar_common_metadata = arCommonMetadata;
        }
    }
}
