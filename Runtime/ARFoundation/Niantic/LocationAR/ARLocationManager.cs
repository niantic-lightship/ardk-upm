using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

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
        [Tooltip("Whether or not to auto-track the currently selected location.  Auto-tracked locations will be enabled, including their children, when the camera is aimed at the physical location.")]
        [SerializeField]
        private bool _autoTrack = false;

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

        private Dictionary<ARPersistentAnchor, ARLocation> _trackedARLocations = new();
        private Dictionary<ARPersistentAnchor, Transform> _originalParents = new();

        private bool _onlyUsingPrimaryARLocation = false;
        private ARLocation _primaryARLocation = null;

        private CoverageClient _coverageClient = null;
        private List<GameObject> _arLocationHolders = new();

        protected override void OnEnable()
        {
            base.OnEnable();
            arPersistentAnchorStateChanged += HandleARPersistentAnchorStateChanged;
        }

        protected override void Start()
        {
            base.Start();
            foreach (var arLocation in ARLocations)
            {
                if (_autoTrack && arLocation.gameObject.activeSelf)
                {
                    arLocation.gameObject.SetActive(false);
                    StartTracking(arLocation);
                }
                else //TODO: Do this at build time, not in start here
                {
                    arLocation.gameObject.SetActive(false);
                }
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            arPersistentAnchorStateChanged -= HandleARPersistentAnchorStateChanged;
        }

        /// <summary>
        /// Starts tracking a location.  This will create digital content in the physical world.
        /// Content authored as children of the ARLocation will be enabled once the ARLocation becomes tracked.
        /// </summary>
        /// <param name="arLocation">The location to track.</param>
        public void StartTracking(ARLocation arLocation)
        {
            ARLocation[] arLocations = { arLocation };
            StartTrackingOneOfMany(arLocations);
        }

        /// <summary>
        /// Attempt to track one of the many locations provided. The first location to gain tracking will be tracked until StopTracking() is called.
        /// We currently only support up to 5 locations. This will create digital content in the physical world.
        /// Content authored as children of each ARLocation will be enabled once the ARLocation becomes tracked.
        /// To switch which location of the array is tracked, call StopTracking() and this function again.
        /// </summary>
        /// <param name="arLocations">The locations to track. Only one of them will be tracked</param>
        public void StartTrackingOneOfMany(params ARLocation[] arLocations)
        {
            if (_trackedARLocations.Count != 0)
            {
                Debug.LogError(
                    $"You are already tracking {_trackedARLocations.Count} locations.  Call StopTracking() before attempting to track a new location.", 
                    gameObject);
                return;
            }

            // We currently only support up to 5 locations atm.
            if (5 < arLocations.Length)
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
                    _originalParents.Add(anchor, arLocation.transform);
                    arLocation.transform.SetParent(anchor.transform, false);
                    _trackedARLocations.Add(anchor, arLocation);
                }
                else
                {
#if UNITY_EDITOR
                    Debug.LogError($"Failed to track anchor." +
                        $"{Environment.NewLine}" +
                        $"In-Editor Playback uses Standalone XR Plug-in Management.  Try enabling \"Niantic Lightship SDK\" for Standalone in XR Plug-in Management under Project Settings.",
                        gameObject);
#else
                    Debug.LogError($"Failed to track anchor.", arLocation.gameObject);
#endif
                }
            }

            _onlyUsingPrimaryARLocation = true;
        }

        /// <summary>
        /// Starts tracking one location out of many possible default locations.
        /// Content authored as children of default ARLocation will be enabled while the default ARLocation is tracked.
        /// To get a different default location, call StopTracking() and this function again.
        /// </summary>
        public void StartTrackingOneDefaultLocation()
        {
            if (_arLocationHolders.Count != 0)
            {
                Debug.LogError(
                    $"You are already tracking {_arLocationHolders.Count} default locations.  Call StopTracking() before attempting to track new default locations.", 
                    gameObject);
                return;
            }

            if (_coverageClient == null)
            {
                _coverageClient = CoverageClientFactory.Create();
            }
            
            // Location Permissions should already have been granted 
            var inputLocation = new LatLng(Input.location.lastData);
            var queryRadius = 500; // 500 meters
            _coverageClient.TryGetCoverage(inputLocation, queryRadius, OnTryGetCoverage);
        }

        /// <summary>
        /// Stops tracking the currently tracked location.  This must be called before switching to a new location.
        /// </summary>
        public void StopTracking()
        {
            if (_trackedARLocations.Count == 0)
            {
                Debug.LogError($"No AR Location is currently being tracked, so StopTracking() is not needed.",
                    gameObject);
                return;
            }

            foreach (var (anchor, arLocation) in _trackedARLocations)
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
            _originalParents.Clear();

            _onlyUsingPrimaryARLocation = false;
            _primaryARLocation = null;

            foreach (var arLocationHolder in _arLocationHolders)
            {
                Destroy(arLocationHolder);
            }
            _arLocationHolders.Clear();
        }

        /// <summary>
        /// Tries to refresh the tracking of the currently tracked anchors.
        /// </summary>
        public void TryUpdateTracking()
        {
            if (_trackedARLocations.Count == 0)
            {
                Debug.LogError($"No AR Location is currently being tracked, so AttemptToUpdateTracking() is not needed.",
                    gameObject);
                return;
            }

            // If using primary location, we do not update the other ones.
            if (_onlyUsingPrimaryARLocation)
            {
                if (_primaryARLocation)
                {
                    bool success =
                        TryTrackAnchor(_primaryARLocation.Payload, out var existingAnchor);
                    if (!success)
                    {
                        Debug.LogError($"Failed to attempt to update tracking of anchor.", _primaryARLocation.gameObject);
                    }
                }
                return;
            }

            foreach (var (anchor, arLocation) in _trackedARLocations)
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

                foreach (var (anchor, arLocation) in _trackedARLocations)
                {

                    // We only want to track the primary location if _onlyUsingPrimaryARLocation = true
                    if (_onlyUsingPrimaryARLocation && _primaryARLocation) 
                    {
                        if (_primaryARLocation != arLocation)
                        {
                            // This arLocation is NOT the primary location
                            continue;
                        }
                    }

                    bool tracked = anchor.trackingState == TrackingState.Tracking;
                    if (tracked)
                    {
                        // The primary location is set to the first tracked location
                        if (_onlyUsingPrimaryARLocation && !_primaryARLocation) 
                        {
                            _primaryARLocation = arLocation;
                        }

                        arLocation.gameObject.SetActive(true);
                        var args = new ARLocationTrackedEventArgs(arLocation, true);
                        locationTrackingStateChanged?.Invoke(args);
                    }
                    else
                    {

                        // We only want to give updates to the primary location if _onlyUsingPrimaryARLocation = true
                        if (_onlyUsingPrimaryARLocation && !_primaryARLocation) 
                        {
                            continue;
                        }

                        arLocation.gameObject.SetActive(false);
                        var args = new ARLocationTrackedEventArgs(arLocation, false);
                        locationTrackingStateChanged?.Invoke(args);
                    }
                }
            }
        }

        private void OnTryGetCoverage(AreaTargetsResult args)
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
                _arLocationHolders.Add(arLocationHolder);
                arLocations.Add(arLocation);

                // Only choose the closest 5 locations.
                if (5 <= arLocations.Count)
                {
                    break;
                }
            }

            StartTrackingOneOfMany(arLocations.ToArray());
        }

    }
}
