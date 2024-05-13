// Copyright 2022-2024 Niantic.

using System;
using System.Collections;
using System.Threading.Tasks;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// <summary>
    /// The CoverageClientManager component provides the ability to query VPS coverage area and localization target
    /// information within a specified radius from either a device's current location or a specified location.
    /// Additionally, private VPS-scans can also be provided to this manager to have them be included in the query
    /// result for testing purposes (as private VPS-scans are not currently included in the query response).
    /// </summary>
    [PublicAPI("apiref/Niantic/Lightship/AR/VpsCoverage/CoverageClientManager/")]
    public class CoverageClientManager : MonoBehaviour
    {
        [SerializeField] [Tooltip("Radial distance from query location when querying coverage")] [Range(0,2000)]
        private int _queryRadius = 1000;

#if UNITY_EDITOR
        [SerializeField] [Tooltip("Private scan assets to be considered in the coverage result")]
        private ARLocationManifest[] _privateARLocations;
#endif

        [SerializeField] [HideInInspector]
        private bool _useCurrentLocation = true;

        [SerializeField] [HideInInspector]
        private float _queryLatitude;

        [SerializeField] [HideInInspector]
        private float _queryLongitude;

        [SerializeField] [HideInInspector]
        private LocalizationTarget[] _privateARLocalizationTargets;

        private CoverageClient _coverageClient;

        /// <summary>
        /// Whether or not to use current location in the query
        /// </summary>
        public bool UseCurrentLocation
        {
            get => _useCurrentLocation;
            set
            {
                _useCurrentLocation = value;
            }
        }

        /// <summary>
        /// The radius of the query in meters
        /// </summary>
        public int QueryRadius
        {
            get => _queryRadius;
            set
            {
                _queryRadius = value;
            }
        }

        /// <summary>
        /// The latitude of the query
        /// </summary>
        public float QueryLatitude
        {
            get => _queryLatitude;
            set
            {
                _queryLatitude = value;
            }
        }

        /// <summary>
        /// The longitude of the query
        /// </summary>
        public float QueryLongitude
        {
            get => _queryLongitude;
            set
            {
                _queryLongitude = value;
            }
        }

        /// <summary>
        /// The localization targets for the private scans
        /// </summary>
        public LocalizationTarget[] PrivateARLocalizationTargets
        {
            get => _privateARLocalizationTargets;
            set
            {
                _privateARLocalizationTargets = value;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// The location manifests for the private scans
        /// </summary>
        public ARLocationManifest[] PrivateARLocations
        {
            get => _privateARLocations;
            set
            {
                _privateARLocations = value;
            }
        }
#endif

        private void Awake()
        {
            _coverageClient = CoverageClientFactory.Create();
        }

        /// <summary>
        /// Queries for coverage
        /// </summary>
        /// <param name="onTryGetCoverage">Callback after query completes</param>
        public void TryGetCoverage(Action<AreaTargetsResult> onTryGetCoverage)
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            if (_useCurrentLocation && !Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                var androidPermissionCallbacks = new PermissionCallbacks();
                androidPermissionCallbacks.PermissionGranted += permissionName =>
                {
                    if (permissionName == "android.permission.ACCESS_FINE_LOCATION")
                    {
                        TryGetCoverage(onTryGetCoverage);
                    }
                };

                Permission.RequestUserPermission(Permission.FineLocation, androidPermissionCallbacks);
                return;
            }
#endif
            if (UseCurrentLocation)
            {
                StartCoroutine(TryGetCoverageWithGrantedPermissions(onTryGetCoverage));
            }
            else
            {
                var inputLocation = new LatLng(QueryLatitude, QueryLongitude);
                _coverageClient.TryGetCoverage(inputLocation, QueryRadius, onTryGetCoverage, _privateARLocalizationTargets);
            }
        }

        private IEnumerator TryGetCoverageWithGrantedPermissions(Action<AreaTargetsResult> onTryGetCoverage)
        {
            if (!Input.location.isEnabledByUser)
            {
                yield break;
            }

            bool wasLocationServicesStopped = Input.location.status == LocationServiceStatus.Stopped;

            // only turn on location services if location services were not enabled in the first place
            if (wasLocationServicesStopped)
            {
                Input.location.Start(1);
            }

            // wait for location services to turn on
            while (Input.location.status != LocationServiceStatus.Running)
            {
                yield return new WaitForEndOfFrame();
            }

            var inputLocation = new LatLng(Input.location.lastData);

            // only turn off location services if location services were not enabled in the first place
            if (wasLocationServicesStopped)
            {
                Input.location.Stop();
            }

            _coverageClient.TryGetCoverage(inputLocation, QueryRadius, onTryGetCoverage, _privateARLocalizationTargets);
        }

        /// <summary>
        /// Tries to get a hint image from the URL
        /// </summary>
        /// <param name="imageUrl">The URL used to get the hint image</param>
        /// <returns>The texture with the hint image fetched from the URL</returns>
        public Task<Texture> TryGetImageFromUrl(string imageUrl) => _coverageClient.TryGetImageFromUrl(imageUrl);

        /// <summary>
        /// Tries to get a hint image from the URL
        /// </summary>
        /// <param name="imageUrl">The URL used to get the hint image</param>
        /// <param name="onImageDownloaded">Callback after the hint image is downloaded</param>
        public void TryGetImageFromUrl(string imageUrl, Action<Texture> onImageDownloaded)
        {
            _coverageClient.TryGetImageFromUrl(imageUrl, onImageDownloaded);
        }

        /// <summary>
        /// Tries to get a hint image from the URL
        /// </summary>
        /// <param name="imageUrl">The URL used to get the hint image</param>
        /// <param name="width">The requested width of the hint image</param>
        /// <param name="height">The requested height of the hint image</param>
        /// <returns>The texture with the hint image</returns>
        public Task<Texture> TryGetImageFromUrl(string imageUrl, int width, int height) =>
            _coverageClient.TryGetImageFromUrl(imageUrl, width, height);

        /// <summary>
        /// Tries to get a hint image from the URL
        /// </summary>
        /// <param name="imageUrl">The URL used to get the hint image</param>
        /// <param name="width">The requested width of the hint image</param>
        /// <param name="height">The requested height of the hint image</param>
        /// <param name="onImageDownloaded">Callback when the hint image has been downloaded</param>
        public void TryGetImageFromUrl(string imageUrl, int width, int height, Action<Texture> onImageDownloaded)
        {
            _coverageClient.TryGetImageFromUrl(imageUrl, width, height, onImageDownloaded);
        }
    }
}
