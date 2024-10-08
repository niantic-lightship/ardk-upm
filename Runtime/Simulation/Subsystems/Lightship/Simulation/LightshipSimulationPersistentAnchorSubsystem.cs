// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.PersistentAnchors;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;
using UnityEngine.XR.ARSubsystems;

using Random = System.Random;

namespace Niantic.Lightship.AR.Simulation
{
    public class LightshipSimulationPersistentAnchorSubsystem :
        XRPersistentAnchorSubsystem
    {
        private const string SubsystemId = "Lightship-Simulation-PersistentAnchor";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor()
        {
            var cinfo = new XRPersistentAnchorSubsystemDescriptor.Cinfo
            {
                id = SubsystemId,
                providerType = typeof(LightshipSimulationProvider),
                subsystemTypeOverride = typeof(LightshipSimulationPersistentAnchorSubsystem),
                supportsTrackableAttachments = true
            };

            XRPersistentAnchorSubsystemDescriptor.Create(cinfo);
        }

        private class LightshipSimulationProvider : Provider
        {
            private bool _started;

            private readonly List<XRPersistentAnchor> _addedList = new List<XRPersistentAnchor>();
            private readonly List<XRPersistentAnchor> _updateList = new List<XRPersistentAnchor>();
            private readonly List<TrackableId> _removedList = new List<TrackableId>();
            private readonly List<NativeArray<byte>> _payloads = new List<NativeArray<byte>>();
            private Guid _sessionId;
            private readonly LightshipSimulationPersistentAnchorParams _persistentAnchorParams;
            private readonly Random _random = new Random();
            private readonly List<XRPersistentAnchor> _anchors = new List<XRPersistentAnchor>();

            public override XRPersistentAnchorConfiguration CurrentConfiguration { get; set; } = new XRPersistentAnchorConfiguration();

            public override bool IsMockProvider
            {
                get => true;
            }

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipSimulationProvider()
            {
                Log.Debug($"{nameof(LightshipSimulationPersistentAnchorSubsystem)}.{nameof(LightshipSimulationProvider)} ctor");

                _sessionId = Guid.NewGuid();
                _persistentAnchorParams =
                    LightshipSettingsHelper.ActiveSettings.LightshipSimulationParams.SimulationPersistentAnchorParams;
            }

            public override void Start()
            {
                _started = true;
            }

            public override void Stop()
            {
                _started = false;
                foreach (var payload in _payloads)
                {
                   payload.Dispose();
                }
                _payloads.Clear();

                foreach (var anchor in _anchors)
                {
                    _removedList.Add(anchor.trackableId);
                }

                _anchors.Clear();
            }

            public override void Destroy()
            {
                _started = false;
            }

            [Obsolete("This method does not do anything")]
            public void Configure(IntPtr persistentAnchorApiHandle)
            {
            }

            public override TrackableChanges<XRPersistentAnchor> GetChanges
            (
                XRPersistentAnchor defaultAnchor,
                Allocator allocator
            )
            {
                var added = new NativeArray<XRPersistentAnchor>(_addedList.Count, Allocator.Temp);
                var updated = new NativeArray<XRPersistentAnchor>
                (
                    _updateList.Count,
                    Allocator.Temp
                );

                var removed = new NativeArray<TrackableId>(_removedList.Count, Allocator.Temp);

                for (var i = 0; i < _addedList.Count; i++)
                {
                    added[i] = _addedList[i];
                }

                for (var i = 0; i < _updateList.Count; i++)
                {
                    updated[i] = _updateList[i];
                }

                for (var i = 0; i < _removedList.Count; i++)
                {
                    removed[i] = _removedList[i];
                }

                var changes = TrackableChanges<XRPersistentAnchor>.CopyFrom
                (
                    added,
                    updated,
                    removed,
                    allocator
                );

                _addedList.Clear();
                _updateList.Clear();
                _removedList.Clear();
                return changes;
            }

            public override bool GetNetworkStatusUpdate
            (
                out XRPersistentAnchorNetworkRequestStatus[] statuses
            )
            {
                statuses = default;
                return false;
            }

            public override bool GetLocalizationStatusUpdate
            (
                out XRPersistentAnchorLocalizationStatus[] statuses
            )
            {
                statuses = default;
                return false;
            }

            public override bool GetFrameDiagnosticsUpdate
            (
                out XRPersistentAnchorFrameDiagnostics[] statuses
            )
            {
                statuses = default;
                return false;
            }

            public override bool GetVpsSessionId(out string vpsSessionId)
            {
                vpsSessionId = _sessionId.ToString("N").ToUpper();
                return true;
            }

            public override bool TryAddAnchor(Pose pose, out XRPersistentAnchor anchor)
            {
                anchor = default;
                return false;
            }

            public override bool TryRemoveAnchor(TrackableId anchorId)
            {
                _removedList.Add(anchorId);
                return true;
            }

            public override bool TryRestoreAnchor
            (
                XRPersistentAnchorPayload anchorPayload,
                out XRPersistentAnchor anchor
            )
            {
                return TryLocalize(anchorPayload, out anchor);
            }

            public override bool TryLocalize
            (
                XRPersistentAnchorPayload anchorPayload,
                out XRPersistentAnchor anchor
            )
            {
                if (!_started)
                {
                    Log.Warning("LightshipSimulationProvider not started. Cannot localize anchor.");
                    anchor = default;
                    return false;
                }

                var hash = anchorPayload.GetHashCode();
                var anchorGuid = new TrackableId((ulong)hash, (ulong)hash);

                // Copy the temporary payload data to a persistent array
                // Because the nativearray from the manager is allocated as Temp, the data can be
                // lost after the next frame. We need to copy it to a persistent array to keep it alive
                var bytes = anchorPayload.GetDataAsBytes();
                var persistentPayload = new NativeArray<byte>(bytes, Allocator.Persistent);
                _payloads.Add(persistentPayload);

                IntPtr payloadIntPtr;
                unsafe
                {
                    payloadIntPtr = (IntPtr)persistentPayload.GetUnsafeReadOnlyPtr();
                }

                // Surface the anchor backed by the persistent payload
                var payloadSize = bytes.Length;
                var xrPersistentAnchorPayload = new XRPersistentAnchorPayload(payloadIntPtr, payloadSize);

                anchor = new XRPersistentAnchor
                (
                    anchorGuid,
                    Pose.identity,
                    TrackingState.None,
                    TrackingStateReason.None,
                    xrPersistentAnchorPayload,
                    (ulong)Time.realtimeSinceStartup
                );

                _anchors.Add(anchor);
                _addedList.Add(anchor);
                InvokeLocalization(anchor);
                return true;
            }

            private async Task InvokeLocalization(XRPersistentAnchor anchor)
            {
                // Get a random amount of MS to wait based on min/max discovery time
                var timeToWaitMs = _random.Next
                (
                    (int)Math.Min
                    (
                        _persistentAnchorParams.minimumAnchorDiscoveryTimeSeconds,
                        _persistentAnchorParams.maximumAnchorDiscoveryTimeSeconds
                    ) *
                    1000,
                    (int)_persistentAnchorParams.maximumAnchorDiscoveryTimeSeconds * 1000
                );

                await Task.Delay(timeToWaitMs);

                // Determine state and reason to surface based on settings
                var state = _persistentAnchorParams.surfaceAnchorFailure
                    ? TrackingState.None
                    : TrackingState.Tracking;

                var reason = _persistentAnchorParams.surfaceAnchorFailure
                    ? _persistentAnchorParams.trackingStateReason
                    : TrackingStateReason.None;

                // Apply any translational or rotational offsets
                var pose = Pose.identity;
                if (_persistentAnchorParams.applyTranslationalOffset)
                {
                    var offsetVector = UnityEngine.Random.onUnitSphere;

                    // Apply a random magnitude between 0 and the max severity
                    offsetVector *=
                    (
                        _persistentAnchorParams.translationalOffsetSeverityMeters *
                        (float)_random.NextDouble()
                    );

                    pose.position = offsetVector;
                }

                if (_persistentAnchorParams.applyRotationalOffset)
                {
                    // Random look angle
                    var offsetQuaternion = UnityEngine.Random.rotation;

                    var angle = Quaternion.Angle(offsetQuaternion, Quaternion.identity);

                    // Apply a random angle between 0 and the max severity
                    var angleLimit =
                        _persistentAnchorParams.rotationalOffsetSeverityDegrees *
                        (float)_random.NextDouble();

                    Quaternion finalRotation;

                    // If the angle is greater than the limit, return a rotation that is in the direction of the
                    //  offsetQuaternion
                    if (angle > angleLimit)
                    {
                        finalRotation = Quaternion.Slerp
                        (
                            Quaternion.identity,
                            offsetQuaternion,
                            angleLimit / angle
                        );
                    }
                    else
                    {
                        finalRotation = offsetQuaternion;
                    }

                    pose.rotation = finalRotation;
                }

                var update = new XRPersistentAnchor
                (
                    anchor.trackableId,
                    pose,
                    trackingState: state,
                    trackingStateReason: reason,
                    anchor.xrPersistentAnchorPayload,
                    (ulong) Time.realtimeSinceStartup
                );

                // If the anchor is in the added list, update it. Otherwise, add it to the update list
                // This is to prevent the same anchor from being added to multiple lists
                var existsInAddedList = false;
                for (int i = 0; i < _addedList.Count; i++)
                {
                    if (_addedList[i].trackableId.Equals(update.trackableId))
                    {
                        _addedList[i] = update;
                        existsInAddedList = true;
                    }
                }

                if (!existsInAddedList)
                {
                    _updateList.Add(update);
                }
            }
        }
    }
}
