using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace Niantic.Lightship.AR
{
    internal abstract class LightshipInputDevice : InputDevice
    {
        private const string InterfaceName = "Lightship-Input-Device";
        private const string Manufacturer = "Niantic";

        [InputControl(layout = "Vector3")]
        public Vector3Control CenterEyePosition { get; private set; }

        [InputControl(layout = "Quaternion")]
        public QuaternionControl CenterEyeRotation { get; private set; }

        [InputControl(layout = "Vector3")]
        public Vector3Control LeftEyePosition { get; private set; }

        [InputControl(layout = "Quaternion")]
        public QuaternionControl LeftEyeRotation { get; private set; }

        [InputControl(layout = "Integer")]
        public IntegerControl DeviceOrientation { get; private set; }

        [InputControl(layout = "Double")]
        public DoubleControl Timestamp { get; private set; }

        protected override void FinishSetup()
        {
            base.FinishSetup();
            CenterEyePosition = GetChildControl<Vector3Control>("centerEyePosition");
            CenterEyeRotation = GetChildControl<QuaternionControl>("centerEyeRotation");
            LeftEyePosition = GetChildControl<Vector3Control>("leftEyePosition");
            LeftEyeRotation = GetChildControl<QuaternionControl>("leftEyeRotation");
            DeviceOrientation = GetChildControl<IntegerControl>("DeviceOrientation");
            Timestamp = GetChildControl<DoubleControl>("timestamp");
        }

        public void PushUpdate(Vector3 centerEyePosition, Quaternion centerEyeRotation, Vector3 leftEyePosition,
            Quaternion leftEyeRotation, DeviceOrientation deviceOrientation, double timestampMs)
        {
            var state = new LightshipInputState
            {
                centerEyePosition = centerEyePosition,
                centerEyeRotation = centerEyeRotation,
                leftEyePosition = leftEyePosition,
                leftEyeRotation = leftEyeRotation,
                deviceOrientation = (int)deviceOrientation,
                timestamp = timestampMs
            };

            // Push the pose update to the input system
            InputSystem.QueueStateEvent(this, state);
        }

        public static void AddDevice<T>(string productName) where T : LightshipInputDevice, new()
        {
            InputSystem.RegisterLayout<T>(
                matches: new InputDeviceMatcher()
                    .WithManufacturer(Manufacturer)
                    .WithInterface(InterfaceName)
                    .WithProduct(productName)
            );

            InputSystem.AddDevice(new InputDeviceDescription
            {
                manufacturer = Manufacturer,
                interfaceName = InterfaceName,
                product = productName
            });
        }

        public static void RemoveDevice<T>() where T : LightshipInputDevice, new()
        {
            // Find and remove the device
            var device = InputSystem.GetDevice<T>();
            if (device != null)
            {
                InputSystem.RemoveDevice(device);
            }

            // Unregister the layout
            InputSystem.RemoveLayout(nameof(T));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LightshipInputState : IInputStateTypeInfo
    {
        // FourCC stands for Lightship Input Device
        private static FourCC Format => new('L', 'S', 'I', 'D');
        public FourCC format => Format;

        [InputControl(layout = "Vector3", usage = "CenterEyePosition")]
        public Vector3 centerEyePosition;

        [InputControl(layout = "Quaternion", usage = "CenterEyeRotation")]
        public Quaternion centerEyeRotation;

        [InputControl(layout = "Vector3", usage = "LeftEyePosition")]
        public Vector3 leftEyePosition;

        [InputControl(layout = "Quaternion", usage = "LeftEyeRotation")]
        public Quaternion leftEyeRotation;

        [InputControl(layout = "Integer", usage = "DeviceOrientation")]
        public int deviceOrientation;

        [InputControl(layout = "Double", usage = "Timestamp")]
        public double timestamp;
    }
}
