// Copyright 2022-2024 Niantic.

using System;
using System.Linq;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// <summary>
    /// The CoverageArea struct represents an area where localization with VPS is possible.
    /// Precisely, it is a calculated area representing a cluster of localization targets within
    /// a certain proximity of each other to help determine reliability of VPS tracking.
    /// </summary>
    [PublicAPI]
    [Serializable]
    public struct CoverageArea
    {
        /// The localizability quality is split into CoverageAreas above a quality threshold marked as
        /// PRODUCTION, and CoverageAreas that have a lower localizability quality marked as EXPERIMENTAL.
        public enum Localizability
        {
            UNSET,
            PRODUCTION,
            EXPERIMENTAL
        }

        [SerializeField] private string[] _localizationTargetIdentifiers;

        [SerializeField] private LatLng[] _shape;

        [SerializeField] private Localizability _localizabilityQuality;

        internal CoverageArea(CoverageAreasResponse.VpsCoverageArea location)
            : this(location.vps_localization_target_id, location.shape.polygon.loop[0].vertex, location.localizability)
        {
        }

        internal CoverageArea(string[] localizationTargetIdentifiers, LatLng[] shape, string localizability)
        {
            _localizationTargetIdentifiers = localizationTargetIdentifiers;
            _shape = shape;
            Centroid = CalculateCentroid(_shape);

            Enum.TryParse(localizability, out _localizabilityQuality);
        }

        /// Identifiers of all LocalizationTargets within the CoverageArea.
        public readonly string[] LocalizationTargetIdentifiers => _localizationTargetIdentifiers;

        /// Polygon outlining the CoverageArea.
        public readonly LatLng[] Shape => _shape;

        /// Centroid of the Shape polygon.
        public LatLng Centroid { get; private set; }

        /// The localizability quality gives information about the chances of a successful localization
        /// in this CoverageArea.
        public readonly Localizability LocalizabilityQuality => _localizabilityQuality;

        // taken from https://stackoverflow.com/questions/6671183/calculate-the-center-point-of-multiple-latitude-longitude-coordinate-pairs
        private static LatLng CalculateCentroid(params LatLng[] points)
        {
            if (points.Length == 1) return points.Single();

            double x = 0;
            double y = 0;
            double z = 0;

            foreach (var point in points)
            {
                var latitude = point.Latitude * Math.PI / 180;
                var longitude = point.Longitude * Math.PI / 180;

                x += Math.Cos(latitude) * Math.Cos(longitude);
                y += Math.Cos(latitude) * Math.Sin(longitude);
                z += Math.Sin(latitude);
            }

            var total = points.Length;

            x = x / total;
            y = y / total;
            z = z / total;

            var centralLongitude = Math.Atan2(y, x);
            var centralSquareRoot = Math.Sqrt(x * x + y * y);
            var centralLatitude = Math.Atan2(z, centralSquareRoot);

            return new LatLng(centralLatitude * 180 / Math.PI, centralLongitude * 180 / Math.PI);
        }
    }
}
