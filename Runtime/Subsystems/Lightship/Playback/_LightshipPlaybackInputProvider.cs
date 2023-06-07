// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.Scripting;

using Inputs = UnityEngine.InputSystem.InputSystem;

namespace Niantic.Lightship.AR.Playback
{
    [Preserve]
    internal class _LightshipPlaybackInputProvider : _IPlaybackDatasetUser, IDisposable
    {
        private _PlaybackDatasetReader _datasetReader;

        public _LightshipPlaybackInputProvider()
        {
            Lightship_ARDK_Unity_Input_Provider_Construct();
            _MonoBehaviourEventDispatcher.LateUpdating += LateUpdate;

            RegisterLayouts();
        }

        private void LateUpdate()
        {
            if (_datasetReader == null)
                return;

            if (_datasetReader.CurrentFrameIndex < 0)
                return;

            Vector3 position = _datasetReader.CurrFrame.Pose.ToPosition();

            Quaternion cameraToLocal = _datasetReader.CurrFrame.Pose.ToRotation();
            var displayToLocal = cameraToLocal * _CameraMath.DisplayToCameraRotation(_datasetReader.CurrFrame.Orientation);

            SetPose(position, displayToLocal, (uint)_datasetReader.CurrFrame.Orientation);
        }

        void RegisterLayouts()
        {
            Inputs.RegisterLayout<XRHMD>(matches: new InputDeviceMatcher()
                .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                .WithManufacturer("Niantic"));
        }

        public static void SetPose(Vector3 position, Quaternion rotation, uint deviceOrientation)
        {
            Lightship_ARDK_Unity_Input_Provider_SetPose(position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w, deviceOrientation);
        }

        [DllImport(_LightshipPlugin.Name)]
        private static extern int Lightship_ARDK_Unity_Input_Provider_Construct();

        [DllImport(_LightshipPlugin.Name)]
        private static extern int Lightship_ARDK_Unity_Input_Provider_SetPose(float px, float py, float pz, float qx, float qy, float qz, float qw, uint deviceOrientation);

        public void SetPlaybackDatasetReader(_PlaybackDatasetReader reader)
        {
            _datasetReader = reader;
        }

        public void Dispose()
        {
            _MonoBehaviourEventDispatcher.LateUpdating -= LateUpdate;
        }
    }
}
