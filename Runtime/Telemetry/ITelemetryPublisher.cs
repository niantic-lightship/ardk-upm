// Copyright 2023 Niantic, Inc. All Rights Reserved.

using Niantic.Lightship.AR.Protobuf;

namespace Telemetry
{
    internal interface ITelemetryPublisher
    {
        public void RecordEvent(ArdkNextTelemetryOmniProto telemetryEvent);
    }
}
