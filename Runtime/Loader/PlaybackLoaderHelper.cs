// Copyright 2022-2024 Niantic.
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Subsystems.Playback;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR
{
    internal class PlaybackLoaderHelper
    {
        private readonly List<XRSessionSubsystemDescriptor> _sessionSubsystemDescriptors = new();
        private readonly List<XRCameraSubsystemDescriptor> _cameraSubsystemDescriptors = new();
        private readonly List<XROcclusionSubsystemDescriptor> _occlusionSubsystemDescriptors = new();
        private readonly List<XRInputSubsystemDescriptor> _inputSubsystemDescriptors = new();

        private LightshipPlaybackInputProvider _inputProvider;

        internal PlaybackDatasetReader DatasetReader { get; private set; }

        /// <summary>
        /// Initializes the loader. This step has to be done before the native helper is initialized, so the native
        /// helper knows if lidar support is in the dataset.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        internal bool InitializeBeforeNativeHelper(ILightshipInternalLoaderSupport loader)
        {
            Log.Info("Initialize Playback subsystems");

            var settings = LightshipSettingsHelper.ActiveSettings;
            var dataset = PlaybackDatasetLoader.Load(settings.PlaybackDatasetPath);

            if (dataset == null)
            {
                Log.Error("Failed to initialize Playback subsystems because no dataset was loaded.");
                return false;
            }

            DatasetReader = new PlaybackDatasetReader(dataset, settings.LoopPlaybackInfinitely);

            loader.CreateSubsystem<XRSessionSubsystemDescriptor, XRSessionSubsystem>
            (
                _sessionSubsystemDescriptors,
                "Lightship-Playback-Session"
            );

            var sessionSubsystem = loader.GetLoadedSubsystem<XRSessionSubsystem>();
            ((IPlaybackDatasetUser)sessionSubsystem).SetPlaybackDatasetReader(DatasetReader);

            loader.CreateSubsystem<XRCameraSubsystemDescriptor, XRCameraSubsystem>
            (
                _cameraSubsystemDescriptors,
                "Lightship-Playback-Camera"
            );

            var cameraSubsystem = loader.GetLoadedSubsystem<XRCameraSubsystem>();

            if (sessionSubsystem == null)
            {
                // Subsystems can only be loaded in Play Mode
                Log.Error("Failed to load subsystem.");
                return false;
            }

            ((IPlaybackDatasetUser)cameraSubsystem).SetPlaybackDatasetReader(DatasetReader);

            if (dataset.LidarEnabled && (!settings.UseLightshipDepth || settings.PreferLidarIfAvailable))
            {
                loader.DestroySubsystem<XROcclusionSubsystem>();

                Log.Info("Creating " + nameof(LightshipPlaybackOcclusionSubsystem));
                loader.CreateSubsystem<XROcclusionSubsystemDescriptor, XROcclusionSubsystem>
                (
                    _occlusionSubsystemDescriptors,
                    "Lightship-Playback-Occlusion"
                );

                var occlusionSubsystem = loader.GetLoadedSubsystem<XROcclusionSubsystem>();
                ((IPlaybackDatasetUser)occlusionSubsystem).SetPlaybackDatasetReader(DatasetReader);
            }

            ConfigureLocationAndCompass(true, DatasetReader);

            return true;
        }

        /// <summary>
        /// Initializes the loader. This step has to be done after the native helper is initialized because the Unity
        /// context has to exist.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        internal bool InitializeAfterNativeHelper(ILightshipInternalLoaderSupport loader)
        {
            // Input is an integrated subsystem that must be created after the LightshipUnityContext is initialized,
            // which is why it's done here instead of in the PlaybackLoaderHelper
            Log.Info("Creating " + nameof(LightshipPlaybackInputProvider));
            _inputProvider = new LightshipPlaybackInputProvider();
            _inputProvider.SetPlaybackDatasetReader(DatasetReader);

            loader.DestroySubsystem<XRInputSubsystem>();
            loader.CreateSubsystem<XRInputSubsystemDescriptor, XRInputSubsystem>
            (
                _inputSubsystemDescriptors,
                "LightshipInput"
            );

            return true;
        }

        /// <summary>
        /// Destroys each initialized subsystem.
        /// </summary>
        /// <returns>Always returns `true`.</returns>
        internal bool Deinitialize(ILightshipInternalLoaderSupport loader)
        {
            Log.Info("Deinitialize playback subsystems");
            if (loader == null)
            {
                Log.Warning("Loader is null. Assuming system is already deinitialized.");
                return true;
            }

            _inputProvider?.Dispose();
            DatasetReader = null;
            ConfigureLocationAndCompass(false, null);

            var sessionSubsystem = loader.GetLoadedSubsystem<XRSessionSubsystem>();
            if (sessionSubsystem != null)
            {
                if (sessionSubsystem.running)
                {
                    sessionSubsystem.Stop();
                }

                loader.DestroySubsystem<XRSessionSubsystem>();
            }

            var xrCameraSubsystem = loader.GetLoadedSubsystem<XRCameraSubsystem>();
            if (xrCameraSubsystem != null)
            {
                if (xrCameraSubsystem.running)
                {
                    xrCameraSubsystem.Stop();
                }

                loader.DestroySubsystem<XRCameraSubsystem>();
            }

            return true;
        }

        private static void ConfigureLocationAndCompass(bool isLoaded, PlaybackDatasetReader datasetReader)
        {
            ((IPlaybackDatasetUser)Input.location).SetPlaybackDatasetReader(datasetReader);
            ((IPlaybackDatasetUser)Input.compass).SetPlaybackDatasetReader(datasetReader);
        }
    }
}
