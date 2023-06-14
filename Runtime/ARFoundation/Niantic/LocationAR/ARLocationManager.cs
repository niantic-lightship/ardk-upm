using System;
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

        private ARLocation _trackedARLocation;
        private ARPersistentAnchor _trackedARPersistentAnchor;

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
            if (_trackedARLocation)
            {
                Debug.LogError(
                    $"You are already tracking {_trackedARLocation.name}.  Call StopTracking() before attempting to track a new location.",
                    _trackedARLocation.gameObject);
                return;
            }

            _trackedARLocation = arLocation;
            var payload = arLocation.Payload;
            bool success =
                TryTrackAnchor(payload, out _trackedARPersistentAnchor);
            _trackedARLocation.transform.SetParent(_trackedARPersistentAnchor.transform, false);
            if (!success)
            {
                Debug.LogError($"Failed to track anchor,", gameObject);
            }
        }

        /// <summary>
        /// Stops tracking the currently tracked location.  This must be called before switching to a new location.
        /// </summary>
        public void StopTracking()
        {
            if (!_trackedARLocation)
            {
                Debug.LogError($"No AR Location is currently being tracked, so StopTracking() is not needed.",
                    gameObject);
                return;
            }

            _trackedARLocation.gameObject.SetActive(false);
            _trackedARLocation.transform.SetParent(transform, false);
            DestroyAnchor(_trackedARPersistentAnchor);
            _trackedARLocation = null;
        }

        private void HandleARPersistentAnchorStateChanged(
            ARPersistentAnchorStateChangedEventArgs arPersistentAnchorStateChangedEventArgs)
        {
            using (new ScopedProfiler("OnLocationTracked"))
            {
                bool tracked = _trackedARPersistentAnchor.trackingState == TrackingState.Tracking;
                if (tracked)
                {
                    if (_trackedARLocation)
                    {
                        _trackedARLocation.gameObject.SetActive(true);
                    }

                    var args = new ARLocationTrackedEventArgs(_trackedARLocation, true);
                    locationTrackingStateChanged?.Invoke(args);
                }
                else
                {
                    var args = new ARLocationTrackedEventArgs(_trackedARLocation, false);
                    locationTrackingStateChanged?.Invoke(args);
                }
            }
        }
    }
}
