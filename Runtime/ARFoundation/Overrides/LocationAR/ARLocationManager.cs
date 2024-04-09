// Copyright 2022-2024 Niantic.
using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.PersistentAnchors;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.VpsCoverage;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.ARSubsystems;

#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

using Input = Niantic.Lightship.AR.Input;

namespace Niantic.Lightship.AR.LocationAR
{
    /// <summary>
    /// The ARLocationManager is used to track ARLocations.  ARLocations tie digital content to the physical world.
    /// When you start tracking an ARLocation, and aim your phone's camera at the physical location,
    /// the digital content that you child to the ARLocation will appear in the physical world.
    /// </summary>
    [PublicAPI]
    public class ARLocationManager : ARPersistentAnchorManager
    {
        [Header("Experimental: Drift Mitigation")]
        [Tooltip("Continue to send localization requests after initial localization is achieved. " +
            "This refines the localization over time and mitigates drift, but consumes more bandwidth")]
        [SerializeField]
        private bool _ContinuousLocalizationEnabled = XRPersistentAnchorConfiguration.DefaultContinuousLocalizationEnabled;

        [Tooltip("Interpolate anchor updates instead of snapping. Only works when continuous localization is enabled")]
        [SerializeField]
        private bool _InterpolationEnabled;

        /// <summary>
        /// Number of seconds over which anchor interpolation occurs.
        /// Faster times will result in more noticeable movement.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public float InterpolationTimeSeconds
        {
            get => ARPersistentAnchorInterpolator.InterpolationTimeSeconds;
            set => ARPersistentAnchorInterpolator.InterpolationTimeSeconds = value;
        }

        /// <summary>
        /// Whether to enable or disable continuous localization
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool ContinuousLocalizationEnabled
        {
            get => base.ContinuousLocalizationEnabled;
            set => base.ContinuousLocalizationEnabled = value;
        }

        /// <summary>
        /// Whether to enable or disable interpolation
        /// It is recommended to use the native solution "TransformUpdateSmoothingEnabled" instead of this property
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool InterpolationEnabled
        {
            get => base.InterpolateAnchors;
            set => base.InterpolateAnchors = value;
        }

#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
        [Tooltip("Averages/Fuses multiple localization results to provide a more stable localization. Only works when continuous localization is enabled")]
        [SerializeField]
        private bool _TemporalFusionEnabled = XRPersistentAnchorConfiguration.DefaultTemporalFusionEnabled;

        [Header("Experimental: Drift Mitigation tuning")]
        [FormerlySerializedAs("_CloudLocalizerMaxRequestsPerSecond")]
        [Tooltip("Number of seconds between server requests for initial localization. 0 value means as many requests as possible.")]
        [SerializeField]
        private float _InitialServiceRequestIntervalSeconds = 1 / XRPersistentAnchorConfiguration.DefaultCloudLocalizerInitialRequestsPerSecond;

        [Tooltip("Number of seconds between server requests for continuous localization. 0 value means as many requests as possible.")]
        [SerializeField]
        private float _ContinuousServiceRequestIntervalSeconds = 1 / XRPersistentAnchorConfiguration.DefaultCloudLocalizerContinuousRequestsPerSecond;

        [Tooltip("Number of localization samples used to fuse. This value should account for the rate of localization requests. " +
            "By default, it is recommended to cache around 5 seconds of localization samples to fuse")]
        [SerializeField]
        private uint _CloudLocalizationTemporalFusionWindowSize = XRPersistentAnchorConfiguration.DefaultCloudLocalizationTemporalFusionWindowSize;

        [Header("Experimental: Diagnostics")]
        [Tooltip("Whether to enable or disable frame diagnostics")]
        [SerializeField]
        private bool _DiagnosticsEnabled = XRPersistentAnchorConfiguration.DefaultDiagnosticsEnabled;

        /// <summary>
        /// Whether to enable or disable temporal fusion.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool TemporalFusionEnabled
        {
            get => base.TemporalFusionEnabled;
            set => base.TemporalFusionEnabled = value;
        }

        /// <summary>
        /// Whether to enable or disable transform update smoothing
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool TransformUpdateSmoothingEnabled
        {
            get => base.TransformUpdateSmoothingEnabled;
            set => base.TransformUpdateSmoothingEnabled = value;
        }

        /// <summary>
        /// Number of seconds between server requests for initial localization. 0 value means as many requests as possible.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public float InitialServiceRequestIntervalSeconds
        {
            get => base.CloudLocalizerInitialRequestsPerSecond <= 0 ? 0 : 1 / base.CloudLocalizerInitialRequestsPerSecond;
            set => base.CloudLocalizerInitialRequestsPerSecond = value <= 0 ? 0 : 1 / value;
        }

        /// <summary>
        /// Number of seconds between server requests for continuous localization. 0 value means as many requests as possible.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public float ContinuousServiceRequestIntervalSeconds
        {
            get => base.CloudLocalizerContinuousRequestsPerSecond <= 0 ? 0 : 1 / base.CloudLocalizerContinuousRequestsPerSecond;
            set => base.CloudLocalizerContinuousRequestsPerSecond = value <= 0 ? 0 : 1 / value;
        }

        /// <summary>
        /// Number of localization samples used to fuse
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public uint CloudLocalizationTemporalFusionWindowSize
        {
            get => base.CloudLocalizationTemporalFusionWindowSize;
            set => base.CloudLocalizationTemporalFusionWindowSize = value;
        }

        /// <summary>
        /// Whether to enable or disable frame diagnostics
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool DiagnosticsEnabled
        {
            get => base.DiagnosticsEnabled;
            set => base.DiagnosticsEnabled = value;
        }

        /// <summary>
        /// Obsolete. Use CloudLocalizationTemporalFusionWindowSize instead.
        /// </summary>
        [Obsolete]
        public int TemporalFusionSlidingWindow
        {
            get => (int)base.CloudLocalizationTemporalFusionWindowSize;
            set => base.CloudLocalizationTemporalFusionWindowSize = (uint)value;
        }
#endif

        [Header("AR Locations")]
        [Tooltip("Whether or not to auto-track the currently selected location.  Auto-tracked locations will be enabled, including their children, when the camera is aimed at the physical location.")]
        [SerializeField]
        private bool _autoTrack = false;

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
        /// Maximum number of locations that will be tracked by StartTracking. MaxLocationTrackingCount is always 1.
        /// Future versions of ARDK may support tracking more than one location at a time, but currently only one
        /// location at a time can be tracked.
        /// </summary>
        public int MaxLocationTrackingCount
        {
            get
            {
                return _maxLocationTrackingCount;
            }
        }

        // _targetARLocations hold the locations specified by SetARLocations().
        // At most one of these _targetARLocations will be tracked.
        private readonly List<ARLocation> _targetARLocations = new();

        // _trackedARLocations holds the location actively being tracked.
        // Its maximum size will be MaxLocationTrackingCount (currently always 1).
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
            // This will invoke OnBeforeStart and then start the subsystem. Put any initialization logic
            //  dependent on the subsystem in OnBeforeStart.
            base.OnEnable();
            arPersistentAnchorStateChanged += HandleARPersistentAnchorStateChanged;
        }

        protected override void OnBeforeStart()
        {
            // Apply the settings from the inspector
            base.ContinuousLocalizationEnabled = _ContinuousLocalizationEnabled;
#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
            base.TemporalFusionEnabled = _TemporalFusionEnabled;
            base.TransformUpdateSmoothingEnabled = _InterpolationEnabled;
            base.CloudLocalizerInitialRequestsPerSecond = _InitialServiceRequestIntervalSeconds <= 0 ? 0 : 1 / _InitialServiceRequestIntervalSeconds;
            base.CloudLocalizerContinuousRequestsPerSecond = _ContinuousServiceRequestIntervalSeconds <= 0 ? 0 : 1 / _ContinuousServiceRequestIntervalSeconds;
            base.CloudLocalizationTemporalFusionWindowSize = _CloudLocalizationTemporalFusionWindowSize;
            base.DiagnosticsEnabled = _DiagnosticsEnabled;
#endif

            // This will set the config based on the previously applied settings
            base.OnBeforeStart();
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
        /// Selects the AR Locations to try to track when StartTracking() is called. At most one of these locations will
        /// actually be tracked.
        /// </summary>
        /// <param name="arLocation">The locations to try to track.</param>
        public void SetARLocations(params ARLocation[] arLocations)
        {
            _targetARLocations.Clear();
            _targetARLocations.AddRange(arLocations);
        }

        /// <summary>
        /// Starts tracking locations specified by SetARLocations().
        ///
        /// Currently only one location can be tracked at a time.
        /// In the future, the number of locations tracked will be limited to MaxAnchorTrackingCount.
        ///
        /// Content authored as children of the ARLocation will be enabled once the ARLocation becomes tracked.
        /// This will create digital content in the physical world.
        ///
        /// If no locations were specified in SetARLocations(), requests will be made to attempt to track nearby
        /// locations. In this case, up to 5 nearby locations will be targeted and the first one to successfully
        /// track will be used.
        /// </summary>
        public void StartTracking()
        {
            // No locations specified by SetARLocations(). Finding closest locations.
            if (_targetARLocations.Count == 0)
            {
                if (_coverageARLocationHolders.Count != 0)
                {
                    Log.Error(
                        $"You are already tracking the {_coverageARLocationHolders.Count} closest locations. Call StopTracking() before calling StartTracking()." +
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
        /// Stops tracking the currently tracked location.  This must be called before switching to a new location.
        /// @note Anchors are destroyed asynchronously, so there needs to be a small delay after calling StopTracking()
        /// before calling StartTracking().
        /// </summary>
        public void StopTracking()
        {
            if (_anchorToARLocationMap.Count == 0)
            {
                Log.Info($"No AR Location is currently being tracked, so StopTracking() is not needed for " +
                         $"ARLocationManager on GameObject '{gameObject.name}'.");
                return;
            }

            // Rely on Removed anchor event to clear state, instead of double clearing
            foreach (var anchor in _anchorToARLocationMap.Keys)
            {
                DestroyAnchor(anchor);
            }

            if (MonoBehaviourEventDispatcher.IsMainThread())
            {
                ForceUpdate();
            }
        }

        /// <summary>
        /// Tries to refresh the tracking of the currently tracked anchors.
        /// </summary>
        public void TryUpdateTracking()
        {
            if (_anchorToARLocationMap.Count == 0)
            {
                Log.Error($"No AR Location is currently being tracked, so TryUpdateTracking() is not needed." +
                    gameObject);
                return;
            }

            foreach (var arLocation in _trackedARLocations)
            {
                bool success =
                    TryTrackAnchor(arLocation.Payload, out var existingAnchor);
                if (!success)
                {
                    Log.Error($"Failed to attempt to update tracking of anchor." + arLocation.gameObject);
                }
            }
        }

        /// <summary>
        /// Destroy anchors that will not be tracked because we are already tracking the max number (_maxLocationTrackingCount)
        /// </summary>
        private void DestroySurplusAnchors()
        {
            if (_anchorToARLocationMap.Count <= _maxLocationTrackingCount)
            {
                // There can not be any surplus anchor.
                return;
            }

            if (_trackedARLocations.Count >= _maxLocationTrackingCount)
            {
                // There are no surplus anchors yet
                return;
            }

            // Figure out what anchors to destroy and reset their corresponding arLocation
            var anchorsToBeRemoved = new List<ARPersistentAnchor>();
            foreach (var (anchor, arLocation) in _anchorToARLocationMap)
            {
                if (_trackedARLocations.Contains(arLocation))
                {
                    // Skip. We are tracking this location
                    continue;
                }

                // Reset arLocation to original parent
                ParentARLocationToOriginal(arLocation, anchor);

                // Take note of anchors to remove
                anchorsToBeRemoved.Add(anchor);
            }

            // Destroy anchors that need to be removed
            foreach (var anchor in anchorsToBeRemoved)
            {
                _anchorToARLocationMap.Remove(anchor);
                DestroyAnchor(anchor);
            }
        }

        private void HandleARPersistentAnchorStateChanged(
            ARPersistentAnchorStateChangedEventArgs arPersistentAnchorStateChangedEventArgs)
        {
            using (new ScopedProfiler("OnLocationTracked"))
            {
                var anchor = arPersistentAnchorStateChangedEventArgs.arPersistentAnchor;
                if (!_anchorToARLocationMap.ContainsKey(anchor))
                {
                    // ARLocation/Anchor is not longer tracked
                    return;
                }
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

                            // Destroy anchors that will never correspond to _trackedARLocations
                            DestroySurplusAnchors();
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
                    var wasTrackedAnchorRemoved = false;
                    // If the anchor is removed
                    if (anchor.trackingStateReason == TrackingStateReason.Removed)
                    {
                        ParentARLocationToOriginal(arLocation, anchor);
                        wasTrackedAnchorRemoved = _trackedARLocations.Remove(arLocation);
                        _anchorToARLocationMap.Remove(anchor);

                        // If the last tracked location is removed, clear state and be ready for the next StartTracking()
                        if (_anchorToARLocationMap.Count == 0)
                        {
                            ClearLocationManagerState();
                        }
                    }

                    // This arLocation is NOT one we track yet
                    // If a tracked anchor was removed, we still want to notify that the arLocation is no longer tracked
                    if (!_trackedARLocations.Contains(arLocation) && !wasTrackedAnchorRemoved)
                    {
                        return;
                    }

                    // Note: You could call arLocation.gameObject.SetActive(false) in locationTrackingStateChanged event if you wanted to de-activate gameObject when tracking is lost
                    var args = new ARLocationTrackedEventArgs(arLocation, false);
                    locationTrackingStateChanged?.Invoke(args);
                }
            }
        }

        private void TryTrackLocations(params ARLocation[] arLocations)
        {
            if (_anchorToARLocationMap.Count != 0)
            {
                Log.Error(
                    $"You are already tracking {_anchorToARLocationMap.Count} locations.  Call StopTracking() before attempting to track a new location." +
                    gameObject);
                return;
            }

            // We currently only support up to 5 locations.
            if (arLocations.Length > 5)
            {
                Log.Error("More than 5 ARLocations were passed into StartTrackingOneOfMany. We only support up to 5." +
                    gameObject);
                return;
            }

            foreach (var arLocation in arLocations)
            {
                var payload = arLocation.Payload;
                bool success = TryTrackAnchor(payload, out var anchor);
                if (success)
                {
                    _originalParents.Add(anchor, arLocation.transform.parent);
                    arLocation.transform.SetParent(anchor.transform, false);
                    _anchorToARLocationMap.Add(anchor, arLocation);
                }
                else
                {
#if UNITY_EDITOR
                    Log.Error($"Failed to track anchor." +
                        $"{Environment.NewLine}" +
                        $"In-Editor Playback uses Standalone XR Plug-in Management. Try enabling \"Niantic Lightship SDK\" for Standalone in XR Plug-in Management under Project Settings. And verify validity of Playback dataset" +
                        gameObject);
#else
                    Log.Error($"Failed to track anchor." + arLocation.gameObject);
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
                    Log.Error($"Cannot get GPS!" + gameObject);
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
                        Log.Error($"We need FineLocation Permission from Android!" + gameObject);
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

        private void ParentARLocationToOriginal(ARLocation arLocation, ARPersistentAnchor anchor)
        {
            arLocation.gameObject.SetActive(false);
            if (_originalParents.TryGetValue(anchor, out Transform parent))
            {
                // If the anchor (current parent) is deactivated this frame, we can't move the arlocation
                //  from under it. However... the anchor is deactivated when it is about the be removed,
                //  so we will never be able to reparent the arlocation at scene close.
                // Instead, developers have to call StopTracking() before scene close.
                if (arLocation.transform.parent.gameObject.activeInHierarchy)
                {
                    arLocation.transform.SetParent(parent, false);
                }
                else
                {
                    Log.Warning("ARLocation cannot be reparented at scene close, because the anchor is deactivated. " +
                        "Call StopTracking() before scene close to avoid this problem.");
                }
            }

            _originalParents.Remove(anchor);
        }

        private void ClearLocationManagerState()
        {
            _trackedARLocations.Clear();
            _anchorToARLocationMap.Clear();
            _originalParents.Clear();

            foreach (var arLocationHolder in _coverageARLocationHolders)
            {
                Destroy(arLocationHolder);
            }
            _coverageARLocationHolders.Clear();
        }
    }
}
