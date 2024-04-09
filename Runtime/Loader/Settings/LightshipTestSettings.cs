// Copyright 2022-2024 Niantic.

namespace Niantic.Lightship.AR.Loader
{
    internal class LightshipTestSettings
    {
        public readonly bool DisableTelemetry;
        public readonly bool TickPamOnUpdate;

        public LightshipTestSettings(bool disableTelemetry, bool tickPamOnUpdate)
        {
            DisableTelemetry = disableTelemetry;
            TickPamOnUpdate = tickPamOnUpdate;
        }
    }
}
