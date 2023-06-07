// Copyright 2022 Niantic, Inc. All Rights Reserved.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR
{
    /// A real world target for localization using VPS.
    [Serializable]
    public struct LocalizationTarget
    {
        [SerializeField] private string _identifier;

        [SerializeField] private string _name;

        [SerializeField] private LatLng _center;

        [SerializeField] private string _imageURL;

        [SerializeField] private string _defaultAnchor;

        internal LocalizationTarget(_LocalizationTargetsResponse.VpsLocalizationTarget vpsLocalizationTarget) : this(
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
        public string Identifier => _identifier;

        /// Geolocation of the LocalizationTarget.
        public LatLng Center => _center;

        /// Name of the LocalizationTarget.
        public string Name => _name;

        /// Url where hint image is stored.
        public string ImageURL => _imageURL;

        /// Default anchor.
        public string DefaultAnchor => _defaultAnchor;
    }
}
