// Copyright 2022-2025 Niantic.

using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.Scripting;

using Inputs = UnityEngine.InputSystem.InputSystem;

namespace Niantic.Lightship.AR.Subsystems.Playback
{
    [Preserve]
    internal class LightshipInputProvider
    {

        public LightshipInputProvider()
        {
            Lightship_ARDK_Unity_Input_Provider_Construct();
            RegisterLayouts();
        }

        private void RegisterLayouts()
        {
            Inputs.RegisterLayout<XRHMD>(matches: new InputDeviceMatcher()
                .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                .WithManufacturer("Niantic"));
        }

        // Unity requests device states twice a frame via IUnityXRInputProvider.UpdateDeviceState.
        // The UnityXRInputUpdateType parameter specifies what kind of update Unity expects:
        //      * kUnityXRInputUpdateTypeDynamic is an update before Unity iterates over MonoBehaviour.Update calls
        //        and coroutine continuations. These should represent where the device currently is.
        //      * kUnityXRInputUpdateTypeBeforeRender is called right before Unity prepares to render to the headset,
        //        and just before Application.OnBeforeRender is invoked. These calls should use a forward predicted
        //        tracking position, and represent where you’d like to render the scene at the time it takes to display it.
        public static void SetPose(Vector3 position, Quaternion rotation, uint deviceOrientation)
        {
            Lightship_ARDK_Unity_Input_Provider_SetPose(position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w, deviceOrientation);
        }

        [DllImport(LightshipPlugin.Name)]
        private static extern int Lightship_ARDK_Unity_Input_Provider_Construct();

        [DllImport(LightshipPlugin.Name)]
        private static extern int Lightship_ARDK_Unity_Input_Provider_SetPose(float px, float py, float pz, float qx, float qy, float qz, float qw, uint deviceOrientation);
    }
}
