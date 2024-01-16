// Copyright 2022-2024 Niantic.

using System;

namespace Niantic.Lightship.AR.Settings
{
    internal class LegacyMetadataHelper
    {
        /// <summary>
        /// This method uses the C++ standard for the variable names. Please use Metadata.GetArCommonMetadata()
        /// </summary>
        /// <param name="requestId">requestId of the string</param>
        /// <returns>ArCommonMetadata in C++ format</returns>
        internal static ARCommonMetadataStruct GetCommonDataEnvelopeWithRequestIdAsStruct(string requestId)
        {
            var metadata = new ARCommonMetadataStruct
            (
                Metadata.ApplicationId,
                Metadata.Platform,
                Metadata.Manufacturer,
                Metadata.DeviceModel,
                Metadata.UserId,
                Metadata.ClientId,
                Metadata.Version,
                Metadata.AppInstanceId,
                requestId
            );
            return metadata;
        }

        [Serializable]
        internal struct ARCommonMetadataStruct
        {
            // DO NOT RENAME THESE VARIABLES EVEN WITH THE NEW STYLE
            public string application_id;
            public string platform;
            public string manufacturer;
            public string device_model;
            public string user_id;
            public string client_id;
            public string ardk_version;
            public string ardk_app_instance_id;
            public string request_id;

            public ARCommonMetadataStruct
            (
                string applicationID,
                string platform,
                string manufacturer,
                string deviceModel,
                string userID,
                string clientID,
                string ardkVersion,
                string ardkAppInstanceID,
                string requestID
            )
            {
                application_id = applicationID;
                this.platform = platform;
                this.manufacturer = manufacturer;
                device_model = deviceModel;
                user_id = userID;
                client_id = clientID;
                ardk_version = ardkVersion;
                ardk_app_instance_id = ardkAppInstanceID;
                request_id = requestID;
            }
        }
    }
}
