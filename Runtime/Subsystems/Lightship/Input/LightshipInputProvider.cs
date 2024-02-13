// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities;
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
