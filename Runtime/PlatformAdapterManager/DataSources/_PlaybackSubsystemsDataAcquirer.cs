using System;
using Niantic.Lightship.AR.Playback;
using UnityEngine;
using UnityEngine.XR;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    internal class _PlaybackSubsystemsDataAcquirer : _SubsystemsDataAcquirer
    {
        public _PlaybackSubsystemsDataAcquirer()
        {
            SetupSubsystemReferences();
        }

        public override DeviceOrientation GetDeviceOrientation()
        {
            return (DeviceOrientation)GetScreenOrientation();
        }

        protected override ScreenOrientation GetScreenOrientation()
        {
            if (_inputDevice == null)
                return ScreenOrientation.Portrait;

            _inputDevice.Value.TryGetFeatureValue(new InputFeatureUsage<uint>("DeviceOrientation"), out var deviceOrientation);
            return (ScreenOrientation)deviceOrientation;
        }
    }
}
