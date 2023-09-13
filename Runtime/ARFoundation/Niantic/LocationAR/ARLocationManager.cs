using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

using Input = Niantic.Lightship.AR.Input;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// The ARLocationManager is used to track ARLocations.  ARLocations tie digital content to the physical world.
    /// When you start tracking an ARLocation, and aim your phone's camera at the physical location,
    /// the digital content that you child to the ARLocation will appear in the physical world.
    /// </summary>
    [PublicAPI]
    public class ARLocationManager : ARPersistentAnchorManager
    {
#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
        [Header("Experimental: Drift Mitigation")]
        [Tooltip("Continuously send localization requests to refine ARLocation tracking")]
        [SerializeField]
        private bool _ContinuousLocalizationEnabled;
        
        [Tooltip("Interpolate anchor updates instead of snapping. Only works when continuous localization is enabled")]
        [SerializeField]
        private bool _InterpolationEnabled;
        
        [Tooltip("Averages multiple localization results to provide a more stable localization. Only works when continuous localization is enabled")]
        [SerializeField]
        private bool _TemporalFusionEnabled;

        // Number of seconds between attempting Continuous Localization requests.
        // After attempting localization, server requests will be sent once a second until localization success
        public float ContinuousLocalizationRateSeconds
        {
            get => _continuousLocalizationRateSeconds;
            set => _continuousLocalizationRateSeconds = value;
        }

        // Number of seconds over which anchor interpolation occurs.
        // Faster times will result in more noticeable movement.
        public float InterpolationTimeSeconds
        {
            get => ARPersistentAnchor.InterpolationTimeSeconds;
            set => ARPersistentAnchor.InterpolationTimeSeconds = value;
        }
        
        // Number of localization results to average for temporal fusion
        public int TemporalFusionSlidingWindow {             
            get => ARPersistentAnchor.FusionSlidingWindowSize;
            set => ARPersistentAnchor.FusionSlidingWindowSize = value;
        }

        // Whether to enable or disable continuous localization
        public bool ContinuousLocalizationEnabled
        {
            get => _continuousLocalizationEnabled;
            set => _continuousLocalizationEnabled = value;
        }

        // Whether to enable or disable interpolation
        public bool InterpolationEnabled
        {
            get => InterpolateAnchors;
            set => InterpolateAnchors = value;
        }

        // Whether to enable or disable temporal fusion
        public new bool TemporalFusionEnabled
        {
            get => base.TemporalFusionEnabled;
            set => base.TemporalFusionEnabled = value;
        }
#endif

        [Header("AR Locations")]
        [Tooltip("Whether or not to auto-track the currently selected location.  Auto-tracked locations will be enabled, including their children, when the camera is aimed at the physical location.")]
        [SerializeField]
        private bool _autoTrack = false;

#region Experimental implementation
        private bool _continuousLocalizationEnabled = false;
        private float _continuousLocalizationRateSeconds = 2.0f;
        private float _elapsedTime = 0;
#endregion

        private bool _keepTryingStartLocationServices = false;
        
        // We only support MaxLocationTrackingCount = 1 at the moment
        private const int _maxLocationTrackingCount = 1;

        /// <summary>
        /// Called when the location tracking state has changed.
        /// </summary>
        public event Action<ARLocationTrackedEventArgs> locationTrackingStateChanged;

        /// <summary>
        /// Gets all of the ARLocations childed to the ARLocationManager.
        /// </summary>
        public ARLocation[] ARLocations => GetComponentsInChildren<ARLocation>(true);

        /// <summary>
        /// Whether or not to automatically start tracking the selected ARLocation.
        /// If true, the location that is currently enabled will be automatically tracked on Start.
        /// </summary>
        public bool AutoTrack => _autoTrack;

        /// <summary>
        /// Maximun number of locations that will be tracked by StartTracking. We only support MaxLocationTrackingCount = 1 at the moment
        /// </summary>
        public int MaxLocationTrackingCount
        {
            get
            { 
                return _maxLocationTrackingCount;
            }
        }

        // _targetARLocations hold the locations specified by SetARLocations().
        // Only a subset of _targetARLocations will be tracked. How many is capped by MaxLocationTrackingCount
        // _targetARLocations will be populated by SetARLocations()
        private readonly List<ARLocation> _targetARLocations = new();

        // _trackedARLocations holds the location actively being tracked.
        // Its maximun size will be MaxLocationTrackingCount
        // _trackedARLocations will be populated by HandleARPersistentAnchorStateChanged()
        private readonly List<ARLocation> _trackedARLocations = new();

        // _anchorToARLocationMap holds the relationship between each anchor and its corresponding location
        // ARPersistentAnchorManager updates anchors and then we use this map to determine what location corresponds to what anchor update
        // ARLocationManager will only consume a subset of the anchor updates. What ar locations will be tracked is determined by _trackedARLocations
        // _anchorToARLocationMap will be populated by StartTracking()
        private readonly Dictionary<ARPersistentAnchor, ARLocation> _anchorToARLocationMap = new();

        // _originalParents holds the transform that each location's anchor replaces
        private readonly Dictionary<ARPersistentAnchor, Transform> _originalParents = new();

        // _coverageClient is used when no location is set in SetARLocations
        private CoverageClient _coverageClient = null;

        // _coverageARLocationHolders hold the game objects that hold the ARLocations retreived from _coverageClient
        // _coverageARLocationHolders will be populated by OnCoverageLocationsQueried
        private readonly List<GameObject> _coverageARLocationHolders = new();

        protected override void OnEnable()
        {
            base.OnEnable();
#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
            _continuousLocalizationEnabled = _ContinuousLocalizationEnabled;
            InterpolateAnchors = _InterpolationEnabled;
            base.TemporalFusionEnabled = _TemporalFusionEnabled;
#endif
            arPersistentAnchorStateChanged += HandleARPersistentAnchorStateChanged;
        }

        protected override void Start()
        {
            base.Start();
            var arLocations = new List<ARLocation>();
            foreach (var arLocation in ARLocations)
            {
                if (_autoTrack && arLocation.gameObject.activeSelf)
                {
                    arLocation.gameObject.SetActive(false);
                    arLocations.Add(arLocation);
                }
                else //TODO: Do this at build time, not in start here
                {
                    arLocation.gameObject.SetActive(false);
                }
            }
            if (arLocations.Count != 0)
            {
                SetARLocations(arLocations.ToArray());
                StartTracking();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            arPersistentAnchorStateChanged -= HandleARPersistentAnchorStateChanged;

            // This stops TryStartLocationServiceForCoverage coroutine
            _keepTryingStartLocationServices = true;
        }

        /// <summary>
        /// Selects what AR Locations to track when StartTracking() is called
        /// </summary>
        /// <param name="arLocation">The locations to track.</param>
        public void SetARLocations(params ARLocation[] arLocations)
        {
            _targetARLocations.Clear();
            _targetARLocations.AddRange(arLocations);
        }

        /// <summary>
        /// Starts tracking locations specified by SetARLocations(). The number of locations tracked will be limited to MaxAnchorTrackingCount
        /// The location(s) that ends up being tracked up to MaxAnchorTrackingCount are in first-come first-serve order. This will create digital content in the physical world.
        /// Content authored as children of the ARLocation will be enabled once the ARLocation becomes tracked.
        /// If no locations were specified in SetARLocations(), the closest five locations for the area will be selected
        /// </summary>
        public void StartTracking()
        {
            // No locations specified by SetARLocations(). Finding closest locations.
            if (_targetARLocations.Count == 0)
            {
                if (_coverageARLocationHolders.Count != 0)
                {
                    Debug.LogError(
                        $"You are already tracking the {_coverageARLocationHolders.Count} closest locations. Call StopTracking() before calling StartTracking().", 
                        gameObject);
                    return;
                }

                if (_coverageClient == null)
                {
                    _coverageClient = CoverageClientFactory.Create();
                }
                TryTrackLocationsFromCoverage();
                return;
            }
            
            TryTrackLocations(_targetARLocations.ToArray());
        }

        /// <summary>
        /// Starts tracking a location.  This will create digital content in the physical world.
        /// Content authored as children of the ARLocation will be enabled once the ARLocation becomes tracked.
        /// </summary>
        /// <param name="arLocation">The location to track.</param>
        [Obsolete("StartTracking(ARLocation arLocation) is deprecated, please SetARLocations(params ARLocation[] arLocations) and StartTracking() instead")]
        public void StartTracking(ARLocation arLocation)
        {
            ARLocation[] arLocations = { arLocation };
            SetARLocations(arLocations);
            StartTracking();
        }

        /// <summary>
        /// Starts tracking one location out of closest five locations around.
        /// Content authored as children of default ARLocation will be enabled while the default ARLocation is tracked.
        /// To get a different tracked location, call StopTracking() and this function again.
        /// </summary>
        [Obsolete("StartTrackingOneDefaultLocation() is deprecated, please use SetARLocations(params ARLocation[] arLocations) and StartTracking() instead")]
        public void StartTrackingOneDefaultLocation()
        {
            StartTracking();
        }

        /// <summary>
        /// Stops tracking the currently tracked location.  This must be called before switching to a new location.
        /// </summary>
        public void StopTracking()
        {
            if (_anchorToARLocationMap.Count == 0)
            {
                Debug.LogError($"No AR Location is currently being tracked, so StopTracking() is not needed.",
                    gameObject);
                return;
            }

            foreach (var (anchor, arLocation) in _anchorToARLocationMap)
            {
                arLocation.gameObject.SetActive(false);
                var originalParent = _originalParents[anchor];
                if (originalParent)
                {
                    arLocation.transform.SetParent(originalParent, false);
                }
                DestroyAnchor(anchor);
            }
            _trackedARLocations.Clear();
            _anchorToARLocationMap.Clear();
            _originalParents.Clear();

            foreach (var arLocationHolder in _coverageARLocationHolders)
            {
                Destroy(arLocationHolder);
            }
            _coverageARLocationHolders.Clear();
        }

        /// <summary>
        /// Tries to refresh the tracking of the currently tracked anchors.
        /// </summary>
        public void TryUpdateTracking()
        {
            if (_anchorToARLocationMap.Count == 0)
            {
                Debug.LogError($"No AR Location is currently being tracked, so TryUpdateTracking() is not needed.",
                    gameObject);
                return;
            }

            foreach (var arLocation in _trackedARLocations)
            {
                bool success =
                    TryTrackAnchor(arLocation.Payload, out var existingAnchor);
                if (!success)
                {
                    Debug.LogError($"Failed to attempt to update tracking of anchor.", arLocation.gameObject);
                }
            }
        }

        private void HandleARPersistentAnchorStateChanged(
            ARPersistentAnchorStateChangedEventArgs arPersistentAnchorStateChangedEventArgs)
        {
            using (new ScopedProfiler("OnLocationTracked"))
            {

                var anchor = arPersistentAnchorStateChangedEventArgs.arPersistentAnchor;
                var arLocation = _anchorToARLocationMap[anchor];
                bool tracked = anchor.trackingState == TrackingState.Tracking;
                if (tracked)
                {
                    if (!_trackedARLocations.Contains(arLocation))
                    {
                        if (_trackedARLocations.Count < _maxLocationTrackingCount) 
                        {
                            // We can still track more locations because we have not reached _maxLocationTrackingCount
                            _trackedARLocations.Add(arLocation);
                        }
                        else
                        {
                            // This arLocation is NOT one we track
                            return;
                        }
                    }

                    arLocation.gameObject.SetActive(true);
                    var args = new ARLocationTrackedEventArgs(arLocation, true);
                    locationTrackingStateChanged?.Invoke(args);
                }
                else
                {

                    if (!_trackedARLocations.Contains(arLocation))
                    {
                        // This arLocation is NOT one we track
                        return;
                    }

                    arLocation.gameObject.SetActive(false);
                    var args = new ARLocationTrackedEventArgs(arLocation, false);
                    locationTrackingStateChanged?.Invoke(args);
                }
            }
        }

        private void TryTrackLocations(params ARLocation[] arLocations)
        {
            if (_anchorToARLocationMap.Count != 0)
            {
                Debug.LogError(
                    $"You are already tracking {_anchorToARLocationMap.Count} locations.  Call StopTracking() before attempting to track a new location.", 
                    gameObject);
                return;
            }

            // We currently only support up to 5 locations.
            if (arLocations.Length > 5)
            {
                Debug.LogError("More than 5 ARLocations were passed into StartTrackingOneOfMany. We only support up to 5.", 
                    gameObject);
                return;
            }

            foreach (var arLocation in arLocations)
            {
                var payload = arLocation.Payload;
                bool success =
                    TryTrackAnchor(payload, out var anchor);
                if (success)
                {
                    _originalParents.Add(anchor, arLocation.transform.parent);
                    arLocation.transform.SetParent(anchor.transform, false);
                    _anchorToARLocationMap.Add(anchor, arLocation);
                }
                else
                {
#if UNITY_EDITOR
                    Debug.LogError($"Failed to track anchor." +
                        $"{Environment.NewLine}" +
                        $"In-Editor Playback uses Standalone XR Plug-in Management. Try enabling \"Niantic Lightship SDK\" for Standalone in XR Plug-in Management under Project Settings. And verify validity of Playback dataset",
                        gameObject);
#else
                    Debug.LogError($"Failed to track anchor.", arLocation.gameObject);
#endif
                }
            }

        }

        private IEnumerator TryStartLocationServiceForCoverage()
        {
            _keepTryingStartLocationServices = true;
            while (_keepTryingStartLocationServices)
            {
                if (!Input.location.isEnabledByUser)
                {
                    // Cannot Start() if Location Permissions have not been granted
                    yield return new WaitForEndOfFrame();
                }
                else if (Input.location.status == LocationServiceStatus.Initializing)
                {
                    // Start() was already called. We need to wait until service is running to TryGetCoverage
                    yield return new WaitForEndOfFrame();
                }
                else if (Input.location.status == LocationServiceStatus.Stopped)
                {
                    // Default values match SubsystemDataAcquirer.cs
                    const float defaultAccuracyMeters = 0.01f;
                    const float defaultDistanceMeters = 0.01f;
                    Input.location.Start(defaultAccuracyMeters, defaultDistanceMeters);
                    // Start() was called. We need to wait until service is running to TryGetCoverage
                    yield return new WaitForEndOfFrame();
                }
                else if (Input.location.status == LocationServiceStatus.Running)
                {
                    // Successfully getting GPS!
                    _keepTryingStartLocationServices = false;

                    // We use coverage client to track nearby locations
                    var inputLocation = new LatLng(Input.location.lastData);
                    const int queryRadius = 500; // 500 meters
                    _coverageClient.TryGetCoverage(inputLocation, queryRadius, OnCoverageLocationsQueried);

                    // We will hit yield break; below
                }
                else
                {
                    // LocationServiceStatus.Failed
                    _keepTryingStartLocationServices = false;
                    Debug.LogError($"Cannot get GPS!", gameObject);
                    // We will hit yield break; below
                }
            }

            yield break;
        }

        private void TryTrackLocationsFromCoverage()
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                var androidPermissionCallbacks = new PermissionCallbacks();
                androidPermissionCallbacks.PermissionGranted += permissionName =>
                {
                    if (permissionName == "android.permission.ACCESS_FINE_LOCATION")
                    {
                        // This will call OnCoverageLocationsQueried() eventually
                        StartCoroutine(TryStartLocationServiceForCoverage());
                    }
                    else
                    {
                        Debug.LogError($"We need FineLocation Permission from Android!", gameObject);
                    }
                };

                Permission.RequestUserPermission(Permission.FineLocation, androidPermissionCallbacks);
                return;
            }
#endif
            // This will call OnCoverageLocationsQueried() eventually
            StartCoroutine(TryStartLocationServiceForCoverage());
        }
        
        private void OnCoverageLocationsQueried(AreaTargetsResult args)
        {
            var areaTargets = args.AreaTargets;

            // Sort the area targets by proximity to the user
            // LINQ usage should be limited to code that is only ran periodically.
            // It isn't performant enough to put it in the game loop (in "Update")
            areaTargets.Sort((a, b) =>
                a.Area.Centroid.Distance(args.QueryLocation).CompareTo(
                    b.Area.Centroid.Distance(args.QueryLocation)));

            var arLocations = new List<ARLocation>();
            foreach (var areaTarget in areaTargets)
            {
                var locationName = areaTarget.Target.Name;
                var anchorString = areaTarget.Target.DefaultAnchor;
                if (String.IsNullOrEmpty(anchorString))
                {
                    // This Area Target has no Default Anchor
                    continue;
                }
                // We create AR Location for Area Target
                var arLocationHolder = new GameObject(locationName);
                arLocationHolder.transform.SetParent(this.transform);
                arLocationHolder.SetActive(false); // The ARLocation will be enabled once the anchor starts tracking.
                var arLocation = arLocationHolder.AddComponent<ARLocation>();
                arLocation.Payload = new ARPersistentAnchorPayload(anchorString);

                // We keep track of ar locations gathered
                _coverageARLocationHolders.Add(arLocationHolder);
                arLocations.Add(arLocation);

                // Only choose the closest 5 locations.
                if (arLocations.Count >= 5)
                {
                    break;
                }
            }

            TryTrackLocations(arLocations.ToArray());
        }

        protected override void Update()
        {
            base.Update();

            if (!_continuousLocalizationEnabled || _trackedARLocations.Count == 0)
            {
                _elapsedTime = 0;
                return;
            }
            
            _elapsedTime += Time.deltaTime;
            // Currently the only criteria is elapsed time.
            // Can experiment with other behaviours here
            if (_elapsedTime >= _continuousLocalizationRateSeconds)
            {
                _elapsedTime = 0;
                TryUpdateTracking();
            }
        }
    }
}
