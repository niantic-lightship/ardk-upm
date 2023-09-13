// Restrict inclusion to Android builds to avoid symbol resolution error

#if UNITY_ANDROID || UNITY_EDITOR

#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED && UNITY_ANDROID && !UNITY_EDITOR
#define NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
#endif

using Niantic.Lightship.AR.PAM;
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
    public class LightshipARCoreLoader : ARCoreLoader, ILightshipLoader
    {
        private PlaybackLoaderHelper _playbackHelper;
        private NativeLoaderHelper _nativeHelper;

        PlaybackDatasetReader ILightshipLoader.PlaybackDatasetReader => _playbackHelper?.DatasetReader;

        /// <summary>
        /// Optional override settings for manual XR Loader initialization
        /// </summary>
        public LightshipSettings InitializationSettings { get; set; }

        /// <summary>
        /// The `XROcclusionSubsystem` whose lifecycle is managed by this loader.
        /// </summary>
        public XROcclusionSubsystem LightshipOcclusionSubsystem => GetLoadedSubsystem<XROcclusionSubsystem>();

        /// <summary>
        /// The `XRPersistentAnchorSubsystem` whose lifecycle is managed by this loader.
        /// </summary>
        public XRPersistentAnchorSubsystem LightshipPersistentAnchorSubsystem =>
            GetLoadedSubsystem<XRPersistentAnchorSubsystem>();

        /// <summary>
        /// Initializes the loader.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public override bool Initialize()
        {
            if (InitializationSettings == null)
            {
                InitializationSettings = LightshipSettings.Instance;
            }

            return ((ILightshipLoader)this).InitializeWithSettings(InitializationSettings);
        }

        bool ILightshipLoader.InitializeWithSettings(LightshipSettings settings, bool isTest)
        {
#if NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
            bool initializationSuccess;

            if (settings.DevicePlaybackEnabled)
            {
                // Initialize Playback subsystems instead of initializing ARCore subsystems
                // (for those features that aren't added/supplanted by Lightship),
                _playbackHelper = new PlaybackLoaderHelper();
                initializationSuccess = _playbackHelper.Initialize(this, settings);
            }
            else
            {
                // Initialize ARCore subsystems
                initializationSuccess = base.Initialize();
            }

            // Don't initialize lightship subsystems if ARCore's initialization has already failed.
            if (!initializationSuccess)
            {
                return false;
            }

            // Must initialize Lightship subsystems after ARCore's, because when there's overlap, the native helper will
            // (1) destroy ARCore's subsystems and then
            // (2) create Lightship's version of the subsystems
            _nativeHelper = new NativeLoaderHelper();

            // Determine if device supports LiDAR only during the window where AFTER arf loader initializes but BEFORE
            // lightship loader initializes as non-playback relies on checking the existence of arf's meshing subsystem
            var isLidarSupported = settings.DevicePlaybackEnabled
                ? _playbackHelper.DatasetReader.GetIsLidarAvailable()
                : _nativeHelper.DetermineIfDeviceSupportsLidar();

            initializationSuccess &= _nativeHelper.Initialize(this, settings, isLidarSupported, isTest);

            if (!settings.DevicePlaybackEnabled)
            {
                MonoBehaviourEventDispatcher.Updating.AddListener(SelectCameraConfiguration);
            }

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
            const int minWidth = DataFormatConstants.Jpeg_720_540_ImgWidth;
            const int minHeight = DataFormatConstants.Jpeg_720_540_ImgHeight;

            var cameraSubsystem = GetLoadedSubsystem<XRCameraSubsystem>();
            if (!cameraSubsystem.running)
            {
                return;
            }

            Debug.Log("Trying to select XRCameraConfiguration...");
            using (var configurations = cameraSubsystem.GetConfigurations(Allocator.Temp))
            {
                if (configurations.Length == 0)
                {
                    return;
                }

                // Once we have the first frame with configurations, don't need to check on Update anymore
                MonoBehaviourEventDispatcher.Updating.RemoveListener(SelectCameraConfiguration);

                // This clause is here because, in the case that dev has already set their own custom configuration,
                // we don't want to silently override it. Pretty sure currentConfiguration will always be non-null
                // if GetConfigurations returns a non-zero array.
                if (cameraSubsystem.currentConfiguration.HasValue)
                {
                    var currentConfig = cameraSubsystem.currentConfiguration.Value;
                    var currResolution = currentConfig.resolution;
                    if (MeetsResolutionMinimums(currResolution, minWidth, minHeight))
                    {
                        return;
                    }
                }

                XRCameraConfiguration selectedConfig = default;
                foreach (var config in configurations)
                {
                    //Select the first configuration that meets the resolution minimum assuming
                    //that the configuration will correspond with the default camera use by ARCore
                    //TODO: Verify with Unity that this assumption hold true or
                    //if they can provide us with better API
                    if (MeetsResolutionMinimums(config.resolution, minWidth, minHeight))
                    {
                        selectedConfig = config;
                        break;
                    }
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
    }
}

#endif
