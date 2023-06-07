// Restrict inclusion to Android builds to avoid symbol resolution error

#if UNITY_ANDROID || UNITY_EDITOR

#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED && UNITY_ANDROID && !UNITY_EDITOR
#define NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
#endif

using System;
using Niantic.Lightship.AR.PlatformAdapterManager;
using Niantic.Lightship.AR.Playback;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARCore;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Loader
{
    /// <summary>
    /// Manages the lifecycle of Lightship and ARCore subsystems.
    /// </summary>
    public class LightshipARCoreLoader : ARCoreLoader, _ILightshipLoader
    {
        /// <summary>
        /// The `XROcclusionSubsystem` whose lifecycle is managed by this loader.
        /// </summary>
        public XROcclusionSubsystem LightshipOcclusionSubsystem => GetLoadedSubsystem<XROcclusionSubsystem>();

        /// <summary>
        /// The `XRPersistentAnchorSubsystem` whose lifecycle is managed by this loader.
        /// </summary>
        public XRPersistentAnchorSubsystem LightshipPersistentAnchorSubsystem =>
            GetLoadedSubsystem<XRPersistentAnchorSubsystem>();

        private _PlaybackLoaderHelper _playbackHelper;
        private _NativeLoaderHelper _nativeHelper;

        /// <summary>
        /// Initializes the loader.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public override bool Initialize()
        {
            return ((_ILightshipLoader)this).InitializeWithSettings(LightshipSettings.Instance);
        }

        bool _ILightshipLoader.InitializeWithSettings(LightshipSettings settings)
        {
#if NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
            bool initializationSuccess;
            bool isLidarSupported;

            if (settings.UsePlaybackOnDevice)
            {
                // Initialize Playback subsystems instead of initializing ARCore subsystems
                // (for those features that aren't added/supplanted by Lightship),
                _playbackHelper = new _PlaybackLoaderHelper();
                initializationSuccess = _playbackHelper.Initialize(this, settings);

                // When in playback mode, Lidar device support is dictated whether the Playback input
                // has LiDAR data or not
                isLidarSupported = _playbackHelper.DatasetReader.GetIsLidarAvailable();
            }
            else
            {
                // Initialize ARCore subsystems
                initializationSuccess = base.Initialize();

                // Determine if device supports LiDAR only during the window where
                // AFTER arf loader initializes but BEFORE lightship loader initializes
                isLidarSupported = _ILightshipLoader.IsLidarSupported();
            }

            // Don't initialize lightship subsystems if ARCore's initialization has already failed.
            if (!initializationSuccess)
                return false;

            // Must initialize Lightship subsystems after ARCore's, because when there's overlap, the native helper will
            // (1) destroy ARCore's subsystems and then
            // (2) create Lightship's version of the subsystems
            _nativeHelper = new _NativeLoaderHelper();
            initializationSuccess &= _nativeHelper.Initialize(this, settings, isLidarSupported);

            if (!settings.UsePlaybackOnDevice)
                _MonoBehaviourEventDispatcher.Updating += SelectCameraConfiguration;

            return initializationSuccess;
#else
            return false;
#endif
        }

        // The default camera image resolution from ARCore is 640x480, which is not large enough for the image
        // input required by VPS. Thus we need to increase the resolution by changing the camera configuration.
        // We need to check on Update if configurations are available, because they aren't available until the
        // XRCameraSubsystem is running (and that's not controlled by Lightship). Fortunately, configurations
        // become available during the gap between when the subsystem starts running and the first frame is
        // surfaced, meaning there's no visible hitch when the configuration is changed.
        private void SelectCameraConfiguration()
        {
            const int minWidth = _DataFormatConstants.JPEG_720_540_IMG_WIDTH;
            const int minHeight = _DataFormatConstants.JPEG_720_540_IMG_HEIGHT;

            var cameraSubsystem = GetLoadedSubsystem<XRCameraSubsystem>();
            if (!cameraSubsystem.running)
                return;

            Debug.Log("Trying to select XRCameraConfiguration...");
            using (var configurations = cameraSubsystem.GetConfigurations(Allocator.Temp))
            {
                if (configurations.Length == 0)
                    return;

                // Once we have the first frame with configurations, don't need to check on Update anymore
                _MonoBehaviourEventDispatcher.Updating -= SelectCameraConfiguration;

                // This clause is here because, in the case that dev has already set their own custom configuration,
                // we don't want to silently override it. Pretty sure currentConfiguration will always be non-null
                // if GetConfigurations returns a non-zero array.
                if (cameraSubsystem.currentConfiguration.HasValue)
                {
                    var currentConfig = cameraSubsystem.currentConfiguration.Value;
                    var currResolution = currentConfig.resolution;
                    if (MeetsResolutionMinimums(currResolution, minWidth, minHeight))
                        return;

                    // Note: This log will print whether or not the current configuration was set by default or by
                    // the dev. That's what we want, in order to notify the dev that this is happening in all cases.
                    Debug.LogWarning
                    (
                        $"The current camera configuration resolution of {currResolution.x}x{currResolution.y} " +
                        "does not meet the minimum resolution required by Lightship features. " +
                        "A camera configuration with a higher resolution will be selected."
                    );
                }

                XRCameraConfiguration selectedConfig = default;

                foreach (var config in configurations)
                {
                    if (!MeetsResolutionMinimums(config.resolution, minWidth, minHeight))
                        continue;

                    // If no config has been selected yet OR the selected config resolution is larger than necessary,
                    // then the current config becomes the selected config
                    if (selectedConfig.resolution.x == 0 || selectedConfig.resolution.x > config.resolution.x)
                        selectedConfig = config;
                }

                if (selectedConfig == default ||
                    !MeetsResolutionMinimums(selectedConfig.resolution, minWidth, minHeight))
                {
                    Debug.LogError("No available camera configuration meets Lightship requirements.");
                    return;
                }

                Debug.Log("Setting XRCameraConfiguration as: " + selectedConfig);
                cameraSubsystem.currentConfiguration = selectedConfig;
            }
        }

        private bool MeetsResolutionMinimums(Vector2Int resolution, int minWidth, int minHeight)
        {
            return resolution.x >= minWidth && resolution.y >= minHeight;
        }

        /// <summary>
        /// This method does nothing. Subsystems must be started individually.
        /// </summary>
        /// <returns>Returns `true` on Android. Returns `false` otherwise.</returns>
        public override bool Start()
        {
#if NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
            return base.Start();
#else
            return false;
#endif
        }

        /// <summary>
        /// This method does nothing. Subsystems must be stopped individually.
        /// </summary>
        /// <returns>Returns `true` on Android. Returns `false` otherwise.</returns>
        public override bool Stop()
        {
#if NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
            return base.Stop();
#else
            return false;
#endif
        }

        /// <summary>
        /// Destroys each subsystem.
        /// </summary>
        /// <returns>Always returns `true`.</returns>
        public override bool Deinitialize()
        {
#if NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
            _nativeHelper?.Deinitialize(this);
            _playbackHelper?.Deinitialize(this);

            return base.Deinitialize();
#else
            return true;
#endif
        }

        _PlaybackDatasetReader _ILightshipLoader.PlaybackDatasetReader => _playbackHelper?.DatasetReader;
    }
}

#endif
