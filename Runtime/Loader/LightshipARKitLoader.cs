// Restrict inclusion to iOS builds to avoid symbol resolution error

#if UNITY_IOS || UNITY_EDITOR

#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED && UNITY_IOS && !UNITY_EDITOR
#define NIANTIC_LIGHTSHIP_ARKIT_LOADER_ENABLED
#endif

using System;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Playback;
using Niantic.Lightship.AR.Subsystems;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Loader
{
    /// <summary>
    /// Manages the lifecycle of Lightship and ARKit subsystems.
    /// </summary>
    public class LightshipARKitLoader : ARKitLoader, _ILightshipLoader
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
#if NIANTIC_LIGHTSHIP_ARKIT_LOADER_ENABLED
            bool initializationSuccess;
            bool isLidarSupported;

            if (settings.UsePlaybackOnDevice)
            {
                // Initialize Playback subsystems instead of initializing ARKit subsystems
                // (for those features that aren't added/supplanted by Lightship),
                _playbackHelper = new _PlaybackLoaderHelper();
                initializationSuccess = _playbackHelper.Initialize(this, settings);

                // When in playback mode, Lidar device support is dictated whether the Playback input
                // has LiDAR data or not
                isLidarSupported = _playbackHelper.DatasetReader.GetIsLidarAvailable();
            }
            else
            {
                // Initialize ARKit subsystems
                initializationSuccess = base.Initialize();

                // Determine if device supports LiDAR only during the window where
                // AFTER ARKit loader initializes but BEFORE Lightship loader initializes
                isLidarSupported = _ILightshipLoader.IsLidarSupported();
            }

            // Don't initialize lightship subsystems if ARKit's initialization has already failed.
            if (!initializationSuccess)
                return false;

            // Must initialize Lightship subsystems after ARKit's, because when there's overlap, the native helper will
            // (1) destroy ARKit's subsystems and then
            // (2) create Lightship's version of the subsystems
            _nativeHelper = new _NativeLoaderHelper();
            return _nativeHelper.Initialize(this, settings, isLidarSupported);
#else
            return false;
#endif
        }

        /// <summary>
        /// This method does nothing. Subsystems must be started individually.
        /// </summary>
        /// <returns>Returns `true` on iOS. Returns `false` otherwise.</returns>
        public override bool Start()
        {
#if NIANTIC_LIGHTSHIP_ARKIT_LOADER_ENABLED
            return base.Start();
#else
            return false;
#endif
        }

        /// <summary>
        /// This method does nothing. Subsystems must be stopped individually.
        /// </summary>
        /// <returns>Returns `true` on iOS. Returns `false` otherwise.</returns>
        public override bool Stop()
        {
#if NIANTIC_LIGHTSHIP_ARKIT_LOADER_ENABLED
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
#if NIANTIC_LIGHTSHIP_ARKIT_LOADER_ENABLED
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
