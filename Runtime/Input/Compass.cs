// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Subsystems.Playback;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR
{
    /// <summary>
    ///   <para>Interface into compass functionality.</para>
    /// </summary>
    public class Compass: IPlaybackDatasetUser, ILightshipSettingsUser
    {
        /// <summary>
        ///   <para>The heading in degrees relative to the magnetic North Pole. (Read Only)</para>
        /// </summary>
        public float magneticHeading => GetOrCreateProvider().magneticHeading;

        /// <summary>
        ///   <para>The heading in degrees relative to the geographic North Pole. (Read Only)</para>
        /// </summary>
        public float trueHeading => GetOrCreateProvider().trueHeading;

        /// <summary>
        ///   <para>Accuracy of heading reading in degrees.</para>
        /// </summary>
        public float headingAccuracy => GetOrCreateProvider().headingAccuracy;

        /// <summary>
        ///   <para>The raw geomagnetic data measured in microteslas. (Read Only)</para>
        /// </summary>
        public Vector3 rawVector => GetOrCreateProvider().raw;

        /// <summary>
        ///   <para>Timestamp (in seconds since 1970) when the heading was last time updated. (Read Only)</para>
        /// </summary>
        public double timestamp => GetOrCreateProvider().timestamp;

        /// <summary>
        ///   <para>Used to enable or disable compass. Note, that if you want Input.compass.trueHeading property to contain a valid value, you must also enable location updates by calling Input.location.Start().</para>
        /// </summary>
        public bool enabled
        {
            get => GetOrCreateProvider().IsHeadingUpdatesEnabled();
            set => GetOrCreateProvider().SetHeadingUpdatesEnabled(value);
        }

        internal ICompassProvider Provider => _provider;

        private ICompassProvider _provider;
        private LightshipSettings _lightshipSettings;
        private bool m_PrevWasEnabled;

        private ICompassProvider GetOrCreateProvider()
        {
            if (_provider == null)
            {
                CreateProvider();
            }

            return _provider;
        }

        // Logic for determining what the next provider implementation should be is kept contained
        // to this class, otherwise it would be _split_ between this class and the PlaybackLoaderHelper class,
        // because we need to account for when this method is invoked without XR having been initialized.
        private void CreateProvider()
        {
            var nextIsPlayback = _lightshipSettings != null && _lightshipSettings.UsePlayback;

            if (_provider == null)
            {
                CreateProvider(nextIsPlayback);
                return;
            }

            var currHasStarted = _provider.IsHeadingUpdatesEnabled();

            if (nextIsPlayback)
            {
                ReplaceProvider(InputImplementationType.Playback, currHasStarted);
            }
            else if (Application.isEditor)
            {
                ReplaceProvider(InputImplementationType.Mock, currHasStarted);
            }
            else
            {
                ReplaceProvider(InputImplementationType.Unity, currHasStarted);
            }
        }

        private void CreateProvider(bool isPlayback)
        {
            if (isPlayback)
            {
                _provider = new PlaybackCompassProvider();
            }
            else
            {
                _provider = Application.isEditor
                    ? new MockCompassProvider()
                    : new UnityCompassProvider();
            }
        }

        // Switch from the Mock to Playback implementation and back are expected scenariso,
        // as the lifetime/usage of the LocationService is independent of XR.
        //
        // Switching from the Unity and Playback/Mock providers and back is only expected to happen in
        // testing scenarios.
        private void ReplaceProvider(InputImplementationType nextImplType, bool currIsRunning)
        {
            if (_provider.ImplementationType == nextImplType)
            {
                // Replacing a provider with another of the same type will never happen currently.
                // But if it happens, just keep the same provider.
                return;
            }

            var nextIsRunning = currIsRunning;

#if UNITY_EDITOR
            // Silence warning logs when exiting Play Mode
            var exitingPlayMode = !EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.isPlaying;
            nextIsRunning &= !exitingPlayMode;
#endif

            switch (nextImplType)
            {
                case InputImplementationType.Unity:
                    _provider = new UnityCompassProvider();
                    if (nextIsRunning)
                    {
                        if (!_provider.IsHeadingUpdatesEnabled())
                        {
                            Log.Warning
                            (
                                "Compass service implementation was reinitialized to now be provided by the device." +
                                "There will be a short disruption in service while it initializes."
                            );
                        }
                    }

                    break;

                case InputImplementationType.Mock:
                    var oldProvider = _provider;
                    _provider = new MockCompassProvider(oldProvider);
                    if (nextIsRunning)
                    {
                        // Log is specifically about switching from the Playback --> Mock implementation, because
                        // switching from the Unity --> Mock implementation is not expected to ever happen.
                        Log.Warning
                        (
                            "Because XR was deinitialized, there is no Playback dataset for Input.compass to draw " +
                            "data updates from. While the compass will continue to be enabled, data " +
                            "will not be updated."
                        );
                    }

                    break;
                case InputImplementationType.Playback:
                    _provider = new PlaybackCompassProvider();
                    break;
            }

            if (nextIsRunning)
            {
                _provider.SetHeadingUpdatesEnabled(true);
            }
        }

        internal Compass() {}

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            if (reader == null)
            {
                if (_provider is PlaybackCompassProvider)
                {
                    Log.Error
                    (
                        "Cannot set the PlaybackDatasetReader of an active PlaybackCompassProvider to null."
                    );
                }

                return;
            }

            if (_provider is PlaybackCompassProvider playbackProvider)
            {
                playbackProvider.SetPlaybackDatasetReader(reader);
            }
        }

        void ILightshipSettingsUser.SetLightshipSettings(LightshipSettings settings)
        {
            _lightshipSettings = settings;
            CreateProvider();
        }

        internal interface ICompassProvider
        {
            float magneticHeading { get; }
            float trueHeading { get; }
            float headingAccuracy { get; }
            Vector3 raw { get; }
            double timestamp { get; }

            InputImplementationType ImplementationType { get; }

            bool IsHeadingUpdatesEnabled();
            void SetHeadingUpdatesEnabled(bool value);
        }

        private class PlaybackCompassProvider: ICompassProvider, IPlaybackDatasetUser
        {
            public InputImplementationType ImplementationType => InputImplementationType.Playback;

            private bool _enabled;
            private PlaybackDatasetReader _datasetReader;

            // TODO [AR-17775]
            // Magnetic Heading was not recorded in playback datasets,
            // so this will return the true heading (for now)
            public float magneticHeading
            {
                get
                {
                    if (CanReturnValidValues)
                        return  _datasetReader.CurrFrame.LocationInfo.Heading;

                    return 0;
                }
            }

            public float trueHeading
            {
                get
                {
                    if (CanReturnValidValues && Input.location.status == LocationServiceStatus.Running)
                        return  _datasetReader.CurrFrame.LocationInfo.Heading;

                    return 0;
                }
            }

            public float headingAccuracy
            {
                get
                {
                    if (CanReturnValidValues)
                        return _datasetReader.CurrFrame.LocationInfo.HeadingAccuracy;

                    return 0;
                }
            }

            // TODO [AR-17775]
            // The raw geomagnetic data was not recorded in playback datasets,
            // so this will invalid (but non-zero) values for now.
            public Vector3 raw
            {
                get
                {
                    if (CanReturnValidValues)
                        return Vector3.one;

                    return Vector3.zero;
                }
            }

            public double timestamp
            {
                get
                {
                    if (CanReturnValidValues)
                        return _datasetReader.CurrFrame.LocationInfo.HeadingTimestamp;

                    return 0;
                }
            }

            private bool CanReturnValidValues => _enabled && _datasetReader?.CurrFrame?.LocationInfo != null;

            public bool IsHeadingUpdatesEnabled() => _enabled && _datasetReader.GetCompassEnabled();

            public void SetHeadingUpdatesEnabled(bool enabled)
            {
                _enabled = enabled;
            }

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                _datasetReader = reader;
            }
        }

        private class MockCompassProvider : ICompassProvider
        {
            public InputImplementationType ImplementationType => InputImplementationType.Mock;

            public float magneticHeading { get; }
            public float trueHeading { get; }
            public float headingAccuracy { get; }
            public Vector3 raw { get; }
            public double timestamp { get; }

            private bool _isHeadingUpdatesEnabled;

            public MockCompassProvider()
            {
            }

            public MockCompassProvider(ICompassProvider oldProvider)
            {
                magneticHeading = oldProvider.magneticHeading;
                trueHeading = oldProvider.trueHeading;
                headingAccuracy = oldProvider.headingAccuracy;
                raw = oldProvider.raw;
                timestamp = oldProvider.timestamp;
                _isHeadingUpdatesEnabled = oldProvider.IsHeadingUpdatesEnabled();
            }

            public bool IsHeadingUpdatesEnabled()
            {
                return _isHeadingUpdatesEnabled;
            }

            public void SetHeadingUpdatesEnabled(bool value)
            {
                _isHeadingUpdatesEnabled = value;
            }
        }

        private class UnityCompassProvider : ICompassProvider
        {
            public InputImplementationType ImplementationType => InputImplementationType.Unity;

            public float magneticHeading => UnityEngine.Input.compass.magneticHeading;

            public float trueHeading => UnityEngine.Input.compass.trueHeading;
            public float headingAccuracy => UnityEngine.Input.compass.headingAccuracy;
            public Vector3 raw => UnityEngine.Input.compass.rawVector;
            public double timestamp => UnityEngine.Input.compass.timestamp;

            public bool IsHeadingUpdatesEnabled() => UnityEngine.Input.compass.enabled;

            public void SetHeadingUpdatesEnabled(bool value)
            {
                UnityEngine.Input.compass.enabled = value;
            }
        }
    }
}
