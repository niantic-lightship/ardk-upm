// Copyright 2022-2024 Niantic.
using UnityEngine;
using UnityEngine.XR;

namespace Niantic.Lightship.AR.PAM
{
    internal class StandaloneSubsystemsDataAcquirer : SubsystemsDataAcquirer
    {
        private ScreenOrientation _lastScreenOrientation;

        public StandaloneSubsystemsDataAcquirer()
        {
            SetupSubsystemReferences();
        }

        public override DeviceOrientation GetDeviceOrientation()
        {
            return (DeviceOrientation)GetScreenOrientation();
        }

        protected override ScreenOrientation GetScreenOrientation()
        {
            if (InputDevice == null)
            {
                return ScreenOrientation.Portrait;
            }

            InputDevice.Value.TryGetFeatureValue(new InputFeatureUsage<uint>("DeviceOrientation"), out var deviceOrientation);
            if (deviceOrientation != 0)
                _lastScreenOrientation = (ScreenOrientation)deviceOrientation;

            return _lastScreenOrientation;
        }
    }
}
