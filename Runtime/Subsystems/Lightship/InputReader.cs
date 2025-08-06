// Copyright 2022-2025 Niantic.

using UnityEngine;

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// A component to acquire the current device pose in a static context.
    /// </summary>
    internal static class InputReader
    {
        /// <summary>
        /// Some platform use the new Input System, while others use the legacy xr input system.
        /// </summary>
        private static IInputReaderImpl s_readerImpl;

        /// <summary>
        /// Initializes the input reader.
        /// </summary>
        public static void Initialize()
        {
            // Already initialized?
            if (s_readerImpl != null)
            {
                Debug.LogWarning("Tried to initialize input reader multiple times.");
                return;
            }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            s_readerImpl = new InputReaderImpl();
#else
            s_readerImpl = new LegacyInputReaderImpl();
#endif
            s_readerImpl.OnCreate();
        }

        /// <summary>
        /// Shuts down the input reader.
        /// </summary>
        public static void Shutdown()
        {
            s_readerImpl?.OnDestroy();
            s_readerImpl = null;
        }

        /// <summary>
        /// Returns whether the component has successfully initialized with an appropriate tracking device.
        /// </summary>
        public static bool HasValidTrackingDevice => s_readerImpl.IsValid;

        /// <summary>
        /// The latest pose from the tracked device.
        /// </summary>
        public static Matrix4x4? CurrentPose => TryGetPose(out var pose) ? pose : null;

        /// <summary>
        /// Get the current pose of the camera capturing the background images.
        /// </summary>
        public static Matrix4x4? CurrentEyePose => TryGetEyePose(out var pose) ? pose : null;

        /// <summary>
        /// Returns the distance between the left and right cameras.
        /// </summary>
        public static float? InterpupillaryDistance => TryGetInterpupillaryDistance(out var ipd) ? ipd : null;

        /// <summary>
        /// Get the current pose of the tracked device.
        /// </summary>
        /// <param name="result">The transformation matrix for the most recent pose.</param>
        /// <param name="excludeDisplayRotation">Whether to exclude the rotation that compensates for UI rotation.</param>
        /// <returns>True, if the pose was successfully retrieved.</returns>
        public static bool TryGetPose(out Matrix4x4 result, bool excludeDisplayRotation = true)
        {
            if (s_readerImpl == null)
            {
                result = Matrix4x4.identity;
                return false;
            }

            return s_readerImpl.TryGetPose(out result, excludeDisplayRotation);
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
        public static bool TryGetEyePose(out Matrix4x4 result, bool excludeDisplayRotation = true)
        {
            if (s_readerImpl == null)
            {
                result = Matrix4x4.identity;
                return false;
            }

            return s_readerImpl.TryGetEyePose(out result, excludeDisplayRotation);
        }

        /// <summary>
        /// Get the interpupillary distance.
        /// </summary>
        /// <param name="ipd">Distance between left and right cameras</param>
        /// <returns>True, if the device ipd was successfully retrieved.</returns>
        public static bool TryGetInterpupillaryDistance(out float ipd)
        {
            if (s_readerImpl == null)
            {
                ipd = 0f;
                return false;
            }

            return s_readerImpl.TryGetInterpupillaryDistance(out ipd);
        }

        /// <summary>
        /// Get the current orientation for the tracked device.
        /// </summary>
        /// <returns>The current UI orientation.</returns>
        public static DeviceOrientation GetDeviceOrientation() =>
            s_readerImpl == null ? DeviceOrientation.Unknown :
            s_readerImpl.TryGetOrientation(out var orientation) ? orientation : DeviceOrientation.Unknown;

        /// <summary>
        /// Force use the old system for testing purposes.
        /// </summary>
        internal static void ForceUseLegacyInputSystem()
        {
            s_readerImpl?.OnDestroy();
            s_readerImpl = new LegacyInputReaderImpl();
            s_readerImpl.OnCreate();
        }

        /// <summary>
        /// Force use the new input system for testing purposes.
        /// </summary>
        internal static void ForceUseNewInputSystem()
        {
            s_readerImpl?.OnDestroy();
            s_readerImpl = new InputReaderImpl();
            s_readerImpl.OnCreate();
        }
    }
}
