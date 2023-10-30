// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System.Collections.Generic;
using UnityEngine;
using Niantic.Lightship.AR.Utilities.Log;
using UnityEngine.XR;

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// A component to acquire the current device pose in a static context.
    /// TODO: TECHDEBT Consolidate with SubsystemDataAcquirer (AR-17631).
    /// </summary>
    internal static class PoseProvider
    {
        // The set of device descriptors that the PoseProvider can use
        private static readonly InputDeviceCharacteristics[] s_appropriateDevices =
        {
            InputDeviceCharacteristics.HeldInHand, InputDeviceCharacteristics.TrackedDevice
        };

        // Reference to the active tracking device
        private static InputDevice? s_trackingDevice;


        /// <summary>
        /// Returns whether the component has successfully initialized with an appropriate tracking device.
        /// </summary>
        public static bool HasValidTrackingDevice
        {
            get => s_trackingDevice is {isValid: true};
        }

        /// <summary>
        /// Tries to acquire a new device appropriate for pose tracking.
        /// </summary>
        /// <returns>Whether the component has successfully initialized with an appropriate tracking device.</returns>
        private static bool Reinitialize()
        {
            s_trackingDevice = FindTrackingDevice();
            return s_trackingDevice is {isValid: true};
        }

        static PoseProvider()
        {
            if (!Reinitialize())
            {
                Log.Error("Static pose provider could not locate an appropriate tracking device.");
            }
        }

        private static InputDevice? FindTrackingDevice()
        {
            var devices = new List<InputDevice>();
            foreach (var deviceType in s_appropriateDevices)
            {
                InputDevices.GetDevicesWithCharacteristics(deviceType, devices);
                foreach (var device in devices)
                {
                    if (IsPoseTracker(device))
                    {
                        return device;
                    }
                }

                devices.Clear();
            }

            return null;
        }

        private static bool IsPoseTracker(InputDevice device)
        {
            var tracksPosition = device.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 _)
                || device.TryGetFeatureValue(CommonUsages.colorCameraPosition, out Vector3 _);
            var tracksRotation = device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion _) ||
                device.TryGetFeatureValue(CommonUsages.colorCameraRotation, out Quaternion _);

            return tracksPosition && tracksRotation;
        }

        public static bool TryAcquireCurrentPose(out Matrix4x4 result, bool excludeDisplayRotation = true)
        {
            if (s_trackingDevice == null)
            {
                if (!Reinitialize())
                {
                    result = Matrix4x4.identity;
                    Log.Error("Static pose provider couldn't acquire the current device pose.");
                    return false;
                }
            }

            var pose = Pose.identity;
            bool positionSuccess;
            bool rotationSuccess;

            if (!(positionSuccess =
                    s_trackingDevice.Value.TryGetFeatureValue(CommonUsages.centerEyePosition, out pose.position)))
            {
                positionSuccess =
                    s_trackingDevice.Value.TryGetFeatureValue(CommonUsages.colorCameraPosition, out pose.position);
            }

            if (!(rotationSuccess =
                    s_trackingDevice.Value.TryGetFeatureValue(CommonUsages.centerEyeRotation, out pose.rotation)))
            {
                rotationSuccess =
                    s_trackingDevice.Value.TryGetFeatureValue(CommonUsages.colorCameraRotation, out pose.rotation);
            }

            if (positionSuccess || rotationSuccess)
            {
                if (!excludeDisplayRotation)
                {
                    result = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
                    return true;
                }

                var cameraToDisplay = Matrix4x4.identity;
                switch (Screen.orientation)
                {
                    case ScreenOrientation.Portrait:
                        cameraToDisplay = Matrix4x4.Rotate(Quaternion.Euler(0, 0, -90));
                        break;
                    case ScreenOrientation.LandscapeLeft:
                        cameraToDisplay = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 0));
                        break;
                    case ScreenOrientation.PortraitUpsideDown:
                        cameraToDisplay = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90));
                        break;
                    case ScreenOrientation.LandscapeRight:
                        cameraToDisplay = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 180));
                        break;
                }

                result = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one) * cameraToDisplay;
                return true;
            }

            result = Matrix4x4.identity;
            return false;
        }
    }
}
