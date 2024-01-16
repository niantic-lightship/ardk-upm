// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.VpsCoverage
{
    [Serializable]
    internal class LocalizationTargetsResponse
    {
        public VpsLocalizationTarget[] vps_localization_target;
        public string status;
        public string error_message;

        public LocalizationTargetsResponse
        (
            VpsLocalizationTarget[] vpsLocalizationTarget,
            string status,
            string errorMessage
        )
        {
            vps_localization_target = vpsLocalizationTarget;
            this.status = status;
            error_message = errorMessage;
        }

        [Serializable]
        public struct VpsLocalizationTarget
        {
            public string id;
            public Shape shape;
            public string name;
            public string image_url;
            public string default_anchor;

            public VpsLocalizationTarget(string id, Shape shape, string name, string image_url, string default_anchor)
            {
                this.id = id;
                this.shape = shape;
                this.name = name;
                this.image_url = image_url;
                this.default_anchor = default_anchor;
            }
        }

        [Serializable]
        public struct Shape
        {
            public LatLng point;

            public Shape(LatLng point)
            {
                this.point = point;
            }
        }
    }
}
