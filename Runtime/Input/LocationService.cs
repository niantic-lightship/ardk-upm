// Copyright 2022-2024 Niantic.
using System;
using System.Reflection;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEditor;

namespace Niantic.Lightship.AR
{
    public class LocationService : IPlaybackDatasetUser, ILightshipSettingsUser
    {
        /// <summary>
        ///   <para>Indicates whether the device allows access the application to access the location service.</para>
        /// </summary>
        public bool isEnabledByUser => GetOrCreateProvider().isEnabledByUser;

        /// <summary>
        ///   <para>Returns the location service status.</para>
        /// </summary>
        public LocationServiceStatus status => GetOrCreateProvider().status;

        /// <summary>
        ///   <para>The last geographical location that the device registered.</para>
        /// </summary>
        public LocationInfo lastData => GetOrCreateProvider().lastData;

        /// <summary>
        ///   <para>Starts location service updates.</para>
        /// </summary>
        /// <param name="desiredAccuracyInMeters">
        ///     The service accuracy you want to use, in meters. This determines the accuracy of the device's last location coordinates. Higher values like 500 don't require the device to use its GPS chip and
        ///     thus save battery power. Lower values like 5-10 provide the best accuracy but require the GPS chip and thus use more battery power. The default value is 10 meters.
        /// </param>
        /// <param name="updateDistanceInMeters">
        ///     The minimum distance, in meters, that the device must move laterally before Unity updates Input.location. Higher values like 500 produce fewer updates and are less resource intensive to process. The default is 10 meters.
        /// </param>
        public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters) =>
            GetOrCreateProvider().Start(desiredAccuracyInMeters, updateDistanceInMeters);

        /// <summary>
        ///   <para>Starts location service updates.</para>
        /// </summary>
        /// <param name="desiredAccuracyInMeters">
        ///     The service accuracy you want to use, in meters. This determines the accuracy of the device's last location coordinates. Higher values like 500 don't require the device to use its GPS chip and
        ///     thus save battery power. Lower values like 5-10 provide the best accuracy but require the GPS chip and thus use more battery power. The default value is 10 meters.
        /// </param>
        /// <param name="updateDistanceInMeters">
        ///     The minimum distance, in meters, that the device must move laterally before Unity updates Input.location. Higher values like 500 produce fewer updates and are less resource intensive to process. The default is 10 meters.
        /// </param>
        public void Start(float desiredAccuracyInMeters) => GetOrCreateProvider().Start(desiredAccuracyInMeters);

        /// <summary>
        ///   <para>Starts location service updates.</para>
        /// </summary>
        /// <param name="desiredAccuracyInMeters">
        ///     The service accuracy you want to use, in meters. This determines the accuracy of the device's last location coordinates. Higher values like 500 don't require the device to use its GPS chip and
        ///     thus save battery power. Lower values like 5-10 provide the best accuracy but require the GPS chip and thus use more battery power. The default value is 10 meters.
        /// </param>
        /// <param name="updateDistanceInMeters">
        ///     The minimum distance, in meters, that the device must move laterally before Unity updates Input.location. Higher values like 500 produce fewer updates and are less resource intensive to process. The default is 10 meters.
        /// </param>
        public void Start() => GetOrCreateProvider().Start();

        /// <summary>
        ///   <para>Stops location service updates. This is useful to save battery power when the application doesn't require the location service.</para>
        /// </summary>
        public void Stop() => GetOrCreateProvider().Stop();

        internal ILocationServiceProvider Provider => _provider;

        private ILocationServiceProvider _provider;
        private LightshipSettings _lightshipSettings;

        // Defaults as defined by Unity
        private const float k_DefaultAccuracyInMeters = 10f;
        private const float k_DefaultUpdateDistanceInMeters = 10f;

        private ILocationServiceProvider GetOrCreateProvider()
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

            var currHasStarted =
                _provider.status == LocationServiceStatus.Running ||
                _provider.status == LocationServiceStatus.Initializing;

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
                _provider = new PlaybackLocationServiceProvider();
            }
            else
            {
                _provider = Application.isEditor
                    ? new MockLocationServiceProvider()
                    : new UnityLocationServiceProvider();
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

            var oldProvider = _provider;

            var nextIsRunning = currIsRunning;

#if UNITY_EDITOR
            // Silence warning logs when exiting Play Mode
            var exitingPlayMode = !EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.isPlaying;
            nextIsRunning &= !exitingPlayMode;
#endif

            switch (nextImplType)
            {
                case InputImplementationType.Unity:
                    _provider = new UnityLocationServiceProvider();
                    if (nextIsRunning)
                    {
                        if (_provider.status != LocationServiceStatus.Running)
                        {
                            Log.Warning
                            (
                                "Location service implementation was reinitialized to now be provided by the device." +
                                "There will be a short disruption in service while it initializes."
                            );
                        }

                        _provider.Start(oldProvider.AccuracyInMeters, oldProvider.UpdateDistanceInMeters);
                    }

                    break;

                case InputImplementationType.Mock:
                    _provider = new MockLocationServiceProvider(oldProvider);
                    if (nextIsRunning)
                    {
                        // Log is specifically about switching from the Playback --> Mock implementation, because
                        // switching from the Unity --> Mock implementation is not expected to ever happen.
                        Log.Warning
                        (
                            "Because XR was deinitialized, there is no Playback dataset for Input.location to draw " +
                            "data updates from. While the location service status will continue to be Running, data " +
                            "will not be updated."
                        );

                        ((MockLocationServiceProvider)_provider).StartImmediately
                        (
                            oldProvider.AccuracyInMeters,
                            oldProvider.UpdateDistanceInMeters
                        );
                    }

                    break;
                case InputImplementationType.Playback:
                    _provider = new PlaybackLocationServiceProvider();
                    if (nextIsRunning)
                    {
                        _provider.Start(oldProvider.AccuracyInMeters, oldProvider.UpdateDistanceInMeters);
                    }
                    break;
            }
        }

        internal LocationService()
        {
        }

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            if (reader == null)
            {
                if (_provider is PlaybackLocationServiceProvider)
                {
                    Log.Error
                    (
                        "Cannot set the PlaybackDatasetReader of an active PlaybackLocationServiceProvider to null."
                    );
                }

                return;
            }

            if (_provider is PlaybackLocationServiceProvider playbackProvider)
            {
                playbackProvider.SetPlaybackDatasetReader(reader);
            }
        }

        void ILightshipSettingsUser.SetLightshipSettings(LightshipSettings settings)
        {
            _lightshipSettings = settings;
            CreateProvider();
        }

        internal interface ILocationServiceProvider
        {
            bool isEnabledByUser { get; }
            LocationServiceStatus status { get; }

            LocationInfo lastData { get; }

            InputImplementationType ImplementationType { get; }

            float AccuracyInMeters { get; }
            float UpdateDistanceInMeters { get; }

            void Start(float desiredAccuracyInMeters, float updateDistanceInMeters);
            void Start(float desiredAccuracyInMeters);

            void Start();
            void Stop();
        }

        private class PlaybackLocationServiceProvider : ILocationServiceProvider, IPlaybackDatasetUser
        {
            public InputImplementationType ImplementationType => InputImplementationType.Playback;
            public float AccuracyInMeters { get; private set; }
            public float UpdateDistanceInMeters { get; private set; }

            public bool isEnabledByUser => _datasetReader.GetLocationServicesEnabled();

            public LocationServiceStatus status
            {
                get
                {
                    if (!_started)
                        return LocationServiceStatus.Stopped;

                    if (!isEnabledByUser)
                        return LocationServiceStatus.Failed;

                    if (_datasetReader.CurrFrame == null || _datasetReader.CurrFrame.LocationInfo == null)
                        return LocationServiceStatus.Initializing;

                    return LocationServiceStatus.Running;
                }
            }

            private LocationInfo _lastData;

            public LocationInfo lastData
            {
                get
                {
                    if (status != LocationServiceStatus.Running)
                    {
                        Log.Info
                        (
                            "Location service updates are not enabled. " +
                            "Check LocationService.status before querying last location."
                        );

                        return default(LocationInfo);
                    }

                    return ConvertToUnity(_datasetReader.CurrFrame?.LocationInfo);
                }
            }

            private bool _started;

            private PlaybackDatasetReader _datasetReader;

            public void Start(float desiredAccuracyInMeters)
            {
                Start(desiredAccuracyInMeters, k_DefaultUpdateDistanceInMeters);
            }

            public void Start()
            {
                Start(k_DefaultAccuracyInMeters, k_DefaultUpdateDistanceInMeters);
            }

            public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
            {
                // TODO [AR-16134]
                // Improve Input.location by filtering location updates with configured updateDistanceInMeters
                if (Math.Abs(desiredAccuracyInMeters - k_DefaultAccuracyInMeters) > 0.001 ||
                    Math.Abs(updateDistanceInMeters - k_DefaultUpdateDistanceInMeters) > 0.001)
                {
                    Log.Warning
                    (
                        "Customizing the location service's accuracy or update distance is not supported " +
                        "while using Lightship Playback."
                    );
                }

                AccuracyInMeters = desiredAccuracyInMeters;
                UpdateDistanceInMeters = updateDistanceInMeters;

                _started = true;
            }

            public void Stop()
            {
                _started = false;
            }

            private UnityEngine.LocationInfo ConvertToUnity(PlaybackDataset.LocationInfo info)
            {
                if (info == null)
                    return default;

                // Box here before passing into reflection method
                object unityInfo = new UnityEngine.LocationInfo();

                SetFieldViaReflection(unityInfo, "m_Timestamp", info.PositionTimestamp);
                SetFieldViaReflection(unityInfo, "m_Latitude", (float)info.Latitude);
                SetFieldViaReflection(unityInfo, "m_Longitude", (float)info.Longitude);
                SetFieldViaReflection(unityInfo, "m_Altitude", (float)info.Altitude);
                SetFieldViaReflection(unityInfo, "m_HorizontalAccuracy", info.PositionAccuracy);
                SetFieldViaReflection(unityInfo, "m_VerticalAccuracy", (float)info.AltitudeAccuracy);

                return (LocationInfo)unityInfo;
            }

            private static void SetFieldViaReflection(object o, string fieldName, object value)
            {
                var fi =
                    typeof(LocationInfo).GetField
                    (
                        fieldName,
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase
                    );

                fi.SetValue(o, value);
            }

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                _datasetReader = reader;
            }
        }

        private class MockLocationServiceProvider : ILocationServiceProvider
        {
            public InputImplementationType ImplementationType => InputImplementationType.Mock;
            public float AccuracyInMeters { get; private set; }
            public float UpdateDistanceInMeters { get; private set; }
            public bool isEnabledByUser { get; private set; }
            public LocationServiceStatus status { get; private set; }

            public LocationInfo lastData
            {
                get
                {
                    if (status != LocationServiceStatus.Running)
                    {
                        Log.Info
                        (
                            "Location service updates are not enabled. " +
                            "Check LocationService.status before querying last location."
                        );

                        return default;
                    }

                    return _lastData;
                }
            }

            private LocationInfo _lastData = default;

            public MockLocationServiceProvider()
            {
                isEnabledByUser = true;
            }

            public MockLocationServiceProvider(ILocationServiceProvider prevProvider)
            {
                isEnabledByUser = prevProvider.isEnabledByUser;
                _lastData = prevProvider.lastData;
            }

            public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
            {
                if (status == LocationServiceStatus.Stopped)
                {
                    if (!isEnabledByUser)
                    {
                        status = LocationServiceStatus.Failed;
                    }
                    else
                    {
                        AccuracyInMeters = desiredAccuracyInMeters;
                        UpdateDistanceInMeters = updateDistanceInMeters;

                        status = LocationServiceStatus.Initializing;
                        MonoBehaviourEventDispatcher.Updating.AddListener(SetStatusToRunning);
                    }
                }
            }

            public void Start(float desiredAccuracyInMeters)
            {
                Start(desiredAccuracyInMeters, k_DefaultUpdateDistanceInMeters);
            }

            public void Start()
            {
                Start(k_DefaultAccuracyInMeters, k_DefaultUpdateDistanceInMeters);
            }

            public void StartImmediately(float desiredAccuracyInMeters, float updateDistanceInMeters)
            {
                isEnabledByUser = true;
                AccuracyInMeters = desiredAccuracyInMeters;
                UpdateDistanceInMeters = updateDistanceInMeters;
                status = LocationServiceStatus.Running;
            }

            private void SetStatusToRunning()
            {
                MonoBehaviourEventDispatcher.Updating.RemoveListener(SetStatusToRunning);
                status = LocationServiceStatus.Running;
            }

            public void Stop()
            {
                if (status != LocationServiceStatus.Failed)
                {
                    status = LocationServiceStatus.Stopped;
                }
            }
        }

        private class UnityLocationServiceProvider : ILocationServiceProvider
        {
            public InputImplementationType ImplementationType => InputImplementationType.Unity;

            public float AccuracyInMeters { get; private set; }
            public float UpdateDistanceInMeters { get; private set; }

            public bool isEnabledByUser => UnityEngine.Input.location.isEnabledByUser;
            public LocationServiceStatus status => UnityEngine.Input.location.status;
            public LocationInfo lastData => UnityEngine.Input.location.lastData;

            public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
            {
                AccuracyInMeters = desiredAccuracyInMeters;
                UpdateDistanceInMeters = updateDistanceInMeters;

                UnityEngine.Input.location.Start(desiredAccuracyInMeters, updateDistanceInMeters);
            }

            public void Start(float desiredAccuracyInMeters)
            {
                AccuracyInMeters = desiredAccuracyInMeters;
                UpdateDistanceInMeters = k_DefaultUpdateDistanceInMeters;

                UnityEngine.Input.location.Start(desiredAccuracyInMeters);
            }

            public void Start()
            {
                AccuracyInMeters = k_DefaultUpdateDistanceInMeters;
                UpdateDistanceInMeters = k_DefaultUpdateDistanceInMeters;

                UnityEngine.Input.location.Start();
            }

            public void Stop()
            {
                UnityEngine.Input.location.Stop();
            }
        }
    }
}
