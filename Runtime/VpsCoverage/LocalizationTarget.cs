// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// <summary>
    /// The LocalizationTarget struct represents a real world point of interest that used for VPS tracking.
    /// </summary>
    [PublicAPI]
    [Serializable]
    public struct LocalizationTarget
    {
        [SerializeField] private string _identifier;

        [SerializeField] private string _name;

        [SerializeField] private LatLng _center;

        [SerializeField] private string _imageURL;

        [SerializeField] private string _defaultAnchor;

        internal LocalizationTarget(LocalizationTargetsResponse.VpsLocalizationTarget vpsLocalizationTarget) : this(
            vpsLocalizationTarget.id,
            vpsLocalizationTarget.shape.point,
            vpsLocalizationTarget.name,
            vpsLocalizationTarget.image_url,
            vpsLocalizationTarget.default_anchor)
        {
        }

        internal LocalizationTarget(string identifier, LatLng center, string name, string imageUrl,
            string defaultAnchor)
        {
            _identifier = identifier;
            _center = center;
            _name = name;
            _imageURL = imageUrl;
            _defaultAnchor = defaultAnchor;
        }

        /// Unique identifier of the LocalizationTarget.
        public readonly string Identifier => _identifier;

        /// Geolocation of the LocalizationTarget.
        public readonly LatLng Center => _center;

        /// Name of the LocalizationTarget.
        public readonly string Name => _name;

        /// Url where hint image is stored.
        public readonly string ImageURL => _imageURL;

        /// Default anchor.
        public readonly string DefaultAnchor => _defaultAnchor;
    }
}
