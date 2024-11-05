// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SubsystemsImplementation;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Base class for a persistent anchor subsystem.
    /// </summary>
    /// <remarks>
    /// <para>An anchor is a pose in the physical environment that is tracked by an XR device.
    /// As the device refines its understanding of the environment, anchors will be
    /// updated, allowing you to keep virtual content connected to a real-world position and orientation.</para>
    /// <para>This abstract class should be implemented by an XR provider and instantiated using the <c>SubsystemManager</c>
    /// to enumerate the available <see cref="XRPersistentAnchorSubsystemDescriptor"/>s.</para>
    /// </remarks>
    [PublicAPI]
    public class XRPersistentAnchorSubsystem
        : TrackingSubsystem<XRPersistentAnchor, XRPersistentAnchorSubsystem, XRPersistentAnchorSubsystemDescriptor,
            XRPersistentAnchorSubsystem.Provider>
    {

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private ValidationUtility<XRPersistentAnchor> m_ValidationUtility;
#endif

        /// <summary>
        /// Constructor. Do not invoke directly; use the <c>SubsystemManager</c>
        /// to enumerate the available <see cref="XRPersistentAnchorSubsystemDescriptor"/>s
        /// and call <c>Create</c> on the desired descriptor.
        /// </summary>
        public XRPersistentAnchorSubsystem()
        {

        }

        /// <summary>
        /// Get or set configuration with <paramref> <name>XRPersistentAnchorConfiguration</name>
        ///
        /// @note This api calls into native, so getting or setting the configuration will return a deep copy
        /// Updated configurations need to be set to take effect
        /// </paramref>
        /// </summary>
        public XRPersistentAnchorConfiguration CurrentConfiguration
        {
            // XRPersistentAnchorConfiguration is a reference type, so make a deep copy to avoid confusion
            get => new(provider.CurrentConfiguration);
            set
            {
                if (value == null)
                {
                    Log.Debug("Applied a null configuration, ignoring");
                    return;
                }

                // XRPersistentAnchorConfiguration is a reference type, so make a deep copy to avoid confusion
                //  The params are going down into native when this is set, so we don't want to change them after this point
                provider.CurrentConfiguration = new(value);

                OnConfigurationChanged?.Invoke(value);
            }
        }

        // Overloaded start to invoke the OnBeforeSubsystemStart event
        // Different from base OnBeforeStart that is only invoked on MonoBehaviour OnEnable
        public new void Start()
        {
            OnBeforeSubsystemStart?.Invoke();
            base.Start();
        }

        protected override void OnStart()
        {
            base.OnStart();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            m_ValidationUtility = new();
#endif
        }

        // One case of Vps Session ending
        protected override void OnStop()
        {
            base.OnStop();
            OnSubsystemStop?.Invoke();
            ResetTelemetryMetrics();
        }

        public bool IsMockProvider => provider.IsMockProvider;

        /// <summary>
        /// Called when debug info is available
        ///
        /// Each invocation of this event contains a XRPersistentAnchorDebugInfo object
        /// that contains arrays of XRPersistentAnchorNetworkRequestStatus, XRPersistentAnchorLocalizationStatus,
        /// and XRPersistentAnchorFrameDiagnostics
        /// </summary>
        public event Action<XRPersistentAnchorDebugInfo> debugInfoProvided;

        /// <summary>
        /// Called when the subsystem's configuration changes
        /// </summary>
        public event Action<XRPersistentAnchorConfiguration> OnConfigurationChanged;

        /// <summary>
        /// Get the changes to anchors (added, updated, and removed) since the last call
        /// to <see cref="GetChanges(Allocator)"/>.
        /// </summary>
        /// <param name="allocator">An allocator to use for the <c>NativeArray</c>s in <see cref="TrackableChanges{T}"/>.</param>
        /// <returns>Changes since the last call to <see cref="GetChanges"/>.</returns>
        public override TrackableChanges<XRPersistentAnchor> GetChanges(Allocator allocator)
        {
            if (!running)
                throw new InvalidOperationException(
                    "Can't call \"GetChanges\" without \"Start\"ing the persistent anchor subsystem!");

            var changes = provider.GetChanges(XRPersistentAnchor.defaultValue, allocator);
            var gotNetworkStatus = provider.GetNetworkStatusUpdate(out var networkStatuses);
            if (gotNetworkStatus)
            {
                foreach (var status in networkStatuses)
                {
                    // On success or fail, increment request count. Don't count pending
                    if (status.Status == RequestStatus.Successful || status.Status == RequestStatus.Failed)
                    {
                        // Defaults to 0 if not present
                        NetworkErrorCodeCounts.TryGetValue(status.Error, out var count);
                        // Increment then replace
                        NetworkErrorCodeCounts[status.Error] = ++count;

                        NumberServerRequests++;
                    }
#if ARDK_DEBUG_LOG_ENABLED
                    Log.Info($"Persistent Anchor request {status.RequestId} for {status.Type} got a status {status.Status}," +
                        $" with an error {status.Error} and RTT of {Math.Max(status.EndTimeMs, status.StartTimeMs) - status.StartTimeMs}ms");
#endif
                }
            }

            var gotLocalizationStatus = provider.GetLocalizationStatusUpdate(out var localizationStatuses);
            if (gotLocalizationStatus)
            {
                foreach (var status in localizationStatuses)
                {
                    Log.Info($"Localization got a result of {status.Status} with LocalizationConfidence {status.LocalizationConfidence}");
                }
            }

            var gotDiagnostics = provider.GetFrameDiagnosticsUpdate(out var diagnosticsArray);
            if (gotDiagnostics)
            {
                // TODO: How do we want to expose diagnostics?
            }

            // We send debug info out
            bool debugInfoIsAvailable = gotNetworkStatus || gotLocalizationStatus || gotDiagnostics;
            if (debugInfoIsAvailable)
            {
                var debugInfo = new XRPersistentAnchorDebugInfo(networkStatuses, localizationStatuses, diagnosticsArray);
                debugInfoProvided?.Invoke(debugInfo);
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            m_ValidationUtility.ValidateAndDisposeIfThrown(changes);
#endif
            return changes;
        }

        /// <summary>
        /// Attempts to create a new anchor with the provide <paramref name="pose"/>.
        /// </summary>
        /// <param name="pose">The pose, in session space, of the new anchor.</param>
        /// <param name="anchor">The new anchor. Only valid if this method returns <c>true</c>.</param>
        /// <returns><c>true</c> if the new anchor was added, otherwise <c>false</c>.</returns>
        public bool TryAddAnchor(Pose pose, out XRPersistentAnchor anchor)
        {
            return provider.TryAddAnchor(pose, out anchor);
        }

        /// <summary>
        /// Attempts to remove an existing anchor with <see cref="TrackableId"/> <paramref name="anchorId"/>.
        /// </summary>
        /// <param name="anchorId">The id of an existing anchor to remove.</param>
        /// <returns><c>true</c> if the anchor was removed, otherwise <c>false</c>.</returns>
        public bool TryRemoveAnchor(TrackableId anchorId)
        {
            return provider.TryRemoveAnchor(anchorId);
        }

        /// <summary>
        /// Tries to restore an anchor
        /// </summary>
        /// <param name="anchorPayload">The payload to restore the anchor with</param>
        /// <param name="anchor">The restored anchor</param>
        /// <returns>Whether or not the restoration was successful</returns>
        public bool TryRestoreAnchor(XRPersistentAnchorPayload anchorPayload, out XRPersistentAnchor anchor)
        {
            return provider.TryRestoreAnchor(anchorPayload, out anchor);
        }

        public bool TryLocalize(XRPersistentAnchorPayload anchorPayload, out XRPersistentAnchor anchor)
        {
            return provider.TryLocalize(anchorPayload, out anchor);
        }

        /// <summary>
        /// Get the vps session id, if any
        /// </summary>
        /// <param name="vpsSessionId">The vps session id as 32 character hexidecimal upper-case string.</param>
        /// <returns>True if vps session id is present, false otherwise</returns>
        public bool GetVpsSessionId(out string vpsSessionId)
        {
            var success = provider.GetVpsSessionId(out vpsSessionId);
            return success;
        }

        // Invoked when the subsystem is stopped
        internal event Action OnSubsystemStop;

        // Invoked when the subsystem is about to start. This is different from OnBeforeStart,
        // which is only invoked on MonoBehaviour OnEnable
        internal event Action OnBeforeSubsystemStart;

        internal int NumberServerRequests { get; private set; } = 0;

        // If we discover that the vps session has reset, we should reset the number of server requests
        internal void ResetTelemetryMetrics()
        {
            NumberServerRequests = 0;
            NetworkErrorCodeCounts.Clear();
        }

        internal readonly Dictionary<ErrorCode, int> NetworkErrorCodeCounts = new();

        /// <summary>
        /// An abstract class to be implemented by providers of this subsystem.
        /// </summary>
        public abstract class Provider : SubsystemProvider<XRPersistentAnchorSubsystem>
        {
            public virtual bool IsMockProvider { get; } = false;

            /// <summary>
            /// Get or set configuration with <paramref> <name>XRPersistentAnchorConfiguration</name>
            /// </paramref>
            /// </summary>
            public virtual XRPersistentAnchorConfiguration CurrentConfiguration { get; set; }

            /// <summary>
            /// Invoked to get the changes to anchors (added, updated, and removed) since the last call to
            /// <see cref="GetChanges(XRPersistentAnchor,Allocator)"/>.
            /// </summary>
            /// <param name="defaultAnchor">The default anchor. This should be used to initialize the returned
            /// <c>NativeArray</c>s for backwards compatibility.
            /// See <see cref="Allocator"/>.
            /// </param>
            /// <param name="allocator">An allocator to use for the <c>NativeArray</c>s in <see cref="TrackableChanges{T}"/>.</param>
            /// <returns>Changes since the last call to <see cref="GetChanges"/>.</returns>
            public abstract TrackableChanges<XRPersistentAnchor> GetChanges(XRPersistentAnchor defaultAnchor,
                Allocator allocator);

            /// <summary>
            /// Get a list of network status updates, if any
            /// </summary>
            /// <returns>True if an update is present, false otherwise</returns>
            public abstract bool GetNetworkStatusUpdate(out XRPersistentAnchorNetworkRequestStatus[] statuses);

            /// <summary>
            /// Get a list of localization status updates, if any
            /// </summary>
            /// <returns>True if an update is present, false otherwise</returns>
            public abstract bool GetLocalizationStatusUpdate(out XRPersistentAnchorLocalizationStatus[] statuses);

            /// <summary>
            /// Get a list of frame diagnostics updates, if any
            /// </summary>
            /// <returns>True if an update is present, false otherwise</returns>
            public abstract bool GetFrameDiagnosticsUpdate(out XRPersistentAnchorFrameDiagnostics[] statuses);

            /// <summary>
            /// Get the vps session id, if any
            /// </summary>
            /// <param name="vpsSessionId">The vps session id as 32 character hexidecimal upper-case string.</param>
            /// <returns>True if vps session id is present, false otherwise</returns>
            public virtual bool GetVpsSessionId(out string vpsSessionId)
            {
                vpsSessionId = default;
                return false;
            }

            /// <summary>
            /// Should create a new anchor with the provided <paramref name="pose"/>.
            /// </summary>
            /// <param name="pose">The pose, in session space, of the new anchor.</param>
            /// <param name="anchor">The new anchor. Must be valid only if this method returns <c>true</c>.</param>
            /// <returns>Should return <c>true</c> if the new anchor was added, otherwise <c>false</c>.</returns>
            public virtual bool TryAddAnchor(Pose pose, out XRPersistentAnchor anchor)
            {
                anchor = XRPersistentAnchor.defaultValue;
                return false;
            }

            /// <summary>
            /// Should remove an existing anchor with <see cref="TrackableId"/> <paramref name="anchorId"/>.
            /// </summary>
            /// <param name="anchorId">The id of an existing anchor to remove.</param>
            /// <returns>Should return <c>true</c> if the anchor was removed, otherwise <c>false</c>. If the anchor
            /// does not exist, return <c>false</c>.</returns>
            public virtual bool TryRemoveAnchor(TrackableId anchorId) => false;

            /// <summary>
            /// Tries to restore an anchor
            /// </summary>
            /// <param name="anchorPayload">The payload to restore the anchor with</param>
            /// <param name="anchor">The restored anchor</param>
            /// <returns>Whether or not the restoration was successful</returns>
            public virtual bool TryRestoreAnchor(XRPersistentAnchorPayload anchorPayload, out XRPersistentAnchor anchor)
            {
                anchor = XRPersistentAnchor.defaultValue;
                return false;
            }

            public virtual bool TryLocalize(XRPersistentAnchorPayload anchorPayload, out XRPersistentAnchor anchor)
            {
                anchor = XRPersistentAnchor.defaultValue;
                return false;
            }
        }
    }
}
