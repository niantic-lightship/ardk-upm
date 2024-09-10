// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.Loader
{
    public class LightshipLoaderHelper
    {
        private readonly PlaybackLoaderHelper _playbackLoaderHelper;
        private readonly NativeLoaderHelper _nativeLoaderHelper;

        private ILightshipInternalLoaderSupport _loader;
        private readonly List<ILightshipExternalLoader> _externalLoaders;

        // Constructs NativeLoaderHelper and PlaybackLoaderHelper components based on the given settings
        internal LightshipLoaderHelper(List<ILightshipExternalLoader> externalLoaders = null)
        {
            _externalLoaders = externalLoaders ?? new List<ILightshipExternalLoader>();
            _nativeLoaderHelper = new NativeLoaderHelper();
            if (LightshipSettingsHelper.ActiveSettings.UsePlayback)
            {
                _playbackLoaderHelper = new PlaybackLoaderHelper();
            }
        }

        // Constructor with externally defined NativeLoaderHelper and PlaybackLoaderHelper components
        internal LightshipLoaderHelper
        (
            NativeLoaderHelper nativeLoaderHelper,
            PlaybackLoaderHelper playbackLoaderHelper
        )
        {
            _nativeLoaderHelper = nativeLoaderHelper;
            _playbackLoaderHelper = playbackLoaderHelper;
        }

        /// <summary>
        /// Initializes the loader. Additionally added external loader will be loaded after this core loader is done.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        internal bool Initialize(ILightshipInternalLoaderSupport loader)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _loader = loader;
            var initializationSuccess = true;

            MonoBehaviourEventDispatcher.Create();
            InputReader.Initialize();

            var settings = LightshipSettingsHelper.ActiveSettings;
            if (settings.UsePlayback)
            {
                initializationSuccess &= _playbackLoaderHelper.InitializeBeforeNativeHelper(_loader);
            }
            else
            {
                // Initialize possible ARCore / ARKit / ... subsystems
                initializationSuccess &= _loader.InitializePlatform();
            }

            // Don't continue if initialization has already failed.
            if (!initializationSuccess)
            {
                return false;
            }

            // Determine if device supports LiDAR only during the window where AFTER arf loader initializes but BEFORE
            // lightship loader initializes as non-playback relies on checking the existence of arf's meshing subsystem
            var isLidarSupported = settings.UsePlayback
                ? _playbackLoaderHelper.DatasetReader.GetIsLidarAvailable()
                : _loader.IsPlatformDepthAvailable();

            initializationSuccess &= _nativeLoaderHelper.Initialize(_loader, isLidarSupported);

            if (settings.UsePlayback)
            {
                initializationSuccess &= _playbackLoaderHelper.InitializeAfterNativeHelper(_loader);
            }

            // Initialise external loaders last because they might depend on core subsystems:
            if (_externalLoaders != null)
            {
                foreach(ILightshipExternalLoader externalLoader in _externalLoaders)
                    initializationSuccess &= externalLoader.Initialize(_loader);
            }

            return initializationSuccess;
#else
            return false;
#endif
        }

        public bool Deinitialize()
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            // Destroy external loaders first because they might depend on core subsystems:
            if (_externalLoaders != null)
            {
                foreach (ILightshipExternalLoader externalLoader in _externalLoaders)
                    externalLoader.Deinitialize(_loader);
            }

            _playbackLoaderHelper?.Deinitialize(_loader);
            _nativeLoaderHelper?.Deinitialize(_loader);

            if (_playbackLoaderHelper == null)
            {
                _loader.DeinitializePlatform();
            }

            InputReader.Shutdown();
            MonoBehaviourEventDispatcher.DestroySelf();
#endif
            return true;
        }
    }
}
