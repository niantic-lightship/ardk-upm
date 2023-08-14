using UnityEngine;
using UnityEngine.XR;

namespace Niantic.Lightship.AR.PAM
{
    internal class PlaybackSubsystemsDataAcquirer : SubsystemsDataAcquirer
    {
        public PlaybackSubsystemsDataAcquirer()
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
            return (ScreenOrientation)deviceOrientation;
        }
    }
}
