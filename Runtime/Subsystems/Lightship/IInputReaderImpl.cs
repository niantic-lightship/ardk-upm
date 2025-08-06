// Copyright 2022-2025 Niantic.

using UnityEngine;

namespace Niantic.Lightship.AR
{
    internal interface IInputReaderImpl
    {
        /// <summary>
        /// Whether the state of the input reader is ready to provide data.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Invoked when it is time to allocate resources.
        /// </summary>
        /// <remarks>Find the necessary input devices in this call.</remarks>
        void OnCreate();

        /// <summary>
        /// Invoked when it is time to release resources.
        /// </summary>
        void OnDestroy();

        /// <summary>
        /// Get the current pose of the tracked device.
        /// </summary>
        /// <param name="result">The transformation matrix for the most recent pose.</param>
        /// <param name="excludeDisplayRotation">Whether to exclude the rotation that compensates for UI rotation.</param>
        /// <returns>True, if the pose was successfully retrieved.</returns>
        bool TryGetPose(out Matrix4x4 result, bool excludeDisplayRotation = true);

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
        bool TryGetEyePose(out Matrix4x4 result, bool excludeDisplayRotation = true);

        /// <summary>
        /// Get the current orientation for the tracked device.
        /// </summary>
        /// <param name="orientation">The current UI orientation.</param>
        /// <returns>True, if the device orientation was successfully retrieved.</returns>
        bool TryGetOrientation(out DeviceOrientation orientation);

        /// <summary>
        /// Get the interpupillary distance.
        /// </summary>
        /// <param name="ipd">Distance between left and right cameras</param>
        /// <returns>True, if the device ipd was successfully retrieved.</returns>
        bool TryGetInterpupillaryDistance(out float ipd);
    }
}
