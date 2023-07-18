using System.Collections.Generic;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Playback;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR
{
    internal class _PlaybackLoaderHelper
    {
        private readonly List<XRSessionSubsystemDescriptor> _sessionSubsystemDescriptors = new();
        private readonly List<XRCameraSubsystemDescriptor> _cameraSubsystemDescriptors = new();
        private readonly List<XROcclusionSubsystemDescriptor> _occlusionSubsystemDescriptors = new();

        public _PlaybackDatasetReader DatasetReader { get; private set; }

        /// <summary>
        /// Initializes the loader.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public bool Initialize(XRLoaderHelper loader, LightshipSettings settings)
        {
            Debug.Log("Initialize Playback subsystems");

#if UNITY_EDITOR
            var dataset = _PlaybackDatasetLoader.Load(settings.EditorPlaybackSettings.PlaybackDatasetPath);
#else
            var dataset = _PlaybackDatasetLoader.Load(settings.DevicePlaybackSettings.PlaybackDatasetPath);
#endif

            if (dataset == null)
            {
                Debug.LogError("Failed to initialize Playback subsystems because no dataset was loaded.");
                return false;
            }

#if UNITY_EDITOR
            DatasetReader = new _PlaybackDatasetReader
            (
                dataset,
                settings.EditorPlaybackSettings.NumberOfIterations,
                settings.EditorPlaybackSettings.LoopInfinitely
            );
#else
           DatasetReader = new _PlaybackDatasetReader
            (
                dataset,
                settings.DevicePlaybackSettings.NumberOfIterations,
                settings.DevicePlaybackSettings.LoopInfinitely
            );
#endif

            loader.CreateSubsystem<XRSessionSubsystemDescriptor, XRSessionSubsystem>
            (
                _sessionSubsystemDescriptors,
                "Lightship-Playback-Session"
            );

            var sessionSubsystem = loader.GetLoadedSubsystem<XRSessionSubsystem>();
            ((_IPlaybackDatasetUser)sessionSubsystem).SetPlaybackDatasetReader(DatasetReader);
            ((_ILightshipSettingsUser)sessionSubsystem).SetLightshipSettings(settings);

            loader.CreateSubsystem<XRCameraSubsystemDescriptor, XRCameraSubsystem>
            (
                _cameraSubsystemDescriptors,
                "Lightship-Playback-Camera"
            );

            var cameraSubsystem = loader.GetLoadedSubsystem<XRCameraSubsystem>();

            if (sessionSubsystem == null)
            {
                // Subsystems can only be loaded in Play Mode
                Debug.LogError("Failed to load subsystem.");
                return false;
            }

            ((_IPlaybackDatasetUser)cameraSubsystem).SetPlaybackDatasetReader(DatasetReader);

            if (dataset.LidarEnabled && (!settings.UseLightshipDepth || settings.PreferLidarIfAvailable))
            {
                loader.DestroySubsystem<XROcclusionSubsystem>();

                Debug.Log("Creating " + nameof(LightshipPlaybackOcclusionSubsystem));
                loader.CreateSubsystem<XROcclusionSubsystemDescriptor, XROcclusionSubsystem>
                (
                    _occlusionSubsystemDescriptors,
                    "Lightship-Playback-Occlusion"
                );

                var occlusionSubsystem = loader.GetLoadedSubsystem<XROcclusionSubsystem>();
                ((_IPlaybackDatasetUser)occlusionSubsystem).SetPlaybackDatasetReader(DatasetReader);
            }

            ((_ILightshipSettingsUser)Input.location).SetLightshipSettings(settings);
            ((_IPlaybackDatasetUser)Input.location).SetPlaybackDatasetReader(DatasetReader);

            ((_ILightshipSettingsUser)Input.compass).SetLightshipSettings(settings);
            ((_IPlaybackDatasetUser)Input.compass).SetPlaybackDatasetReader(DatasetReader);

            return true;
        }

        /// <summary>
        /// Destroys each initialized subsystem.
        /// </summary>
        /// <returns>Always returns `true`.</returns>
        public bool Deinitialize(XRLoaderHelper loader)
        {
            Debug.Log("Deinitialize playback subsystems");
            loader.DestroySubsystem<XRSessionSubsystem>();
            loader.DestroySubsystem<XRCameraSubsystem>();

            return true;
        }
    }
}
