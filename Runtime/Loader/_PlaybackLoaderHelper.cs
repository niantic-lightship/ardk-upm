using System.Collections.Generic;
using Niantic.Lightship.AR.Loader;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Niantic.Lightship.AR.Playback;

namespace Niantic.Lightship.AR
{
    internal class _PlaybackLoaderHelper
    {
        private static readonly List<XRSessionSubsystemDescriptor> s_sessionSubsystemDescriptors = new();
        private static readonly List<XRCameraSubsystemDescriptor> s_cameraSubsystemDescriptors = new();
        private static readonly List<XROcclusionSubsystemDescriptor> s_occlusionSubsystemDescriptors = new();

        public _PlaybackDatasetReader DatasetReader { get; private set; }

        /// <summary>
        /// Initializes the loader.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public bool Initialize(XRLoaderHelper loader, LightshipSettings settings)
        {
            Debug.Log("Initialize Playback subsystems");

#if UNITY_EDITOR
            var dataset = _PlaybackDatasetLoader.Load(settings.PlaybackDatasetPathEditor);
#else
            var dataset = _PlaybackDatasetLoader.Load(settings.PlaybackDatasetPathDevice);
#endif

            if (dataset == null)
            {
                Debug.LogError("Failed to initialize Playback subsystems because no dataset was loaded.");
                return false;
            }

            DatasetReader = new _PlaybackDatasetReader(dataset);

            loader.CreateSubsystem<XRSessionSubsystemDescriptor, XRSessionSubsystem>
            (
                s_sessionSubsystemDescriptors,
                "Lightship-Playback-Session"
            );

            var sessionSubsystem = loader.GetLoadedSubsystem<XRSessionSubsystem>();
            ((_IPlaybackDatasetUser)sessionSubsystem).SetPlaybackDatasetReader(DatasetReader);
            ((_ILightshipSettingsUser)sessionSubsystem).SetLightshipSettings(settings);

            loader.CreateSubsystem<XRCameraSubsystemDescriptor, XRCameraSubsystem>
            (
                s_cameraSubsystemDescriptors,
                "Lightship-Playback-Camera"
            );

            var cameraSubsystem = loader.GetLoadedSubsystem<XRCameraSubsystem>();
            ((_IPlaybackDatasetUser)cameraSubsystem).SetPlaybackDatasetReader(DatasetReader);

            if (dataset.LidarEnabled && (!settings.UseLightshipDepth || settings.PreferLidarIfAvailable))
            {
                loader.DestroySubsystem<XROcclusionSubsystem>();

                Debug.Log("Creating " + nameof(LightshipOcclusionSubsystem));
                loader.CreateSubsystem<XROcclusionSubsystemDescriptor, XROcclusionSubsystem>
                (
                    s_occlusionSubsystemDescriptors,
                    "Lightship-Playback-Occlusion"
                );

                var occlusionSubsystem = loader.GetLoadedSubsystem<XROcclusionSubsystem>();
                ((_IPlaybackDatasetUser)occlusionSubsystem).SetPlaybackDatasetReader(DatasetReader);
            }

            ((_IPlaybackDatasetUser)Input.location).SetPlaybackDatasetReader(DatasetReader);


            if (sessionSubsystem == null)
            {
                Debug.LogError("Failed to load session subsystem.");
            }

            return sessionSubsystem != null;
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

            Input.location.DestroyProvider();

            return true;
        }
    }
}
