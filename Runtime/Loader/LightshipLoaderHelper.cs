// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;

namespace Niantic.Lightship.AR.Loader
{
    public class LightshipLoaderHelper
    {
        private readonly PlaybackLoaderHelper _playbackLoaderHelper;
        private readonly NativeLoaderHelper _nativeLoaderHelper;
        private readonly LightshipSettings _initializationSettings;

        private ILightshipInternalLoaderSupport _loader;
        private readonly List<ILightshipExternalLoader> _externalLoaders;

        // Use this constructor in loader to reduce duplication of code
        internal LightshipLoaderHelper(LightshipSettings settings) : this(settings, new List<ILightshipExternalLoader>()) { }

        internal LightshipLoaderHelper(LightshipSettings settings, List<ILightshipExternalLoader> externalLoaders)
        {
            _initializationSettings = settings;
            _externalLoaders = externalLoaders ?? throw new ArgumentNullException();
            _nativeLoaderHelper = new NativeLoaderHelper();
            if (_initializationSettings.UsePlayback)
            {
                _playbackLoaderHelper = new PlaybackLoaderHelper();
            }
        }

        // Use this constructor when injecting in tests.
        internal LightshipLoaderHelper(LightshipSettings settings, NativeLoaderHelper nativeLoaderHelper, PlaybackLoaderHelper playbackLoaderHelper)
        {
            _initializationSettings = settings;
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

            InputReader.Initialize();

            if (_initializationSettings.UsePlayback)
            {
                initializationSuccess &= _playbackLoaderHelper.InitializeBeforeNativeHelper(_loader, _initializationSettings);
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
            var isLidarSupported = _initializationSettings.UsePlayback
                ? _playbackLoaderHelper.DatasetReader.GetIsLidarAvailable()
                : _loader.IsPlatformDepthAvailable();

            initializationSuccess &= _nativeLoaderHelper.Initialize(_loader, _initializationSettings, isLidarSupported);

            if (_initializationSettings.UsePlayback)
            {
                initializationSuccess &= _playbackLoaderHelper.InitializeAfterNativeHelper(_loader, _initializationSettings);
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
                return _loader.DeinitializePlatform();
            }

            InputReader.Shutdown();
#endif
            return true;
        }
    }
}
