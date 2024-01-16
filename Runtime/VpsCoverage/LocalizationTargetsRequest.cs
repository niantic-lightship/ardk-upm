// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Settings;
using UnityEngine;

namespace Niantic.Lightship.AR.VpsCoverage
{
    [Serializable]
    internal class LocalizationTargetsRequest
    {
        [SerializeField]
        private string[] query_id;

        [SerializeField]
        internal LegacyMetadataHelper.ARCommonMetadataStruct ar_common_metadata;

        public LocalizationTargetsRequest(string[] queryId, LegacyMetadataHelper.ARCommonMetadataStruct arCommonMetadata)
        {
            query_id = queryId;
            ar_common_metadata = arCommonMetadata;
        }
    }
}
