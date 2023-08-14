using System.Collections.Generic;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Playback;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR
{
    internal class PlaybackLoaderHelper
    {
        private readonly List<XRSessionSubsystemDescriptor> _sessionSubsystemDescriptors = new();
        private readonly List<XRCameraSubsystemDescriptor> _cameraSubsystemDescriptors = new();
        private readonly List<XROcclusionSubsystemDescriptor> _occlusionSubsystemDescriptors = new();

        public PlaybackDatasetReader DatasetReader { get; private set; }

        /// <summary>
        /// Initializes the loader.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public bool Initialize(XRLoaderHelper loader, LightshipSettings settings)
        {
            Debug.Log("Initialize Playback subsystems");

#if UNITY_EDITOR
            var dataset = PlaybackDatasetLoader.Load(settings.EditorPlaybackSettings.PlaybackDatasetPath);
#else
            var dataset = PlaybackDatasetLoader.Load(settings.DevicePlaybackSettings.PlaybackDatasetPath);
#endif

            if (dataset == null)
            {
                Debug.LogError("Failed to initialize Playback subsystems because no dataset was loaded.");
                return false;
            }

#if UNITY_EDITOR
            DatasetReader = new PlaybackDatasetReader
            (
                dataset,
                settings.EditorPlaybackSettings.NumberOfIterations,
                settings.EditorPlaybackSettings.LoopInfinitely
            );
#else
           DatasetReader = new PlaybackDatasetReader
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
            ((IPlaybackDatasetUser)sessionSubsystem).SetPlaybackDatasetReader(DatasetReader);
            ((ILightshipSettingsUser)sessionSubsystem).SetLightshipSettings(settings);

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

            ((IPlaybackDatasetUser)cameraSubsystem).SetPlaybackDatasetReader(DatasetReader);

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
                ((IPlaybackDatasetUser)occlusionSubsystem).SetPlaybackDatasetReader(DatasetReader);
            }

            ((ILightshipSettingsUser)Input.location).SetLightshipSettings(settings);
            ((IPlaybackDatasetUser)Input.location).SetPlaybackDatasetReader(DatasetReader);

            ((ILightshipSettingsUser)Input.compass).SetLightshipSettings(settings);
            ((IPlaybackDatasetUser)Input.compass).SetPlaybackDatasetReader(DatasetReader);

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
