// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine.XR;

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// A component to acquire the current device pose in a static context.
    /// </summary>
    internal static class InputReader
    {
        // Reference to the active tracking device
        public static InputDevice? ActiveDevice { get; private set; }

        /// <summary>
        /// Returns whether the component has successfully initialized with an appropriate tracking device.
        /// </summary>
        public static bool HasValidTrackingDevice
        {
            get => ActiveDevice.HasValue;
        }

        public static void Initialize()
        {
            CheckDevicesWithValidCharacteristics();
            InputDevices.deviceConnected += OnInputDeviceConnected;
        }

        public static void Shutdown()
        {
            InputDevices.deviceConnected -= OnInputDeviceConnected;
        }

        public static Matrix4x4? CurrentPose
        {
            get
            {
                if (TryGetPose(out var pose))
                {
                    return pose;
                }

                return null;
            }
        }

        /// <remarks>
        /// ARKit returns (presumably) best guess poses even when tracking state is Limited (i.e. sliding the
        /// phone with the camera face down will change the translation value). However, ARCore "freezes"
        /// the device pose while the tracking state is Limited.
        /// </remarks>
        public static bool TryGetPose(out Matrix4x4 result, bool excludeDisplayRotation = true)
        {
            if (ActiveDevice.HasValue &&
                TryGetPositionAndRotation(ActiveDevice.Value, out Vector3 position, out Quaternion rotation))
            {
                result = Matrix4x4.TRS(position, rotation, Vector3.one);
                if (!excludeDisplayRotation)
                {
                    return true;
                }

                var screenOrientation = XRDisplayContext.GetScreenOrientation();
                var cameraToDisplay = Matrix4x4.Rotate(CameraMath.CameraToDisplayRotation(screenOrientation));
                result *= cameraToDisplay;
                return true;
            }

            result = Matrix4x4.identity;
            return false;
        }

        public static DeviceOrientation GetDeviceOrientation()
        {
            if (ActiveDevice.HasValue &&
                ActiveDevice.Value.TryGetFeatureValue(new InputFeatureUsage<uint>("DeviceOrientation"), out var val))
            {
                return (DeviceOrientation)val;
            }

            return DeviceOrientation.Unknown;
        }

        private static void OnInputDeviceConnected(InputDevice device)
        {
            Log.Info($"Input device detected with name {device.name}");
            CheckConnectedDevice(device);
        }

        private static void CheckDevicesWithValidCharacteristics()
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.TrackedDevice, devices);
            foreach (var device in devices)
            {
                if (device.characteristics.HasFlag(InputDeviceCharacteristics.TrackedDevice))
                {
                    CheckConnectedDevice(device);
                }
            }
        }

        private static void CheckConnectedDevice(InputDevice device)
        {
            if (TryGetPositionAndRotation(device, out Vector3 p, out Quaternion r))
            {
                ActiveDevice ??= device;
                Log.Debug("Configured to read input from: " + device.name);
            }
        }

        private static bool TryGetPositionAndRotation(InputDevice device, out Vector3 position, out Quaternion rotation)
        {
            var positionSuccess = false;
            var rotationSuccess = false;

            if (!(positionSuccess = device.TryGetFeatureValue(CommonUsages.centerEyePosition, out position)))
            {
                positionSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraPosition, out position);
            }

            if (!(rotationSuccess = device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rotation)))
                rotationSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraRotation, out rotation);

            return positionSuccess && rotationSuccess;
        }
    }
}
