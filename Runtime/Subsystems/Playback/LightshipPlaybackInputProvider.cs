// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Subsystems.Playback;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.Scripting;

using Inputs = UnityEngine.InputSystem.InputSystem;

namespace Niantic.Lightship.AR.Subsystems.Playback
{
    [Preserve]
    internal class LightshipPlaybackInputProvider : LightshipInputProvider, IPlaybackDatasetUser, IDisposable
    {
        private PlaybackDatasetReader _datasetReader;

        public LightshipPlaybackInputProvider()
        {
            MonoBehaviourEventDispatcher.LateUpdating.AddListener(LateUpdate);
        }

        private void LateUpdate()
        {
            if (_datasetReader == null || _datasetReader.CurrentFrameIndex < 0)
            {
                return;
            }

            var pose = _datasetReader.GetCurrentPose();
            Vector3 position = pose.ToPosition();
            Quaternion cameraToLocal = pose.ToRotation();

            var displayToLocal = cameraToLocal * CameraMath.DisplayToCameraRotation(_datasetReader.CurrFrame.Orientation);

            SetPose(position, displayToLocal, (uint)_datasetReader.CurrFrame.Orientation);
        }

        public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            _datasetReader = reader;
        }

        public void Dispose()
        {
            MonoBehaviourEventDispatcher.LateUpdating.RemoveListener(LateUpdate);
        }
    }
}
