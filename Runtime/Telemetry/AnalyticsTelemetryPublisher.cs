using System;
using Niantic.Lightship.AR.Protobuf;
using Niantic.Platform.Analytics.Telemetry;
using Niantic.Platform.Analytics.Telemetry.Logging;
using UnityEngine;
using ILogHandler = Niantic.Platform.Analytics.Telemetry.Logging.ILogHandler;

namespace Telemetry
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
                Debug.Log("Registering logger for telemetry.");
            }

            _ardkPublisher = builder.Build();
            Debug.Log("Successfully created the ardk publisher.");
        }

        public void RecordEvent(ArdkNextTelemetryOmniProto telemetryEvent)
        {
            try
            {
                _ardkPublisher.RecordEvent(telemetryEvent);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Posting telemetry failed with the following exception: {ex}");
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

                        Debug.Log(message);
                        break;

                    case LogLevel.Warning:
                        Debug.LogWarning(message);
                        break;

                    case LogLevel.Error:
                    case LogLevel.Fatal:

                        Debug.LogError(message);
                        break;

                    default:
                        Debug.Log(message);
                        break;
                }
            }
        }
    }
}
