// Copyright 2022-2024 Niantic.
using System;
using System.Collections;
using System.Collections.Generic;

using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Loader;
using System.Linq;
using System.Threading.Tasks;

using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;
using UnityEngine.Serialization;
#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PersistentAnchors
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
    [DefaultExecutionOrder(LightshipARUpdateOrder.PersistentAnchorManager)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Unity.XR.CoreUtils.XROrigin))]
    [PublicAPI("apiref/Niantic/Lightship/AR/PersistentAnchors/ARPersistentAnchorManager/")]
    public partial class ARPersistentAnchorManager : ARTrackableManager<
        XRPersistentAnchorSubsystem,
        XRPersistentAnchorSubsystemDescriptor,
        XRPersistentAnchorSubsystem.Provider,
        XRPersistentAnchor,
        ARPersistentAnchor>
    {
        [Tooltip("The GameObject to use when creating an anchor.  If null, a new GameObject will be created.")]
        [SerializeField]
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
        private bool _isInitialLocalization = false;
        private ARPersistentAnchorTelemetrySidecar _telemetrySidecar;

        /// <summary>
        /// The prefab to use when creating an ARPersistentAnchor.  If null, a new GameObject will be created.
        /// </summary>
        protected override GameObject GetPrefab() => _defaultAnchorGameobject;

        [Header("Experimental: Drift Mitigation")]
        [Tooltip("Continue to send localization requests after initial localization is achieved. " +
            "This refines the localization over time and mitigates drift, but consumes more bandwidth")]
        [SerializeField]
        private bool _ContinuousLocalizationEnabled = XRPersistentAnchorConfiguration.DefaultContinuousLocalizationEnabled;

        [Tooltip("Interpolate anchor updates instead of snapping. Only works when continuous localization is enabled")]
        [SerializeField]
        private bool _InterpolationEnabled = XRPersistentAnchorConfiguration.DefaultTransformUpdateSmoothingEnabled;

        /// <summary>
        /// Number of seconds over which anchor interpolation occurs.
        /// Faster times will result in more noticeable movement.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Obsolete]
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
            get => _ContinuousLocalizationEnabled;
            set => _ContinuousLocalizationEnabled = value;
        }

        /// <summary>
        /// Whether to enable or disable legacy MonoBehaviour driven interpolation
        /// It is recommended to use the native solution "TransformUpdateSmoothingEnabled" instead of this property
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool InterpolationEnabled { get; set; } = false;

#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
        [Tooltip("Averages/Fuses multiple localization results to provide a more stable localization. Only works when continuous localization is enabled")]
        [SerializeField]
        private bool _TemporalFusionEnabled = XRPersistentAnchorConfiguration.DefaultTemporalFusionEnabled;

        [Tooltip("JPEG compression quality for localization images. Must be between 1 and 100.")]
        [SerializeField]
        [Range(1,100)]
        private int _JpegCompressionQuality = XRPersistentAnchorConfiguration.DefaultJpegCompressionQuality;

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

        [Header("Experimental: Limited Localizations")]
        [Tooltip("Suppress successful localizations and only report limited localizations. " +
            "This is useful for debugging and testing limited localization flows. " +
            "This is not recommended for production use.")]
        [SerializeField]
        private bool _LimitedLocalizationsOnly =
            XRPersistentAnchorConfiguration.DefaultLimitedLocalizationsOnly;

        /// <summary>
        /// Whether to enable or disable temporal fusion.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool TemporalFusionEnabled
        {
            get => _TemporalFusionEnabled;
            set => _TemporalFusionEnabled = value;
        }

        public int JpegCompressionQuality
        {
            get => _JpegCompressionQuality;
            set
            {
                if (value < 1 || value > 100)
                {
                    throw new ArgumentException("JpegCompressionQuality must be between 1 and 100.");
                }
                _JpegCompressionQuality = value;
            }
        }

        /// <summary>
        /// Whether to enable or disable transform update smoothing
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool TransformUpdateSmoothingEnabled
        {
            get => _InterpolationEnabled;
            set => _InterpolationEnabled = value;
        }

        /// <summary>
        /// Number of seconds between server requests for initial localization. 0 value means as many requests as possible.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public float InitialServiceRequestIntervalSeconds
        {
            get => _InitialServiceRequestIntervalSeconds;
            set => _InitialServiceRequestIntervalSeconds = value;
        }

        /// <summary>
        /// Number of seconds between server requests for continuous localization. 0 value means as many requests as possible.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public float ContinuousServiceRequestIntervalSeconds
        {
            get => _ContinuousServiceRequestIntervalSeconds;
            set => _ContinuousServiceRequestIntervalSeconds = value;
        }

        /// <summary>
        /// Number of localization samples used to fuse
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public uint CloudLocalizationTemporalFusionWindowSize
        {
            get => _CloudLocalizationTemporalFusionWindowSize;
            set => _CloudLocalizationTemporalFusionWindowSize = value;
        }

        /// <summary>
        /// Whether to enable or disable frame diagnostics
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool DiagnosticsEnabled
        {
            get => _DiagnosticsEnabled;
            set => _DiagnosticsEnabled = value;
        }

        /// <summary>
        /// Suppress successful localizations and only report limited localizations.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool LimitedLocalizationsOnly
        {
            get => _LimitedLocalizationsOnly;
            set => _LimitedLocalizationsOnly = value;
        }

        /// <summary>
        /// Defines the interval between localization requests for Slick Localization.
        /// Set to 0 for processing every frame.
        /// </summary>
        public float SlickLocalizationRequestIntervalSeconds { get; set; }
            = XRPersistentAnchorConfiguration.DefaultSlickLocalizationFps == 0
                ? 0
                : 1 / XRPersistentAnchorConfiguration.DefaultSlickLocalizationFps;

        /// <summary>
        /// Defines the size of the temporal fusion window for Slick Localization.
        /// This should be inversely proportional to the SlickLocalizationRequestIntervalSeconds.
        /// </summary>
        public uint SlickLocalizationTemporalFusionWindowSize { get; set; }
            = XRPersistentAnchorConfiguration.DefaultSlickLocalizationTemporalFusionWindowSize;

        /// <summary>
        /// Obsolete. Use CloudLocalizationTemporalFusionWindowSize instead.
        /// </summary>
        [Obsolete]
        public int TemporalFusionSlidingWindow
        {
            get => (int) CloudLocalizationTemporalFusionWindowSize;
            set => CloudLocalizationTemporalFusionWindowSize = (uint)value;
        }
#endif

        /// <summary>
        /// Enable/Disable Cloud Localization
        /// </summary>
        private bool _cloudLocalizationEnabled = XRPersistentAnchorConfiguration.DefaultCloudLocalizationEnabled;
        public bool CloudLocalizationEnabled
        {
            get => _cloudLocalizationEnabled;
            set => _cloudLocalizationEnabled = value;
        }

        /// <summary>
        /// Enable Device Localization
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        private bool _slickLocalizationEnabled = XRPersistentAnchorConfiguration.DefaultSlickLocalizationEnabled;

        public bool SlickLocalizationEnabled
        {
            get => _slickLocalizationEnabled;
            set
            {
                if (!LightshipUnityContext.FeatureEnabled(XRPersistentAnchorSubsystem.SlickLocalizationFeatureFlagName))
                {
                    Log.Warning("Slick Localization feature cannot be enabled. Use feature flag.");
                    return;
                }
                if (subsystem != null && subsystem.running)
                {
                    Log.Warning("Configured SlickLocalizationEnabled while the subsystem is running." +
                        "Stop the subsystem and set the CurrentConfiguration instead.");
                }
                _slickLocalizationEnabled = value;
            }
        }

        /// <summary>
        /// Asynchronously restarts the subsystem with the current configuration.
        /// This will remove all anchors and stop the subsystem before restarting it.
        /// </summary>
        public async Task RestartSubsystemAsync()
        {
            if (subsystem == null)
            {
                return;
            }

            if (subsystem.running)
            {
                subsystem.Stop();
            }

            await Task.Yield();

            subsystem.Start();
        }

        /// <summary>
        /// Asynchronously restarts the subsystem with the current configuration.
        /// This will remove all anchors and stop the subsystem before restarting it.
        /// </summary>
        public IEnumerator RestartSubsystemAsyncCoroutine()
        {
            if (subsystem == null)
            {
                yield break;
            }

            if (subsystem.running)
            {
                subsystem.Stop();
            }

            yield return null;

            subsystem.Start();
        }

        internal GameObject DefaultAnchorPrefab
        {
            get => _defaultAnchorGameobject;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Instance)
            {
                Log.Error(
                    $"{nameof(ARPersistentAnchorManager)} already has a singleton reference.  "
                    + "Each scene should only have one {nameof(ARPersistentAnchorManager)}." +
                    gameObject);
            }
            else
            {
                Instance = this;
            }

            InitializeTelemetry();
        }

        protected override void OnBeforeStart()
        {
            // We capture the current subsystem configurations so we don't overwrite unwanted configs with default
            XRPersistentAnchorConfiguration cfg = new(subsystem.CurrentConfiguration);

            cfg.CloudLocalizationEnabled = CloudLocalizationEnabled;
            cfg.ContinuousLocalizationEnabled = ContinuousLocalizationEnabled;
            cfg.TransformUpdateSmoothingEnabled = _InterpolationEnabled;

#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
            cfg.TemporalFusionEnabled = TemporalFusionEnabled;
            cfg.CloudLocalizerInitialRequestsPerSecond = InitialServiceRequestIntervalSeconds == 0
                ? 0
                : 1 / InitialServiceRequestIntervalSeconds;

            cfg.CloudLocalizerContinuousRequestsPerSecond =
                ContinuousServiceRequestIntervalSeconds == 0
                    ? 0
                    : 1 / ContinuousServiceRequestIntervalSeconds;
            cfg.CloudLocalizationTemporalFusionWindowSize = CloudLocalizationTemporalFusionWindowSize;
            cfg.DiagnosticsEnabled = DiagnosticsEnabled;
            cfg.LimitedLocalizationsOnly = LimitedLocalizationsOnly;
            cfg.JpegCompressionQuality = _JpegCompressionQuality;
            cfg.SlickLocalizationEnabled = SlickLocalizationEnabled;
            cfg.SlickLocalizationFps = SlickLocalizationRequestIntervalSeconds == 0
                ? 0
                : 1 / SlickLocalizationRequestIntervalSeconds;
            cfg.SlickLocalizationTemporalFusionWindowSize = SlickLocalizationTemporalFusionWindowSize;
#endif

            subsystem.CurrentConfiguration = cfg;
        }

        protected virtual void Start()
        {
            if (subsystem != null)
            {
                subsystem.OnSubsystemStop += OnSubsystemStop;
                subsystem.OnBeforeSubsystemStart += OnBeforeSubsystemStart;
            }

            RequestLocationPermission();
        }

        private void OnBeforeSubsystemStart()
        {
            // Override native config with manager's settings. Any changes made to subsystem's config
            // will be reflected to the manager's settings, so this only captures new changes
            OnBeforeStart();
        }

        protected override void OnDisable()
        {
            if (Instance != this)
            {
                Log.Error($"Each scene should only have one {nameof(ARPersistentAnchorManager)}." + gameObject);
            }
            else
            {
                Instance = null;
            }

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (subsystem != null)
            {
                subsystem.OnSubsystemStop -= OnSubsystemStop;
                subsystem.OnBeforeSubsystemStart -= OnBeforeSubsystemStart;

                // There is a weird behavior on Android that skips OnDisable
                // We explicitly call subsystem.Stop() here to ensure the proper state of subsystem
                if (subsystem.running)
                {
                    subsystem.Stop();
                }
            }

            RemoveAllAnchorsImmediate();

            base.OnDestroy();
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
            if (subsystem == null)
            {
                vpsSessionId = null;
                return false;
            }

            return subsystem.GetVpsSessionId(out vpsSessionId);
        }

        /// <summary>
        /// Creates Anchor from Pose
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        /// <param name="anchorLocalPose">The localToCamera of the anchor to create</param>
        /// <param name="arPersistentAnchor">The ARPersistentAnchor that was created from the pose</param>
        /// <returns>
        /// <c>True</c> If the anchor was successfully crated/added for tracking.
        /// <c>False</c> The anchor cannot be crated/added, it is either already crated/added
        /// </returns>
        public bool TryCreateAnchor(Pose anchorLocalPose, out ARPersistentAnchor arPersistentAnchor)
        {
            if (!LightshipUnityContext.FeatureEnabled(XRPersistentAnchorSubsystem.SlickLocalizationFeatureFlagName))
            {
                arPersistentAnchor = default;
                return false;
            }
            if (subsystem == null || !subsystem.running)
            {
                arPersistentAnchor = default;
                return false;
            }
            bool success = subsystem.TryAddAnchor(anchorLocalPose, out var xrPersistentAnchor);
            if (success)
            {
                arPersistentAnchor = CreateTrackableImmediate(xrPersistentAnchor);
            }
            else
            {
                Log.Error("Failed to create anchor.");
                arPersistentAnchor = default;
            }

            return success;
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
            if (subsystem == null || !subsystem.running)
            {
                arPersistentAnchor = default;
                return false;
            }

            var data = payload.Data;
            var dataNativeArray = new NativeArray<byte>(data, Allocator.Temp);
            IntPtr payloadIntPtr;
            unsafe
            {
                payloadIntPtr = (IntPtr)dataNativeArray.GetUnsafeReadOnlyPtr();
            }

            int payloadSize = payload.Data.Length;
            var xrPersistentAnchorPayload = new XRPersistentAnchorPayload(payloadIntPtr, payloadSize);
            bool success = subsystem.TryLocalize(xrPersistentAnchorPayload, out var xrPersistentAnchor);
            if (success)
            {
                arPersistentAnchor = CreateTrackableImmediate(xrPersistentAnchor);
            }
            else
            {
                Log.Error("Failed to localize." + gameObject);
                arPersistentAnchor = default;
            }

            return success;
        }

        /// <summary>
        /// Destroys an anchor and stop tracking it.
        /// </summary>
        /// <param name="arPersistentAnchor">The anchor to destroy</param>
        public void DestroyAnchor(ARPersistentAnchor arPersistentAnchor)
        {
            if (subsystem == null)
            {
                return;
            }

            var trackableId = arPersistentAnchor.trackableId;
            bool success = subsystem.TryRemoveAnchor(trackableId);
            if (PendingAdds.ContainsKey(trackableId))
            {
                PendingAdds.Remove(trackableId);
            }
            if (success)
            {
                if (arPersistentAnchor._markedForDestruction)
                {
                    Trackables.Remove(trackableId);
                    ReportRemovedAnchors(arPersistentAnchor);
                }
                else
                {
                    arPersistentAnchor._markedForDestruction = true;
                }
            }
            else if (subsystem.running)
            {
                Log.Error($"Failed to destroy anchor {trackableId}." + gameObject);
            }
        }

        // Force the ARTrackableManager to query into native and get updates
        // @note Should only be called on the main thread because this could interact with GameObjects
        internal void ForceUpdate()
        {
            this.Update();
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

                if (InterpolationEnabled && !addedAnchor.Interpolator)
                {
                    addedAnchor.gameObject.AddComponent<ARPersistentAnchorInterpolator>();
                }

                HandleTelemetryForAddedAnchor(addedAnchor);
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

                if (InterpolationEnabled)
                {
                    // Start interpolation with the previous cached pose, before updating it
                    // With temporal fusion, this will set the pose back to the previous fused pose
                    updatedAnchor.ApplyPoseForInterpolation
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

                HandleTelemetryForUpdatedAnchor(updatedAnchor);

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
                removedAnchor.trackingConfidenceOverride = 0.0f;
                _arPersistentAnchorStates.Remove(removedAnchor);
                var arPersistentAnchorStateChangedEvent = new ARPersistentAnchorStateChangedEventArgs(removedAnchor);
                arPersistentAnchorStateChanged?.Invoke(arPersistentAnchorStateChangedEvent);
            }

            if (removedAnchors.Length == 0)
            {
                return;
            }

            // If the last anchor is removed, emit a session ended event
            if (Trackables.Count == 0)
            {
                HandleTelemetryForSessionEnd();
                subsystem?.ResetTelemetryMetrics();
            }
        }

        private void InitializeTelemetry()
        {
            if (_telemetrySidecar == null)
            {
                _telemetrySidecar = new ARPersistentAnchorTelemetrySidecar(this);
            }
        }

        private string VpsSessionId
        {
            get
            {
                string id;

                GetVpsSessionId(out id);
                return id;
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

        private void HandleTelemetryForUpdatedAnchor(ARPersistentAnchor updatedAnchor)
        {
            // If this is a mock, don't emit telemetry
            if (subsystem == null || subsystem.IsMockProvider)
            {
                return;
            }

            // If the anchor is newly tracking, and this is the first localization, emit a localization success event
            if (updatedAnchor.trackingState == TrackingState.Tracking &&
                !_isInitialLocalization)
            {
                _telemetrySidecar.LocalizationSuccess(updatedAnchor);
                _isInitialLocalization = true;
            }
            // If the anchor is newly tracking, and this is not the first localization, emit a tracking regained event
            else if (updatedAnchor.trackingState == TrackingState.Tracking &&
                      _arPersistentAnchorStates[updatedAnchor] == TrackingState.None)
            {
                _telemetrySidecar.TrackingRegained();
            }
            // If the anchor is newly lost, emit a tracking lost event
            else if (updatedAnchor.trackingState == TrackingState.None &&
                     _arPersistentAnchorStates[updatedAnchor] == TrackingState.Tracking)
            {
                _telemetrySidecar.TrackingLost();
            }
        }

        private void HandleTelemetryForAddedAnchor(ARPersistentAnchor addedAnchor)
        {
            // If this is a mock, don't emit telemetry
            if (subsystem == null || subsystem.IsMockProvider)
            {
                return;
            }

            _telemetrySidecar.AnchorAdded(addedAnchor);
        }

        private void RemoveAllAnchorsImmediate()
        {
            var trackables = m_Trackables.Values.ToArray();
            foreach (var trackable in trackables)
            {
                trackable._markedForDestruction = true;
                // Removes it from ARTrackableManager and ARSubsystem collections
                // Fires removal events
                DestroyAnchor(trackable);

                // Destroys the anchor GameObject
                Destroy(trackable.gameObject);
            }
        }

        private void OnSubsystemStop()
        {
            RemoveAllAnchorsImmediate();
            HandleTelemetryForSessionEnd();
        }

        private void HandleTelemetryForSessionEnd()
        {
            _isInitialLocalization = false;

            if (subsystem == null || subsystem.IsMockProvider)
            {
                return;
            }

            _telemetrySidecar.SessionEnded();
        }
    }
}
