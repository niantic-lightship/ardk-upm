// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.Utilities.Log;
using Niantic.Lightship.AR.Protobuf;
using Niantic.Platform.Analytics.Telemetry;
using Niantic.Platform.Analytics.Telemetry.Logging;
using UnityEngine;
using ILogHandler = Niantic.Platform.Analytics.Telemetry.Logging.ILogHandler;

namespace Niantic.Lightship.AR.Telemetry
{
    public class AnalyticsTelemetryPublisher : ITelemetryPublisher
    {
        private readonly ARDKTelemetryService<ArdkNextTelemetryOmniProto> _ardkPublisher;

        public AnalyticsTelemetryPublisher(string endpoint, string key, string directoryPath, bool registerLogger)
        {
            var builder = new ARDKTelemetryService<ArdkNextTelemetryOmniProto>.Builder
            (
                rpcEndpointUrl:endpoint,
                directoryPath,
                key
            );

            if (registerLogger)
            {
                var debugOptions = new StartupDebugOptions();
                debugOptions.LogHandler = new TelemetryLogger();
                debugOptions.LogOptions = LogOptions.All;

                builder.SetDebugOptions(debugOptions);
                // Log.Info("Registering logger for telemetry.");
            }

            _ardkPublisher = builder.Build();
            // Log.Info("Successfully created the ardk publisher.");
        }

        public void RecordEvent(ArdkNextTelemetryOmniProto telemetryEvent)
        {
            try
            {
                _ardkPublisher.RecordEvent(telemetryEvent);
            }
            catch (Exception)
            {
                // fail silently
                // enable for debugging
                // Log.Warning($"Posting telemetry failed with the following exception: {ex}");
            }
        }

        private class TelemetryLogger : ILogHandler
        {
            public void LogMessage(LogLevel logLevel, string message)
            {
                switch (logLevel)
                {
                    case LogLevel.Verbose:
                    case LogLevel.Info:

                        Log.Info(message);
                        break;

                    case LogLevel.Warning:
                        Log.Warning(message);
                        break;

                    case LogLevel.Error:
                    case LogLevel.Fatal:

                        Log.Error(message);
                        break;

                    default:
                        Log.Info(message);
                        break;
                }
            }
        }
    }
}
