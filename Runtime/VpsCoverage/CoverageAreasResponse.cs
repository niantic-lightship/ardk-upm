// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.VpsCoverage
{
    [Serializable]
    internal class CoverageAreasResponse
    {
        public VpsCoverageArea[] vps_coverage_area;
        public string status;
        public string error_message;

        public CoverageAreasResponse(VpsCoverageArea[] vpsCoverageArea, string status, string errorMessage)
        {
            vps_coverage_area = vpsCoverageArea;
            this.status = status;
            error_message = errorMessage;
        }

        [Serializable]
        public struct VpsCoverageArea
        {
            public Shape shape;
            public string[] vps_localization_target_id;
            public string localizability;

            public VpsCoverageArea(Shape shape, string[] vpsLocalizationTargetId, string localizability)
            {
                vps_localization_target_id = vpsLocalizationTargetId;
                this.shape = shape;
                this.localizability = localizability;
            }
        }

        [Serializable]
        public struct Shape
        {
            public Polygon polygon;

            public Shape(Polygon polygon)
            {
                this.polygon = polygon;
            }

            public Shape(LatLng[] points)
            {
                var loop = new Loop(points);
                polygon = new Polygon(new[] { loop });
            }
        }

        [Serializable]
        public struct Polygon
        {
            public Loop[] loop;

            public Polygon(Loop[] loop)
            {
                this.loop = loop;
            }
        }

        [Serializable]
        public struct Loop
        {
            public LatLng[] vertex;

            public Loop(LatLng[] vertex)
            {
                this.vertex = vertex;
            }
        }
    }
}
