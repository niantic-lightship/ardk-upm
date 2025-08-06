// Copyright 2022-2025 Niantic.

using System.Linq;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// Implementation for the InputReader component with Unity's new Input system.
    /// </summary>
    internal sealed class InputReaderImpl : IInputReaderImpl
    {
        // Supported input devices
        private InputDevice _device;

        /// <summary>
        /// Whether the state of the input reader is ready to provide data.
        /// </summary>
        public bool IsValid => _device != null;

        /// <summary>
        /// Invoked when it is time to allocate resources.
        /// </summary>
        /// <remarks>Find the necessary input devices in this call.</remarks>
        public void OnCreate()
        {
            // Try to find devices already connected
            _device = InputSystem.devices.FirstOrDefault(IsDeviceEligible);

            // Listen to input system events
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        /// <summary>
        /// Invoked when it is time to release resources.
        /// </summary>
        public void OnDestroy()
        {
            // Unregister from events
            InputSystem.onDeviceChange -= OnDeviceChange;
            _device = null;
        }

        /// <summary>
        /// Get the current pose of the tracked device.
        /// </summary>
        /// <param name="result">The transformation matrix for the most recent pose.</param>
        /// <param name="excludeDisplayRotation">Whether to exclude the rotation that compensates for UI rotation.</param>
        /// <returns>True, if the pose was successfully retrieved.</returns>
        public bool TryGetPose(out Matrix4x4 result, bool excludeDisplayRotation = true)
        {
            if (!IsValid)
            {
                result = Matrix4x4.identity;
                return false;
            }

            // Look for HMD controls
            var positionControl = _device.TryGetChildControl<Vector3Control>("centerEyePosition");
            var rotationControl = _device.TryGetChildControl<QuaternionControl>("centerEyeRotation");
            if (positionControl == null || rotationControl == null)
            {
                // Fall back to handheld controls
                positionControl = _device.TryGetChildControl<Vector3Control>("devicePosition");
                rotationControl = _device.TryGetChildControl<QuaternionControl>("deviceRotation");
                if (positionControl == null || rotationControl == null)
                {
                    result = Matrix4x4.identity;
                    return false;
                }
            }

            Vector3 pos = positionControl.ReadValue();
            Quaternion rot = rotationControl.ReadValue();
            if (!IsValidQuaternion(rot))
            {
                result = Matrix4x4.identity;
                return false;
            }

            result = Matrix4x4.TRS(pos, rot, Vector3.one);
            if (!excludeDisplayRotation)
            {
                return true;
            }

            var screenOrientation = XRDisplayContext.GetScreenOrientation();
            var cameraToDisplay = Matrix4x4.Rotate(CameraMath.CameraToDisplayRotation(screenOrientation));
            result *= cameraToDisplay;
            return true;
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
            if (!IsValid)
            {
                result = Matrix4x4.identity;
                return false;
            }

            // Look for HMD controls
            var positionControl = _device.TryGetChildControl<Vector3Control>("leftEyePosition");
            var rotationControl = _device.TryGetChildControl<QuaternionControl>("leftEyeRotation");
            if (positionControl != null && rotationControl != null)
            {
                Vector3 pos = positionControl.ReadValue();
                Quaternion rot = rotationControl.ReadValue();
                result = Matrix4x4.TRS(pos, rot, Vector3.one);
                if (!excludeDisplayRotation)
                {
                    return true;
                }

                var screenOrientation = XRDisplayContext.GetScreenOrientation();
                var cameraToDisplay = Matrix4x4.Rotate(CameraMath.CameraToDisplayRotation(screenOrientation));
                result *= cameraToDisplay;
                return true;
            }

            // On handheld, eye pose is the same as the device pose
            return TryGetPose(out result, excludeDisplayRotation);
        }

        /// <summary>
        /// Get the current orientation for the tracked device.
        /// </summary>
        /// <param name="orientation">The current UI orientation.</param>
        /// <returns>True, if the device orientation was successfully retrieved.</returns>
        public bool TryGetOrientation(out DeviceOrientation orientation)
        {
            if (!IsValid)
            {
                orientation = DeviceOrientation.Unknown;
                return false;
            }

            // Look for DeviceOrientation on playback
            var orientationControl = _device.TryGetChildControl<IntegerControl>("DeviceOrientation");
            if (orientationControl != null)
            {
                orientation = (DeviceOrientation)orientationControl.ReadValue();
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
            if (IsValid)
            {
                // Look for HMD controls
                var leftControl = _device.TryGetChildControl<Vector3Control>("leftEyePosition");
                var rightControl = _device.TryGetChildControl<Vector3Control>("rightEyePosition");
                if (leftControl != null && rightControl != null)
                {
                    Vector3 leftPos = leftControl.ReadValue();
                    Vector3 rightPos = rightControl.ReadValue();
                    ipd = Vector3.Distance(leftPos, rightPos);
                    return true;
                }
            }

            ipd = 0f;
            return false;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange deviceChange)
        {
            switch (deviceChange)
            {
                // Add input device
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                {
                    // If we have already found a device
                    if (_device != null)
                    {
                        // Do not replace the device if the current
                        // one is designed for the new input system
                        if (UsesNewInterface(_device))
                        {
                            return;
                        }

                        // Only replace the device if the new one is internal
                        if (!IsDeviceInternal(device))
                        {
                            return;
                        }
                    }

                    if (IsDeviceEligible(device))
                    {
                        _device = device;
                    }
                }
                    break;

                // Remove input device
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                {
                    if (_device == device)
                    {
                        _device = null;
                    }
                }
                    break;
            }
        }

        private static bool IsDeviceInternal(InputDevice device)
        {
            string manufacturer = device.description.manufacturer?.ToLowerInvariant() ?? "";
            return manufacturer == "niantic";
        }

        private static bool UsesNewInterface(InputDevice device) =>
            device.description.interfaceName == "Lightship-Input-Device";

        private static bool IsDeviceEligible(InputDevice device)
        {
            if (device == null || !device.added)
            {
                return false;
            }

            if (IsDeviceInternal(device))
            {
                return true;
            }

            // Reject known controller or hand-tracking layouts
            string layout = device.layout.ToLowerInvariant();
            if (layout.Contains("controller") || layout.Contains("hand"))
            {
                return false;
            }

            // Accept if device provides head-tracking controls
            bool hasCenterEye = device.TryGetChildControl<Vector3Control>("centerEyePosition") != null &&
                device.TryGetChildControl<QuaternionControl>("centerEyeRotation") != null;

            bool hasLeftEye = device.TryGetChildControl<Vector3Control>("leftEyePosition") != null &&
                device.TryGetChildControl<QuaternionControl>("leftEyeRotation") != null;

            // Accept if device provides generic pose but not as a controller
            bool hasDevicePose = device.TryGetChildControl<Vector3Control>("devicePosition") != null &&
                device.TryGetChildControl<QuaternionControl>("deviceRotation") != null;

            string interfaceName = device.description.interfaceName?.ToLowerInvariant() ?? "";
            string productName = device.description.product?.ToLowerInvariant() ?? "";
            bool isLikelyHeadTracked = productName.Contains("head") || interfaceName.Contains("xrinput");
            bool isHandheldARInputDevice = device is HandheldARInputDevice;

            return (hasCenterEye || hasLeftEye || (hasDevicePose && isLikelyHeadTracked)) || isHandheldARInputDevice;
        }

        private static bool IsValidQuaternion(Quaternion q)
        {
            float mag = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            return mag > 0.0001f; // Avoid near-zero magnitude
        }
    }
}
