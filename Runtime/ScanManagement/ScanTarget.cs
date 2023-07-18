using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR;
using UnityEngine;

namespace Niantic.ARDK.AR.Scanning
{
    /// Represents a location that can be scanned and activated for VPS.
    public struct ScanTarget
    {
        /// A unique identifier for this scan target.
        /// @note This identifier is not guaranteed to be stable across sessions.
        public string ScanTargetIdentifier;

        /// The shape of this ScanTarget, as a point or polygon. It is recommended to use the <see cref="Centroid"/>
        /// property to get a point representing the location of the scan target.
        public LatLng[] Shape;

        /// The name of this scan target.
        public string Name;

        /// The URL of an image depicting the scan target, or empty string if none exists.
        public string ImageUrl;

        /// A point representing the center of this scan target.
        public LatLng Centroid => Shape[0];

        /// The localizability status of this scan target. This indicates whether the scan target is currently
        /// activated for VPS.
        public ScanTargetLocalizabilityStatus LocalizabilityStatus;

        public enum ScanTargetLocalizabilityStatus
        {
            /// The localizability of the scan target is unknown.
            Unset,
            /// The scan target is activated as a VPS production wayspot and has a high chance of successful localization.
            Production,
            /// The scan target is activated as a VPS experimental wayspot and may have a lower chance of successful
            /// localization than a PRODUCTION scan target.
            Experimental,
            /// The scan target is not currently activated for VPS.
            Not_Activated
        }

        /// Downloads the image for this scan target, returning it as a Texture.
        public async void DownloadImage(Action<Texture> onImageDownloaded)
        {
            Texture image = await _HttpClient.DownloadImageAsync(ImageUrl);
            onImageDownloaded?.Invoke(image);
        }
    }

}
