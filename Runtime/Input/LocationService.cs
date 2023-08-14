using System;
using System.Reflection;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Playback;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;

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

        private ILocationServiceProvider _provider;
        private LightshipSettings _lightshipSettings;

        private ILocationServiceProvider GetOrCreateProvider()
        {
            if (_lightshipSettings == null)
                throw new InvalidOperationException("Missing LightshipSettings.");

            if (_provider == null)
            {
                var  isPlayback = _lightshipSettings.EditorPlaybackEnabled || _lightshipSettings.DevicePlaybackEnabled;
                CreateOrSwitchProvider(isPlayback);
            }

            return _provider;
        }

        private bool prevWasRunning;

        private void CreateOrSwitchProvider(bool isPlayback)
        {
            if (_provider != null)
            {
                prevWasRunning = _provider.status != LocationServiceStatus.Stopped;
                if (prevWasRunning)
                    _provider.Stop();
            }

            _provider = isPlayback
                ? new PlaybackLocationServiceProvider()
                : new UnityLocationServiceProvider();

            if (prevWasRunning)
                _provider.Start();
        }

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            var playbackProvider = GetOrCreateProvider() as PlaybackLocationServiceProvider;
            Assert.IsNotNull
            (
                playbackProvider,
                "Tried to set the dataset of a non-Playback Location Service. This should not have happened."
            );

            playbackProvider.SetPlaybackDatasetReader(reader);
        }

        void ILightshipSettingsUser.SetLightshipSettings(LightshipSettings settings)
        {
            _lightshipSettings = settings;

            // The provider is not reset when the LightshipLoader deinitializes to cover the situation
            // where AR has been unloaded but location services needs to keep running. So now when new
            // LightshipSettings are injected by the loader, the provider implementations needs to be
            // switched out.
            var isPlayback = _lightshipSettings.EditorPlaybackEnabled || _lightshipSettings.DevicePlaybackEnabled;
            CreateOrSwitchProvider(isPlayback);
        }

        private interface ILocationServiceProvider
        {
            bool isEnabledByUser { get; }
            LocationServiceStatus status { get; }

            LocationInfo lastData { get; }

            void Start(float desiredAccuracyInMeters, float updateDistanceInMeters);
            void Start(float desiredAccuracyInMeters);

            void Start();
            void Stop();
        }

        private class PlaybackLocationServiceProvider : ILocationServiceProvider, IPlaybackDatasetUser
        {
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
                        Debug.Log
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

            public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
            {
                Start(desiredAccuracyInMeters, updateDistanceInMeters, true);
            }

            public void Start(float desiredAccuracyInMeters)
            {
                Start(desiredAccuracyInMeters, 10f, true);
            }

            public void Start()
            {
                Start(10f, 10f, false);
            }

            private void Start(float desiredAccuracyInMeters, float updateDistanceInMeters, bool logWarning)
            {
                // TODO [AR-16134]
                // Improve Input.location by filtering location updates with configured updateDistanceInMeters
                if (logWarning)
                {
                    Debug.LogWarning
                    (
                        "Customizing the location service's accuracy or update distance is not supported " +
                        "while using Lightship Playback."
                    );
                }

                _started = true;
            }

            public void Stop()
            {
                _started = false;
            }

            private UnityEngine.LocationInfo ConvertToUnity(Playback.PlaybackDataset.LocationInfo info)
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

        private class UnityLocationServiceProvider : ILocationServiceProvider
        {
            public bool isEnabledByUser => UnityEngine.Input.location.isEnabledByUser;
            public LocationServiceStatus status => UnityEngine.Input.location.status;
            public LocationInfo lastData => UnityEngine.Input.location.lastData;

            public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
            {
                UnityEngine.Input.location.Start(desiredAccuracyInMeters, updateDistanceInMeters);
            }

            public void Start(float desiredAccuracyInMeters)
            {
                UnityEngine.Input.location.Start(desiredAccuracyInMeters);
            }

            public void Start()
            {
                UnityEngine.Input.location.Start();
            }

            public void Stop()
            {
                UnityEngine.Input.location.Stop();
            }
        }
    }
}
