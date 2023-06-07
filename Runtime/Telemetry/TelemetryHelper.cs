// Copyright 2023 Niantic, Inc. All Rights Reserved.

using Niantic.Lightship.AR.Protobuf;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Telemetry
{
    internal static class TelemetryHelper
    {
        // using this flow with a pvt static bool to avoid re-subscribing to event in case of domain reloads.
        private static bool s_isInitializationEventLoggingRegistered = false;

#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED

        [RuntimeInitializeOnLoadMethod]
        private static void RegisterTelemetryInitEvent()
        {
            if (!s_isInitializationEventLoggingRegistered)
            {
                ARSession.stateChanged += LogInitializationEventOnStart;
            }

            s_isInitializationEventLoggingRegistered = true;
        }
# endif
        private static void LogInitializationEventOnStart(ARSessionStateChangedEventArgs sessionStateChangedEventArgs)
        {
            // On android/ios this is CheckingAvailability as the starting init. Dont ask. Please.
            // On desktop it is Ready.
            if (sessionStateChangedEventArgs.state == ARSessionState.CheckingAvailability ||
                sessionStateChangedEventArgs.state == ARSessionState.Ready)
            {
                var initEvent = new InitializationEvent()
                {
                    InstallMode = Application.installMode.ToString(),
                };

                TelemetryService.PublishEvent(new ArdkNextTelemetryOmniProto()
                {
                    InitializationEvent = initEvent,
                });

                ARSession.stateChanged -= LogInitializationEventOnStart;
            }
        }
    }
}
