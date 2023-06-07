using System;
using System.Collections;
using System.Threading.Tasks;

using Niantic.Lightship.AR.Subsystems;

using UnityEngine;
#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Niantic.Lightship.AR
{
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
        public float _queryLatitude;

        [SerializeField] [HideInInspector]
        public float _queryLongitude;

        [SerializeField] [HideInInspector]
        private LocalizationTarget[] _privateARLocalizationTargets;

        private CoverageClient _coverageClient;

        public bool UseCurrentLocation
        {
            get => _useCurrentLocation;
            set
            {
                _useCurrentLocation = value;
            }
        }

        public int QueryRadius
        {
            get => _queryRadius;
            set
            {
                _queryRadius = value;
            }
        }

        public float QueryLatitude
        {
            get => _queryLatitude;
            set
            {
                _queryLatitude = value;
            }
        }

        public float QueryLongitude
        {
            get => _queryLongitude;
            set
            {
                _queryLongitude = value;
            }
        }

        public LocalizationTarget[] PrivateARLocalizationTargets
        {
            get => _privateARLocalizationTargets;
            set
            {
                _privateARLocalizationTargets = value;
            }
        }

#if UNITY_EDITOR
        public ARLocationManifest[] PrivateARLocations
        {
            get => _privateARLocations;
            set
            {
                _privateARLocations = value;
            }
        }
#endif

        private void Start()
        {
            _coverageClient = CoverageClientFactory.Create();
        }

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

            bool wasLocationServicesAlreadyRunning = Input.location.status == LocationServiceStatus.Running;

            // only turn on location services if location services were not enabled in the first place
            if (!wasLocationServicesAlreadyRunning)
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
            if (!wasLocationServicesAlreadyRunning)
            {
                Input.location.Stop();
            }

            _coverageClient.TryGetCoverage(inputLocation, QueryRadius, onTryGetCoverage, _privateARLocalizationTargets);
        }

        public Task<Texture> TryGetImageFromUrl(string imageUrl) => _coverageClient.TryGetImageFromUrl(imageUrl);

        public void TryGetImageFromUrl(string imageUrl, Action<Texture> onImageDownloaded)
        {
            _coverageClient.TryGetImageFromUrl(imageUrl, onImageDownloaded);
        }

        public Task<Texture> TryGetImageFromUrl(string imageUrl, int width, int height) =>
            _coverageClient.TryGetImageFromUrl(imageUrl, width, height);

        public void TryGetImageFromUrl(string imageUrl, int width, int height, Action<Texture> onImageDownloaded)
        {
            _coverageClient.TryGetImageFromUrl(imageUrl, width, height, onImageDownloaded);
        }
    }
}
