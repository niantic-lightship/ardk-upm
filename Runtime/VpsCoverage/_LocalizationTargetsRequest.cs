// Copyright 2022 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.Lightship.AR.Settings.User;
using UnityEngine;

namespace Niantic.Lightship.AR
{
    [Serializable]
    internal class _LocalizationTargetsRequest
    {
        [SerializeField]
        private string[] query_id;

        [SerializeField]
        internal LegacyMetadataHelper.ARCommonMetadataStruct ar_common_metadata;

        public _LocalizationTargetsRequest(string[] queryId, LegacyMetadataHelper.ARCommonMetadataStruct arCommonMetadata)
        {
            query_id = queryId;
            ar_common_metadata = arCommonMetadata;
        }
    }
}
