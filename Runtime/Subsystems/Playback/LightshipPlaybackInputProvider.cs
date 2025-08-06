// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.Scripting;

namespace Niantic.Lightship.AR.Subsystems.Playback
{
    [Preserve]
    internal class LightshipPlaybackInputProvider : LightshipInputProvider, IPlaybackDatasetUser
    {
        private PlaybackDatasetReader _datasetReader;

        public LightshipPlaybackInputProvider() : base()
        {
            LightshipInputDevice.AddDevice<PlaybackInputDevice>(PlaybackInputDevice.ProductName);
        }

        private void OnFrameUpdate()
        {
            var frame = _datasetReader.GetFrame(_datasetReader.GetNextFrameIndex());
            SendPose(frame.Pose, frame.Orientation);
        }

        public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            if (_datasetReader != null)
            {
                // When running the app normally (i.e. with rendering), we want new data to be sent to the
                // native input provider in OnBeforeRender, so that the integrated input subsystem picks it up
                // in the next frame right before the Update tick. This means the input subsystem has the same
                // pose throughout the entire frame.

                // When code is run in batch mode however, like in our CI tests, there is no OnBeforeRender call,
                // so we have to use LateUpdate. That's fine though, because there's no rendering so effectively
                // the input subsystem will still have the same pose throughout the entire frame.
                if (Application.isBatchMode)
                {
                    MonoBehaviourEventDispatcher.LateUpdating.RemoveListener(OnFrameUpdate);
                }
                else
                {
                    Application.onBeforeRender -= OnFrameUpdate;
                }
            }

            if (reader != null)
            {
                if (Application.isBatchMode)
                {
                    MonoBehaviourEventDispatcher.LateUpdating.AddListener(OnFrameUpdate);
                }
                else
                {
                    Application.onBeforeRender += OnFrameUpdate;
                }
            }

            _datasetReader = reader;
            if (_datasetReader != null)
            {
                var frame = _datasetReader.GetFrame(0);
                SendPose(frame.Pose, frame.Orientation);
            }
        }

        private void SendPose(Matrix4x4 pose, ScreenOrientation orientation)
        {
            Vector3 position = pose.ToPosition();
            Quaternion cameraToLocal = pose.ToRotation();
            var displayToLocal = cameraToLocal * CameraMath.DisplayToCameraRotation(orientation);

            // Update the old input system
            SetPose(position, displayToLocal, (uint)orientation);

            // Update the new input system
            var device = InputSystem.GetDevice<PlaybackInputDevice>();
            device?.PushUpdate(position, displayToLocal, position, displayToLocal,
                (DeviceOrientation)orientation, Time.unscaledTimeAsDouble * 1000.0);
        }
    }

    [InputControlLayout(
        stateType = typeof(LightshipInputState),
        displayName = ProductName)]
    internal sealed class PlaybackInputDevice : LightshipInputDevice
    {
        public const string ProductName = "LightshipPlaybackCamera";
    }
}
