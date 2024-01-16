// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Protobuf;
using Niantic.Lightship.AR.Settings;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.AR.Telemetry
{
    internal static class TelemetryHelper
    {
        // using this flow with a pvt static bool to avoid re-subscribing to event in case of domain reloads.
        private static bool s_isInitializationEventLoggingRegistered = false;
        private static bool s_isArSessionCountEventRegistered = false;

#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED

        [RuntimeInitializeOnLoadMethod]
        private static void RegisterTelemetryInitEvent()
        {
            if (!s_isInitializationEventLoggingRegistered)
            {
                ARSession.stateChanged += LogInitializationEventOnStart;
                s_isInitializationEventLoggingRegistered = true;
            }

            if (!s_isArSessionCountEventRegistered)
            {
                ARSession.stateChanged += LogArSessionStartEvent;
                s_isArSessionCountEventRegistered = true;
            }

            Application.quitting += UnregisterArSessionStartEvent;
        }


# endif
        private static void LogInitializationEventOnStart(ARSessionStateChangedEventArgs sessionStateChangedEventArgs)
        {
            // On android/ios this is CheckingAvailability as the starting init. Dont ask. Please.
            // On desktop it is Ready.
            if (sessionStateChangedEventArgs.state == ARSessionState.CheckingAvailability ||
                sessionStateChangedEventArgs.state == ARSessionState.Ready)
            {
                string processor = string.Empty;
                if (!string.IsNullOrWhiteSpace(SystemInfo.processorType))
                {
                    processor = SystemInfo.processorType;
                }

                var initEvent = new InitializationEvent()
                {
                    InstallMode = Application.installMode.ToString(),
                    Processor = processor,
                };

                TelemetryService.PublishEvent(new ArdkNextTelemetryOmniProto()
                {
                    InitializationEvent = initEvent,
                });

                ARSession.stateChanged -= LogInitializationEventOnStart;
            }
        }

        private static void LogArSessionStartEvent(ARSessionStateChangedEventArgs sessionStateChangedEventArgs)
        {
            // iOS cannot use ARSessionState.SessionTracking since it sends in 2 SessionTracking events in most
            // ARSessions.
            // Hence, getting Android to use the Init event as well to maintain uniformity of definitions
            // across ios and android for data science.
            if (Metadata.IsAndroid() || Metadata.IsIphone())
            {
                if (sessionStateChangedEventArgs.state == ARSessionState.SessionInitializing)
                {
                    TelemetryService.PublishEvent(new ArdkNextTelemetryOmniProto()
                    {
                        ArSessionStartEvent = new ArSessionStartEvent(),
                    });
                }
            }
            else if (Metadata.IsEditor())
            {
                // UnityEditor does not get the ARSessionState.SessionInitializing event. Hence it has to be the SessionTracking
                // event for UnityEditor
                if (sessionStateChangedEventArgs.state == ARSessionState.SessionTracking)
                {
                    TelemetryService.PublishEvent(new ArdkNextTelemetryOmniProto()
                    {
                        ArSessionStartEvent = new ArSessionStartEvent(),
                    });
                }
            }
        }

        private static void UnregisterArSessionStartEvent()
        {
            ARSession.stateChanged -= LogArSessionStartEvent;
            s_isArSessionCountEventRegistered = false;
        }
    }
}
