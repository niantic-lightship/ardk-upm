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
using Niantic.Lightship.AR.Mapping;
using Niantic.Lightship.AR.Subsystems.PersistentAnchor;
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
        /// Each invocation of this event contains a single Persistent Anchor that has had a state or pose change this frame.
        /// Query the arg's arPersistentAnchor's TrackingState to determine its new TrackingState.
        /// Query the arPersistentAnchor's PredictedPose to determine its new PredictedPose.
        /// </summary>
        public event Action<ARPersistentAnchorStateChangedEventArgs> arPersistentAnchorStateChanged;

        /// <summary>
        /// Called when debug info is available
        ///
        /// Each invocation of this event contains a XRPersistentAnchorDebugInfo object
        /// that contains arrays of XRPersistentAnchorNetworkRequestStatus, XRPersistentAnchorLocalizationStatus,
        /// and XRPersistentAnchorFrameDiagnostics
        /// </summary>
        public event Action<XRPersistentAnchorDebugInfo> DebugInfoUpdated;

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

        [Tooltip("Continue to send localization requests after initial localization is achieved. " +
            "This refines the localization over time and mitigates drift, but consumes more bandwidth")]
        [SerializeField]
        private bool _ContinuousLocalizationEnabled = XRPersistentAnchorConfiguration.DefaultContinuousLocalizationEnabled;

        [Tooltip("Interpolate anchor updates instead of snapping. Only works when continuous localization is enabled")]
        [SerializeField]
        private bool _InterpolationEnabled = XRPersistentAnchorConfiguration.DefaultTransformUpdateSmoothingEnabled;

        [Tooltip("Averages/Fuses multiple localization results to provide a more stable localization. Only works when continuous localization is enabled")]
        [SerializeField]
        private bool _TemporalFusionEnabled = XRPersistentAnchorConfiguration.DefaultTemporalFusionEnabled;

        [Tooltip("JPEG compression quality for localization images. Must be between 1 and 100. This only applies to cloud localization.")]
        [SerializeField]
        [Range(1,100)]
        private int _JpegCompressionQuality = XRPersistentAnchorConfiguration.DefaultJpegCompressionQuality;

        [Tooltip("Number of seconds between server requests for initial localization. 0 value means as many requests as possible.")]
        [SerializeField]
        private float _InitialServiceRequestIntervalSeconds = XRPersistentAnchorConfiguration.DefaultCloudLocalizerInitialRequestsPerSecond.ZeroOrReciprocal();

        [Tooltip("Number of seconds between server requests for continuous localization. 0 value means as many requests as possible.")]
        [SerializeField]
        private float _ContinuousServiceRequestIntervalSeconds = XRPersistentAnchorConfiguration.DefaultCloudLocalizerContinuousRequestsPerSecond.ZeroOrReciprocal();

        [Tooltip("Whether to enable or disable frame diagnostics")]
        [SerializeField]
        private bool _DiagnosticsEnabled = XRPersistentAnchorConfiguration.DefaultDiagnosticsEnabled;

        [Tooltip("Automatically tune VPS parameters based on preset configurations")]
        [SerializeField]
        private LightshipVpsUsageUtility.LightshipVpsUsageMode _VpsUsageMode = LightshipVpsUsageUtility.LightshipVpsUsageMode.Default;

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
        /// </summary>
        public bool ContinuousLocalizationEnabled
        {
            get => _ContinuousLocalizationEnabled;
            set => _ContinuousLocalizationEnabled = value;
        }

        /// <summary>
        /// Whether to enable or disable legacy MonoBehaviour driven interpolation
        /// It is recommended to use the native solution "TransformUpdateSmoothingEnabled" instead of this property
        /// </summary>
        public bool InterpolationEnabled { get; set; } = false;

        /// <summary>
        /// Whether to enable or disable temporal fusion.
        /// Fusion will average multiple localization results to provide a more stable localization.
        /// </summary>
        public bool TemporalFusionEnabled
        {
            get => _TemporalFusionEnabled;
            set => _TemporalFusionEnabled = value;
        }

        /// <summary>
        /// Defines the JPEG compression quality for localization images.
        /// This only applies to cloud localization.
        /// Lower compression qualities will result in less bandwidth usage
        /// It is recommended to keep this above 20 to avoid localization quality loss
        /// </summary>
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
        /// </summary>
        public bool TransformUpdateSmoothingEnabled
        {
            get => _InterpolationEnabled;
            set => _InterpolationEnabled = value;
        }

        /// <summary>
        /// Number of seconds between server requests for initial localization. 0 value means as many requests as possible.
        /// </summary>
        public float InitialServiceRequestIntervalSeconds
        {
            get => _InitialServiceRequestIntervalSeconds;
            set => _InitialServiceRequestIntervalSeconds = value;
        }

        /// <summary>
        /// Number of seconds between server requests for continuous localization. 0 value means as many requests as possible.
        /// </summary>
        public float ContinuousServiceRequestIntervalSeconds
        {
            get => _ContinuousServiceRequestIntervalSeconds;
            set => _ContinuousServiceRequestIntervalSeconds = value;
        }

        /// <summary>
        /// Whether to enable or disable frame diagnostics
        /// Listen to the DebugInfoUpdated event to get the diagnostics data
        /// </summary>
        public bool DiagnosticsEnabled
        {
            get => _DiagnosticsEnabled;
            set => _DiagnosticsEnabled = value;
        }

#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
        [Tooltip("Synchronize the fusion window size with the continuous service request interval")]
        [SerializeField]
        private bool _SyncFusionWindow = true;

        [Tooltip("Number of localization samples used to fuse. This value should account for the rate of localization requests. " +
            "By default, it is recommended to cache around 5 seconds of localization samples to fuse")]
        [SerializeField]
        private uint _CloudLocalizationTemporalFusionWindowSize = XRPersistentAnchorConfiguration.DefaultCloudLocalizationTemporalFusionWindowSize;

        [Tooltip("Suppress successful localizations and only report limited localizations. " +
            "This is useful for debugging and testing limited localization flows. " +
            "This is not recommended for production use.")]
        [SerializeField]
        private bool _LimitedLocalizationsOnly =
            XRPersistentAnchorConfiguration.DefaultLimitedLocalizationsOnly;

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
        /// Suppress successful localizations and only report limited localizations.
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        public bool LimitedLocalizationsOnly
        {
            get => _LimitedLocalizationsOnly;
            set => _LimitedLocalizationsOnly = value;
        }

        /// <summary>
        /// Defines the size of the temporal fusion window for Device Mapping Localization.
        /// This should be inversely proportional to the DeviceMappingLocalizationRequestIntervalSeconds.
        /// </summary>
        [Experimental]
        public uint DeviceMappingLocalizationTemporalFusionWindowSize { get; set; }
            = XRPersistentAnchorConfiguration.DefaultDeviceMappingLocalizationTemporalFusionWindowSize;

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
        /// Defines the interval between localization requests for Device Mapping Localization.
        /// Set to 0 for processing every frame.
        /// </summary>
        [Experimental]
        public float DeviceMappingLocalizationRequestIntervalSeconds { get; set; }
            = XRPersistentAnchorConfiguration.DefaultDeviceMappingLocalizationFps.ZeroOrReciprocal();

        /// <summary>
        /// Enable/Disable Cloud Localization
        /// </summary>
        private bool _cloudLocalizationEnabled = XRPersistentAnchorConfiguration.DefaultCloudLocalizationEnabled;
        public bool CloudLocalizationEnabled
        {
            get => _cloudLocalizationEnabled;
            set => _cloudLocalizationEnabled = value;
        }

        private bool _deviceMappingLocalizationEnabled = XRPersistentAnchorConfiguration.DefaultDeviceMappingLocalizationEnabled;

        /// <summary>
        /// Enable Device Localization
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public bool DeviceMappingLocalizationEnabled
        {
            get => _deviceMappingLocalizationEnabled;
            set
            {
                _deviceMappingLocalizationEnabled = value;
            }
        }

        private DeviceMappingType _deviceMappingType = XRPersistentAnchorConfiguration.DefaultDeviceMappingType;

        /// <summary>
        /// Enable Learned Features in Device Localization
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public DeviceMappingType DeviceMappingType
        {
            get => _deviceMappingType;
            set
            {
                _deviceMappingType = value;
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
            cfg.TemporalFusionEnabled = TemporalFusionEnabled;

            cfg.JpegCompressionQuality = _JpegCompressionQuality;
            cfg.CloudLocalizerInitialRequestsPerSecond = InitialServiceRequestIntervalSeconds.ZeroOrReciprocal();
            cfg.CloudLocalizerContinuousRequestsPerSecond = ContinuousServiceRequestIntervalSeconds.ZeroOrReciprocal();

            cfg.DiagnosticsEnabled = DiagnosticsEnabled;

            cfg.DeviceMappingLocalizationEnabled = DeviceMappingLocalizationEnabled;
            cfg.DeviceMappingType = DeviceMappingType;
            cfg.DeviceMappingLocalizationFps = DeviceMappingLocalizationRequestIntervalSeconds.ZeroOrReciprocal();

#if NIANTIC_ARDK_EXPERIMENTAL_FEATURES
            if (_SyncFusionWindow)
            {
                cfg.CloudLocalizationTemporalFusionWindowSize =
                    XRPersistentAnchorConfiguration.DetermineFusionWindowFromRequestRate
                        (cfg.CloudLocalizerContinuousRequestsPerSecond);
            }
            else
            {
                cfg.CloudLocalizationTemporalFusionWindowSize = CloudLocalizationTemporalFusionWindowSize;
            }

            cfg.LimitedLocalizationsOnly = LimitedLocalizationsOnly;
            cfg.DeviceMappingLocalizationTemporalFusionWindowSize = DeviceMappingLocalizationTemporalFusionWindowSize;
#endif

            subsystem.CurrentConfiguration = cfg;
        }

        protected virtual void Start()
        {
            if (subsystem != null)
            {
                subsystem.OnSubsystemStop += OnSubsystemStop;
                subsystem.OnBeforeSubsystemStart += OnBeforeSubsystemStart;
                subsystem.debugInfoProvided += OnDebugInfoUpdated;
            }

            RequestLocationPermission();
        }

        private void OnDebugInfoUpdated(XRPersistentAnchorDebugInfo args)
        {
            try
            {
                DebugInfoUpdated?.Invoke(args);
            }
            catch (Exception e)
            {
                Log.Error("Error invoking DebugInfoUpdated event: " + e);
            }
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
                subsystem.debugInfoProvided -= OnDebugInfoUpdated;

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
