using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Loader;
using System.Linq;

using Niantic.Lightship.AR.Utilities;

using UnityEngine;
#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// Manages persistent anchors.
    ///
    /// Use this component to programmatically restore, remove, or query for persistent
    /// anchors. Persistent anchors are persistent <c>Pose</c>s in the world that are generated
    /// by processed scans, and will be in the same real world location in future sessions.
    /// By placing virtual content relative to a Persistent Anchor, it can be restored to the same
    /// real world location in a future session.
    ///
    /// This is a low level API to manage Persistent Anchors. For authoring virtual content in the
    /// Unity Editor, use the ARLocationManager and ARLocations instead.
    /// </summary>
    /// <remarks>
    /// <para>Subscribe to changes (added, updated, and removed) via the
    /// <see cref="ARPersistentAnchorManager.arPersistentAnchorStateChanged"/> event.</para>
    /// </remarks>
    /// <seealso cref="ARTrackableManager{TSubsystem,TSubsystemDescriptor,TProvider,TSessionRelativeData,TTrackable}"/>
    [DefaultExecutionOrder(LightshipARUpdateOrder.k_PersistentAnchorManager)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Unity.XR.CoreUtils.XROrigin))]
    [PublicAPI]
    public class ARPersistentAnchorManager : ARTrackableManager<
        XRPersistentAnchorSubsystem,
        XRPersistentAnchorSubsystemDescriptor,
        XRPersistentAnchorSubsystem.Provider,
        XRPersistentAnchor,
        ARPersistentAnchor>
    {
        [Tooltip("The GameObject to use when creating an anchor.  If null, a new GameObject will be created.")] [SerializeField]
        private GameObject _defaultAnchorGameobject;

        /// <summary>
        /// Called when the state of an anchor has changed
        ///
        /// Each invocation of this event contains a single Persistent Anchor that has had a state or pose change
        ///     this frame.
        /// Query the arg's arPersistentAnchor's TrackingState to determine its new
        ///     TrackingState.
        /// Query the arPersistentAnchor's PredictedPose to determine its new
        ///     PredictedPose.
        /// </summary>
        public event Action<ARPersistentAnchorStateChangedEventArgs> arPersistentAnchorStateChanged;

        /// <summary>
        /// The singleton instance reference of the ARPersistentAnchorManager.
        /// For internal use.
        /// </summary>
        internal static ARPersistentAnchorManager Instance { get; private set; }

        internal Dictionary<TrackableId, ARPersistentAnchor> Trackables => m_Trackables;
        internal Dictionary<TrackableId, ARPersistentAnchor> PendingAdds => m_PendingAdds;

        private Dictionary<ARPersistentAnchor, TrackingState> _arPersistentAnchorStates = new();

        /// <summary>
        /// The prefab to use when creating an ARPersistentAnchor.  If null, a new GameObject will be created.
        /// </summary>
        protected override GameObject GetPrefab() => _defaultAnchorGameobject;

        protected bool InterpolateAnchors = false;
        protected bool TemporalFusionEnabled = false;

        internal GameObject DefaultAnchorPrefab
        {
            get => _defaultAnchorGameobject;
        }

        private IARPersistentAnchorManagerImplementation _arPersistentAnchorManagerImplementation;

        protected override void Awake()
        {
            base.Awake();
            if (Instance)
            {
                Debug.LogError($"{nameof(ARPersistentAnchorManager)} already has a singleton reference.  Each scene should only have one {nameof(ARPersistentAnchorManager)}.", gameObject);
            }
            else
            {
                Instance = this;
            }

            InitializeARPersistentAnchorManagerImplementation();
        }

        protected virtual void Start()
        {
            RequestLocationPermission();
        }

        protected override void OnDestroy()
        {
            DestroyARPersistentAnchorManagerImplementation();
            var trackables = m_Trackables.Values.ToArray();
            foreach(var trackable in trackables)
            {
                trackable._markedForDestruction = true;
                DestroyAnchor(trackable);
            }
            base.OnDestroy();
            Instance = null;
        }

        /// <summary>
        /// The name to assign to the `GameObject` instantiated for each <see cref="ARPersistentAnchor"/>.
        /// </summary>
        protected override string gameObjectName => "Persistent Anchor";

        /// <summary>
        /// Gets the vps session id (as 32 character hexidecimal upper-case string)
        /// A vps session is defined between first TryTrackAnchor and last DestroyAnchor for a given set of anchors
        /// </summary>
        /// <param name="vpsSessionId">The vps session id as 32 character hexidecimal upper-case string</param>
        /// <returns>
        /// <c>True</c> If vps session id can be obtained
        /// <c>False</c> If no vps session is running
        /// </returns>
        public bool GetVpsSessionId(out string vpsSessionId)
        {
            return _arPersistentAnchorManagerImplementation.GetVpsSessionId(this, out vpsSessionId);
        }

        /// <summary>
        /// Restores a Persistent Anchor from a Payload. The Anchor GameObject will be returned immediately,
        /// and children can be added to it, but a proper position and rotation not be applied
        /// until its TrackingState is Tracking.
        /// </summary>
        /// <param name="payload">The payload of the anchor to restore</param>
        /// <param name="arPersistentAnchor">The ARPersistentAnchor that was created from the payload</param>
        /// <returns>
        /// <c>True</c> If the anchor was successfully added for tracking.
        /// <c>False</c> The anchor cannot be added, it is either already added, or the payload is invalid
        /// </returns>
        public bool TryTrackAnchor(ARPersistentAnchorPayload payload, out ARPersistentAnchor arPersistentAnchor)
        {
            return _arPersistentAnchorManagerImplementation.TryTrackAnchor(this, payload, out arPersistentAnchor);
        }

        /// <summary>
        /// Destroys an anchor and stop tracking it.
        /// </summary>
        /// <param name="arPersistentAnchor">The anchor to destroy</param>
        public void DestroyAnchor(ARPersistentAnchor arPersistentAnchor)
        {
            _arPersistentAnchorManagerImplementation.DestroyAnchor(this, arPersistentAnchor);
        }

        internal new ARPersistentAnchor CreateTrackableImmediate(XRPersistentAnchor xrPersistentAnchor)
        {
            var trackableId = xrPersistentAnchor.trackableId;
            if (base.m_Trackables.TryGetValue(trackableId, out var trackable))
            {
                return trackable;
            }

            return base.CreateTrackableImmediate(xrPersistentAnchor);
        }

        /// <summary>
        /// Invoked when the base class detects trackable changes.
        /// </summary>
        /// <param name="added">The list of added anchors.</param>
        /// <param name="updated">The list of updated anchors.</param>
        /// <param name="removed">The list of removed anchors.</param>
        protected override void OnTrackablesChanged(
            List<ARPersistentAnchor> added,
            List<ARPersistentAnchor> updated,
            List<ARPersistentAnchor> removed)
        {
            base.OnTrackablesChanged(added, updated, removed);
            using (new ScopedProfiler("OnPersistentAnchorsChanged"))
            {
                ReportAddedAnchors(added.ToArray());
                ReportUpdatedAnchors(updated.ToArray());
                ReportRemovedAnchors(removed.ToArray());
            }
        }

        internal void ReportAddedAnchors(params ARPersistentAnchor[] addedAnchors)
        {
            foreach (var addedAnchor in addedAnchors)
            {
                _arPersistentAnchorStates.Add(addedAnchor, addedAnchor.trackingState);
                if (addedAnchor.trackingState == TrackingState.Tracking || 
                    addedAnchor.trackingState == TrackingState.Limited)
                {
                    addedAnchor.PredictedPose = TransformToPose(addedAnchor.transform);
                }

                var arPersistentAnchorStateChangedEvent = new ARPersistentAnchorStateChangedEventArgs(addedAnchor);
                arPersistentAnchorStateChanged?.Invoke(arPersistentAnchorStateChangedEvent);
            }
        }

        internal void ReportUpdatedAnchors(params ARPersistentAnchor[] updatedAnchors)
        {
            foreach (var updatedAnchor in updatedAnchors)
            {
                if (!_arPersistentAnchorStates.ContainsKey(updatedAnchor))
                {
                    //Sometimes an anchor is updated before it is added.  This waits until an anchor is added before running update logic.
                    continue;
                }

                var predictedPose = TransformToPose(updatedAnchor.transform);
                var nextAnchorPose = predictedPose;
                var lastAnchorPose = updatedAnchor.PredictedPose;

                if (TemporalFusionEnabled)
                {
                    // Override the raw predicted pose with the previous fused pose
                    lastAnchorPose = updatedAnchor.FusedPose;

                    if (updatedAnchor.trackingState == TrackingState.None)
                    {
                        updatedAnchor.ClearTemporalFusion();
                    }
                    else if (updatedAnchor.trackingState == TrackingState.Tracking)
                    {
                        // Update temporal fusion, this manipulates the pose of the anchor.
                        nextAnchorPose = updatedAnchor.UpdateAndApplyTemporalFusion(predictedPose);
                    }
                    else if (updatedAnchor.trackingState == TrackingState.Limited)
                    {
                        // Apply the previous fused pose to overwrite the limited prediction
                        updatedAnchor.ApplyPoseToTransform(updatedAnchor.FusedPose);
                        
                        // For interpolation, target == start, so no interpolation takes place
                        nextAnchorPose = lastAnchorPose;
                    }
                }

                if (InterpolateAnchors)
                {
                    // Start interpolation with the previous cached pose, before updating it
                    // With temporal fusion, this will set the pose back to the previous fused pose
                    updatedAnchor.StartInterpolationToPose
                        (
                            nextAnchorPose,
                            lastAnchorPose
                        );
                }

                // Update predicted pose last, this destroys the previous prediction data
                if (updatedAnchor.trackingState == TrackingState.Tracking || 
                    updatedAnchor.trackingState == TrackingState.Limited)
                {
                    updatedAnchor.PredictedPose = predictedPose;
                }

                _arPersistentAnchorStates[updatedAnchor] = updatedAnchor.trackingState;
                var arPersistentAnchorStateChangedEvent =
                    new ARPersistentAnchorStateChangedEventArgs(updatedAnchor);
                arPersistentAnchorStateChanged?.Invoke(arPersistentAnchorStateChangedEvent);
            }
        }

        internal void ReportRemovedAnchors(params ARPersistentAnchor[] removedAnchors)
        {
            foreach (var removedAnchor in removedAnchors)
            {
                removedAnchor.trackingStateOverride = TrackingState.None;
                removedAnchor.trackingStateReasonOverride = TrackingStateReason.Removed;
                _arPersistentAnchorStates.Remove(removedAnchor);
                var arPersistentAnchorStateChangedEvent = new ARPersistentAnchorStateChangedEventArgs(removedAnchor);
                arPersistentAnchorStateChanged?.Invoke(arPersistentAnchorStateChangedEvent);
            }
        }

        private void InitializeARPersistentAnchorManagerImplementation()
        {
#if UNITY_EDITOR
            if (LightshipSettings.Instance.EditorPlaybackEnabled)
            {
                _arPersistentAnchorManagerImplementation = new ARPersistentAnchorManagerImplementation(this);
            }
            else
            {
                _arPersistentAnchorManagerImplementation = new MockARPersistentAnchorManagerImplementation(this);
            }
#else
            _arPersistentAnchorManagerImplementation = new ARPersistentAnchorManagerImplementation(this);
#endif
        }

        private void DestroyARPersistentAnchorManagerImplementation()
        {
            if (_arPersistentAnchorManagerImplementation != null)
            {
                _arPersistentAnchorManagerImplementation.Dispose();
            }
        }

        private void RequestLocationPermission()
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
            }
#endif
        }

        private Pose TransformToPose(Transform tf)
        {
            return new Pose(tf.position, tf.rotation);
        }
    }
}
