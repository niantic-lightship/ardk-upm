// Copyright 2023 Niantic, Inc. All Rights Reserved.
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Log;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Subsystems.Playback;
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
            Log.Info("Initialize Playback subsystems");

            var dataset = PlaybackDatasetLoader.Load(settings.PlaybackDatasetPath);

            if (dataset == null)
            {
                Log.Error("Failed to initialize Playback subsystems because no dataset was loaded.");
                return false;
            }

            DatasetReader = new PlaybackDatasetReader
            (
                dataset,
                settings.LoopInfinitely
            );

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

            InitializeInput(settings, DatasetReader);

            return true;
        }

        /// <summary>
        /// Destroys each initialized subsystem.
        /// </summary>
        /// <returns>Always returns `true`.</returns>
        public bool Deinitialize(XRLoaderHelper loader)
        {
            Log.Info("Deinitialize playback subsystems");
            loader.DestroySubsystem<XRSessionSubsystem>();
            loader.DestroySubsystem<XRCameraSubsystem>();

            DatasetReader = null;
            InitializeInput(null, null);

            return true;
        }

        private static void InitializeInput(LightshipSettings settings, PlaybackDatasetReader datasetReader)
        {
            ((ILightshipSettingsUser)Input.location).SetLightshipSettings(settings);
            ((IPlaybackDatasetUser)Input.location).SetPlaybackDatasetReader(datasetReader);

            ((ILightshipSettingsUser)Input.compass).SetLightshipSettings(settings);
            ((IPlaybackDatasetUser)Input.compass).SetPlaybackDatasetReader(datasetReader);
        }
    }
}
