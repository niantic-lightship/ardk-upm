// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using System.Diagnostics;

using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Protobuf;
using Niantic.Lightship.AR.Telemetry;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;
using Debug = UnityEngine.Debug;

namespace Niantic.Lightship.AR.PersistentAnchors
{
    public partial class ARPersistentAnchorManager
    {
        // This is a sidecar class to handle telemetry for ARPersistentAnchorManager
        // It tracks the total time spent in the session and the time spent tracking,
        //  as well as meta session data
        private class ARPersistentAnchorTelemetrySidecar
        {
            private ARPersistentAnchorManager _manager;
            private Stopwatch _timeSpentTrackingStopwatch = new Stopwatch();
            private Stopwatch _totalTimeInSessionStopwatch = new Stopwatch();
            private string _lastKnownSessionId;

            internal ARPersistentAnchorTelemetrySidecar(ARPersistentAnchorManager manager)
            {
                _manager = manager;
                _totalTimeInSessionStopwatch.Reset();
            }

            internal void AnchorAdded(ARPersistentAnchor anchor)
            {
                if (!string.IsNullOrEmpty(_lastKnownSessionId) &&
                    _lastKnownSessionId != _manager.VpsSessionId)
                {
                    Log.Error("A new session has been started before the last one ended");
                    SessionEnded();
                }

                _lastKnownSessionId = _manager.VpsSessionId;

                EmitSessionStartedEvent
                (
                    _manager.VpsSessionId,
                    new List<string>
                    {
                        anchor.trackableId.ToLightshipHexString()
                    }
                );

                _totalTimeInSessionStopwatch.Start();
            }

            internal void LocalizationSuccess(ARPersistentAnchor anchor)
            {
                _timeSpentTrackingStopwatch.Start();
                var timeToLocalizeMs = _totalTimeInSessionStopwatch.ElapsedMilliseconds;
                var numServerRequests = _manager.subsystem.NumberServerRequests;
                EmitLocalizationSuccess
                (
                    _manager.VpsSessionId,
                    anchor.trackableId.ToLightshipHexString(),
                    timeToLocalizeMs,
                    numServerRequests
                );
            }

            internal void TrackingLost()
            {
                _timeSpentTrackingStopwatch.Stop();
            }

            internal void TrackingRegained()
            {
                _timeSpentTrackingStopwatch.Start();
            }

            internal void SessionEnded()
            {
                if (string.IsNullOrEmpty(_lastKnownSessionId))
                {
                    return;
                }

                var vpsSessionId = _lastKnownSessionId;
                var numServerRequests = _manager.subsystem.NumberServerRequests;
                var timeSpentTrackingMs = _timeSpentTrackingStopwatch.ElapsedMilliseconds;
                var totalSessionTimeMs = _totalTimeInSessionStopwatch.ElapsedMilliseconds;
                var networkErrorState = _manager.subsystem.NetworkErrorCodeCounts;

                EmitVpsSessionEnded
                (
                    vpsSessionId,
                    numServerRequests,
                    timeSpentTrackingMs,
                    totalSessionTimeMs,
                    networkErrorState
                );

                _timeSpentTrackingStopwatch.Reset();
                _totalTimeInSessionStopwatch.Reset();
                _lastKnownSessionId = null;
            }

            private static void EmitSessionStartedEvent
                (string vpsSessionId, List<string> localizationTargetIds)
            {
                var startedEvent = new VpsLocalizationStartedEvent()
                {
                    VpsSessionId = vpsSessionId
                };

                startedEvent.LocalizationTargetIds.AddRange(localizationTargetIds);
                var omniProto = new ArdkNextTelemetryOmniProto
                {
                    VpsLocalizationStartedEvent = startedEvent
                };

                PublishEvent(new ArdkNextTelemetryOmniProto(omniProto));
            }

            private static void EmitLocalizationSuccess
            (
                string vpsSessionId,
                string targetId,
                long timeToLocalize,
                int numServerRequests
            )
            {
                var localizationEvent = new VpsLocalizationSuccessEvent
                {
                    LocalizationTargetId = targetId,
                    VpsSessionId = vpsSessionId,
                    TimeToLocalizeMs = timeToLocalize,
                    NumServerRequests = numServerRequests
                };

                var omniProto = new ArdkNextTelemetryOmniProto
                {
                    VpsLocalizationSuccessEvent = localizationEvent
                };

                PublishEvent(omniProto);
            }

            private static void EmitVpsSessionEnded
            (
                string vpsSessionId,
                int numServerRequests,
                long timeSpentTrackingMs,
                long totalSessionTimeMs,
                Dictionary<ErrorCode, int> networkErrorStates
            )
            {
                var endedEvent = new VpsSessionEndedEvent
                {
                    VpsSessionId = vpsSessionId,
                    NumServerRequests = numServerRequests,
                    TimeTrackedMs = timeSpentTrackingMs,
                    TotalSessionTimeMs = totalSessionTimeMs,
                };

                foreach (var (error, count) in networkErrorStates)
                {
                    endedEvent.NetworkErrorCodes.Add(error.ToString(), count);
                }

                var omniproto = new ArdkNextTelemetryOmniProto()
                {
                    VpsSessionEndedEvent = endedEvent
                };

                PublishEvent(omniproto);
            }

            private static void PublishEvent(ArdkNextTelemetryOmniProto proto)
            {
                TelemetryService.PublishEvent(proto);
            }
        }
    }
}
