// Copyright 2022-2024 Niantic.
#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
using System;
using System.Threading;
using System.Threading.Tasks;

using Niantic.Lightship.AR.PersistentAnchors;
using Niantic.Lightship.AR.Utilities.Logging;

using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.LocationAR
{
    /// <summary>
    /// Implement a simple continuous localization manager that listens to an ARPersistentAnchorManager
    /// Calls into the persistent anchor manager to try re-localizing after a set time
    /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
    /// </summary>
    public class DefaultContinuousLocalizationManager : 
        MonoBehaviour
    {
        [SerializeField]
        private ARPersistentAnchorManager _persistentAnchorManager;
        
        [SerializeField]
        [Tooltip("Defines the interval after a successful localization request to attempt re-localization")]
        private float _relocalizationIntervalSeconds = 5.0f;
        
        private CancellationTokenSource _cancellationTokenSource;
        
        private void OnEnable()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _persistentAnchorManager.arPersistentAnchorStateChanged += OnAnchorStateChanged;
            
            // Start re-localization for all currently tracked anchors
            foreach (var anchor in _persistentAnchorManager.Trackables.Values)
            {
                if (anchor.trackingState == TrackingState.Tracking)
                {
                    var token = _cancellationTokenSource.Token;
                    var task = InvokeRelocalization(anchor, _relocalizationIntervalSeconds, token);
                }
            }
        }

        private void OnDisable()
        {
            if (_persistentAnchorManager)
            {
                _persistentAnchorManager.arPersistentAnchorStateChanged -= OnAnchorStateChanged;
            }
            
            _cancellationTokenSource.Cancel();
        }

        private void OnAnchorStateChanged(ARPersistentAnchorStateChangedEventArgs args)
        {
            if (args.arPersistentAnchor.trackingState == TrackingState.Tracking)
            {
                var token = _cancellationTokenSource.Token;
                var task = InvokeRelocalization(args.arPersistentAnchor, _relocalizationIntervalSeconds, token);
            }
        }

        private async Task InvokeRelocalization
        (
            ARPersistentAnchor anchor,
            float waitTimeSeconds,
            CancellationToken cancellationToken
        )
        {
            await Task.Delay(TimeSpan.FromSeconds(waitTimeSeconds), cancellationToken);

            // If the token is cancelled, don't try to re-localize
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // If the anchor has been destroyed, this reference is null (either gameobject null or real null)
            if (anchor == null)
            {
                return;
            }

            // If the anchor is no longer tracking, don't try to track it again
            if (!_persistentAnchorManager.Trackables.ContainsKey(anchor.trackableId))
            {
                return;
            }

            var payload = new ARPersistentAnchorPayload(anchor.GetDataAsBytes());
            var success = _persistentAnchorManager.TryTrackAnchor(payload, out var _);

            if (!success)
            {
                Log.Warning($"Could not re-localize anchor {anchor.trackableId}");
            }
        }
    }
}
#endif