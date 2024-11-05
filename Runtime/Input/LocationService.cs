// Copyright 2022-2024 Niantic.

using System.Linq;
using System.Reflection;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Subsystems.Playback;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR
{
    /// <summary>
    ///   <para>Interface into location service functionality.</para>
    /// </summary>
    public class LocationService: IPlaybackDatasetUser
    {
        private const float DefaultDesiredAccuracyInMeters = 10f;
        private const float DefaultUpdateDistanceInMeters = 10f;

        private readonly SensorsLocationServiceProvider _sensorsProvider;
        private readonly SpoofLocationServiceProvider _spoofProvider;

        /// <summary>
        ///   <para>Indicates whether the device allows access the application to access the location service.</para>
        /// </summary>
        public bool isEnabledByUser => ActiveProvider.isEnabledByUser;

        /// <summary>
        ///   <para>Returns the location service status.</para>
        /// </summary>
        public LocationServiceStatus status => ActiveProvider.status;

        /// <summary>
        ///   <para>The last geographical location that the device registered.</para>
        /// </summary>
        public LocationInfo lastData => ActiveProvider.lastData;

        internal ILocationServiceProvider ActiveProvider
        {
            get
            {
                if (LightshipSettingsHelper.ActiveSettings.LocationAndCompassDataSource == LocationDataSource.Sensors)
                {
                    return _sensorsProvider;
                }

                return _spoofProvider;
            }
        }

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
        public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
        {
            ActiveProvider.Start(desiredAccuracyInMeters, updateDistanceInMeters);
        }

        /// <summary>
        ///   <para>Starts location service updates.</para>
        /// </summary>
        /// <param name="desiredAccuracyInMeters">
        ///     The service accuracy you want to use, in meters. This determines the accuracy of the device's last location coordinates. Higher values like 500 don't require the device to use its GPS chip and
        ///     thus save battery power. Lower values like 5-10 provide the best accuracy but require the GPS chip and thus use more battery power. The default value is 10 meters.
        /// </param>
        public void Start(float desiredAccuracyInMeters)
        {
            Start(desiredAccuracyInMeters, DefaultUpdateDistanceInMeters);
        }

        /// <summary>
        ///   <para>Starts location service updates.</para>
        /// </summary>
        public void Start()
        {
            Start(DefaultDesiredAccuracyInMeters, DefaultUpdateDistanceInMeters);
        }

        /// <summary>
        ///   <para>Stops location service updates. This is useful to save battery power when the application doesn't require the location service.</para>
        /// </summary>
        public void Stop()
        {
            ActiveProvider.Stop();
        }

        internal LocationService()
        {
            _sensorsProvider = new SensorsLocationServiceProvider();
            _spoofProvider = new SpoofLocationServiceProvider();
        }

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            _sensorsProvider.SetPlaybackDatasetReader(reader);
        }

        internal interface ILocationServiceProvider
        {
            bool isEnabledByUser { get; }
            LocationServiceStatus status { get; }

            LocationInfo lastData { get; }

            void Start(float desiredAccuracyInMeters, float updateDistanceInMeters);

            void Stop();
        }

        internal class SensorsLocationServiceProvider : ILocationServiceProvider, IPlaybackDatasetUser
        {
            private readonly PlaybackLocationServiceProvider _playbackProvider = new();
            private readonly UnityLocationServiceProvider _unityProvider = new();
            private bool _isLightshipLoaderActive;
            internal ILocationServiceProvider ActiveSensorsProvider
            {
                get
                {
                    if (LightshipSettingsHelper.ActiveSettings.UsePlayback)
                    {
                        // HasPlaybackDatasetReader is proxy for knowing if the LightshipLoader is initialized
                        if (!_playbackProvider.HasPlaybackDatasetReader)
                        {
                            Log.Debug
                            (
                                "In order for location services to return playback data, " +
                                "the LightshipLoader must be re-initialized."
                            );

                            return _unityProvider;
                        }

                        return _playbackProvider;
                    }

                    return _unityProvider;
                }
            }

            public bool isEnabledByUser => ActiveSensorsProvider.isEnabledByUser;
            public LocationServiceStatus status => ActiveSensorsProvider.status;
            public LocationInfo lastData => ActiveSensorsProvider.lastData;

            public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
            {
                _playbackProvider.Start(desiredAccuracyInMeters, updateDistanceInMeters);
                _unityProvider.Start(desiredAccuracyInMeters, updateDistanceInMeters);
            }

            public void Stop()
            {
                _playbackProvider.Stop();
                _unityProvider.Stop();
            }

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                _playbackProvider.SetPlaybackDatasetReader(reader);
            }
        }

        internal class PlaybackLocationServiceProvider : ILocationServiceProvider, IPlaybackDatasetUser
        {
            private float _desiredAccuracyInMeters;
            private float _desiredUpdateDistanceInMeters;

            private bool _started;
            private PlaybackDatasetReader _datasetReader;
            private LocationInfo _lastData = default;

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

            public LocationInfo lastData
            {
                get
                {
                    if (status != LocationServiceStatus.Running)
                    {
                        // This is the same log as when getting UnityEngine.Input.Location.lastData
                        // when the service is not running, including when the service is initializing
                        Log.Info
                        (
                            "Location service updates are not enabled. " +
                            "Check LocationService.status before querying last location."
                        );
                    }

                    // On iOS, the location APIs will return the last seen values (but not continue to update)
                    // once the compass is disabled. We replicate that behavior.
                    return _lastData;
                }
            }

            public bool HasPlaybackDatasetReader => _datasetReader != null;

            public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
            {
                // TODO [AR-16134]
                // Improve Input.location by filtering location updates with configured updateDistanceInMeters
                // if (Math.Abs(desiredAccuracyInMeters - k_DefaultAccuracyInMeters) > 0.001 ||
                //     Math.Abs(updateDistanceInMeters - k_DefaultUpdateDistanceInMeters) > 0.001)
                // {
                //     Log.Warning
                //     (
                //         "Customizing the location service's accuracy or update distance is not supported " +
                //         "while using Lightship Playback."
                //     );
                // }

                _started = true;
                _desiredAccuracyInMeters = desiredAccuracyInMeters;
                _desiredUpdateDistanceInMeters = updateDistanceInMeters;

                CheckForNewData();
            }

            public void Stop()
            {
                _started = false;
            }

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                if (reader != null)
                {
                    reader.FrameChanged += CheckForNewData;
                }
                else if (_datasetReader != null)
                {
                    _datasetReader.FrameChanged -= CheckForNewData;
                }

                _datasetReader = reader;
            }

            private void CheckForNewData()
            {
                if (_started && _datasetReader != null)
                {
                    _lastData = ConvertToUnity(_datasetReader.CurrFrame?.LocationInfo);
                }
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
        }

        internal class UnityLocationServiceProvider : ILocationServiceProvider
        {
            public bool isEnabledByUser => UnityEngine.Input.location.isEnabledByUser;
            public LocationServiceStatus status => UnityEngine.Input.location.status;
            public LocationInfo lastData => UnityEngine.Input.location.lastData;

            public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
            {
                UnityEngine.Input.location.Start(desiredAccuracyInMeters, updateDistanceInMeters);
            }

            public void Stop()
            {
                UnityEngine.Input.location.Stop();
            }
        }

        internal class SpoofLocationServiceProvider : ILocationServiceProvider
        {
            private bool _wasRunningEver;

            private LocationServiceStatus _status;
            private SpoofLocationInfo _spoofInfo;

            private float _desiredAccuracyInMeters;

            public bool isEnabledByUser => true;

            public LocationServiceStatus status => _status;

            public LocationInfo lastData
            {
                get
                {
                    if (status != LocationServiceStatus.Running)
                    {
                        // This is the same log as when getting UnityEngine.Input.Location.lastData
                        // when the service is not running, including when the service is initializing
                        Log.Info
                        (
                            "Location service updates are not enabled. " +
                            "Check LocationService.status before querying last location."
                        );
                    }

                    // When the service is stopped after running, UnityEngine.Input.Location.lastData
                    // will continue to return the last seen data. We do the same here.
                    return _wasRunningEver ? ConvertToUnity(_spoofInfo) : default;
                }
            }

            public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
            {
                if (status == LocationServiceStatus.Stopped)
                {
                    _wasRunningEver = true;
                    _spoofInfo = LightshipSettingsHelper.ActiveSettings.SpoofLocationInfo;
                    _status = LocationServiceStatus.Running;
                }
            }

            public void Stop()
            {
                if (status != LocationServiceStatus.Failed)
                {
                    _status = LocationServiceStatus.Stopped;
                }
            }

            private UnityEngine.LocationInfo ConvertToUnity(SpoofLocationInfo spoofInfo)
            {
                // Box here before passing into reflection method
                object unityInfo = new UnityEngine.LocationInfo();

                SetFieldViaReflection(unityInfo, "m_Timestamp", spoofInfo.Timestamp);
                SetFieldViaReflection(unityInfo, "m_Latitude", spoofInfo.Latitude);
                SetFieldViaReflection(unityInfo, "m_Longitude", spoofInfo.Longitude);
                SetFieldViaReflection(unityInfo, "m_Altitude", spoofInfo.Altitude);
                SetFieldViaReflection(unityInfo, "m_HorizontalAccuracy", spoofInfo.HorizontalAccuracy);
                SetFieldViaReflection(unityInfo, "m_VerticalAccuracy", spoofInfo.VerticalAccuracy);

                return (LocationInfo)unityInfo;
            }
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
    }
}
