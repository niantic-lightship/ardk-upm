// Copyright 2022-2025 Niantic.

using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;
using UnityEngine.XR;

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// Implementation for the InputReader component with Unity's legacy XR Input system.
    /// </summary>
    internal sealed class LegacyInputReaderImpl : IInputReaderImpl
    {
        /// <summary>
        /// Whether the state of the input reader is ready to provide data.
        /// </summary>
        public bool IsValid => ActiveDevice.HasValue;

        // Reference to the active tracking device
        private InputDevice? ActiveDevice { get; set; }

        /// <summary>
        /// Invoked when it is time to allocate resources.
        /// </summary>
        /// <remarks>Find the necessary input devices in this call.</remarks>
        public void OnCreate()
        {
            CheckDevicesWithValidCharacteristics();
            InputDevices.deviceConnected += OnInputDeviceConnected;
        }

        /// <summary>
        /// Invoked when it is time to release resources.
        /// </summary>
        public void OnDestroy()
        {
            InputDevices.deviceConnected -= OnInputDeviceConnected;
        }

        /// <summary>
        /// Get the current pose of the tracked device.
        /// </summary>
        /// <remarks>
        /// ARKit returns (presumably) best guess poses even when tracking state is Limited (i.e. sliding the
        /// phone with the camera face down will change the translation value). However, ARCore "freezes"
        /// the device pose while the tracking state is Limited.
        /// </remarks>
        /// <param name="result">The transformation matrix for the most recent pose.</param>
        /// <param name="excludeDisplayRotation">Whether to exclude the rotation that compensates for UI rotation.</param>
        /// <returns>True, if the pose was successfully retrieved.</returns>
        public bool TryGetPose(out Matrix4x4 result, bool excludeDisplayRotation = true)
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

        /// <summary>
        /// Get the current pose of the camera capturing the background images.
        /// </summary>
        /// <remarks>
        /// On handheld, this is the same as the pose of the tracked device.
        /// On devices with multiple cameras (XR HMD), this includes the
        /// displacement from the head to the recording camera.
        /// </remarks>
        /// <param name="result">The transformation matrix for the most recent pose.</param>
        /// <param name="excludeDisplayRotation">Whether to exclude the rotation that compensates for UI rotation.</param>
        /// <returns>True, if the pose was successfully retrieved.</returns>
        public bool TryGetEyePose(out Matrix4x4 result, bool excludeDisplayRotation = true)
        {
            if (ActiveDevice.HasValue &&
                TryGetEyePositionAndRotation(ActiveDevice.Value, out Vector3 position, out Quaternion rotation))
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

        /// <summary>
        /// Get the current orientation for the tracked device.
        /// </summary>
        /// <param name="orientation">The current UI orientation.</param>
        /// <returns>True, if the device orientation was successfully retrieved.</returns>
        public bool TryGetOrientation(out DeviceOrientation orientation)
        {
            if (ActiveDevice.HasValue &&
                ActiveDevice.Value.TryGetFeatureValue(new InputFeatureUsage<uint>("DeviceOrientation"), out var val))
            {
                orientation = (DeviceOrientation)val;
                return true;
            }

            orientation = DeviceOrientation.Unknown;
            return false;
        }

        /// <summary>
        /// Get the interpupillary distance.
        /// </summary>
        /// <param name="ipd">Distance between left and right cameras</param>
        /// <returns>True, if the device ipd was successfully retrieved.</returns>
        public bool TryGetInterpupillaryDistance(out float ipd)
        {
            if (ActiveDevice.HasValue)
            {
                var device = ActiveDevice.Value;
                if (device.characteristics.HasFlag(InputDeviceCharacteristics.HeadMounted))
                {
                    if (device.TryGetFeatureValue(CommonUsages.leftEyePosition, out var leftEyePosition) &&
                        device.TryGetFeatureValue(CommonUsages.rightEyePosition, out var rightEyePosition))
                    {
                        ipd = Vector3.Distance(leftEyePosition, rightEyePosition);
                        return true;
                    }
                }
            }

            ipd = 0;
            return false;
        }

        private void OnInputDeviceConnected(InputDevice device)
        {
            Log.Info($"Input device detected with name {device.name}");
            CheckConnectedDevice(device);
        }

        private void CheckDevicesWithValidCharacteristics()
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

        private void CheckConnectedDevice(InputDevice device)
        {
            if (TryGetPositionAndRotation(device, out Vector3 p, out Quaternion r))
            {
                ActiveDevice ??= device;
                Log.Debug("Configured to read input from: " + device.name);
            }
        }

        private static bool TryGetPositionAndRotation(InputDevice device, out Vector3 position, out Quaternion rotation)
        {
            bool positionSuccess;
            bool rotationSuccess;

            if (!(positionSuccess = device.TryGetFeatureValue(CommonUsages.centerEyePosition, out position)))
            {
                positionSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraPosition, out position);
            }

            if (!(rotationSuccess = device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rotation)))
            {
                rotationSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraRotation, out rotation);
            }

            return positionSuccess && rotationSuccess;
        }

        private static bool TryGetEyePositionAndRotation(InputDevice device, out Vector3 position, out Quaternion rotation)
        {
            bool positionSuccess;
            bool rotationSuccess;

            // TODO(ahegedus): Query which camera is recording
            if (!(positionSuccess = device.TryGetFeatureValue(CommonUsages.leftEyePosition, out position)))
            {
                positionSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraPosition, out position);
            }

            if (!(rotationSuccess = device.TryGetFeatureValue(CommonUsages.leftEyeRotation, out rotation)))
            {
                rotationSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraRotation, out rotation);
            }

            return positionSuccess && rotationSuccess;
        }
    }
}
