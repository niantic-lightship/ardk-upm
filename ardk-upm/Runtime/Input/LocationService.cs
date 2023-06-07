using System.Reflection;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Playback;
using UnityEngine;

namespace Niantic.Lightship.AR
{
    public class LocationService : _IPlaybackDatasetUser, _ILightshipSettingsUser
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

        private ILocationServiceProvider m_Provider;
        private LightshipSettings m_LightshipSettings;

        private ILocationServiceProvider GetOrCreateProvider()
        {
            if (m_Provider == null)
            {
                var isPlayback = false;
                if (m_LightshipSettings)
                {
                    isPlayback = (m_LightshipSettings.UsePlaybackOnEditor && Application.isEditor) ||
                        (m_LightshipSettings.UsePlaybackOnDevice && !Application.isEditor);
                }
                CreateProvider(isPlayback);
            }


            return m_Provider;
        }

        private bool prevWasRunning;

        private void CreateProvider(bool isPlayback)
        {
            if (m_Provider != null)
                DestroyProvider();

            m_Provider = isPlayback
                ? new PlaybackLocationServiceProvider()
                : new UnityLocationServiceProvider();

            if (prevWasRunning)
                m_Provider.Start();
        }

        internal void DestroyProvider()
        {
            if (m_Provider == null)
                return;

            prevWasRunning = m_Provider.status != LocationServiceStatus.Stopped;
            if (prevWasRunning)
                m_Provider.Stop();

            m_Provider = null;
        }

        void _IPlaybackDatasetUser.SetPlaybackDatasetReader(_PlaybackDatasetReader reader)
        {
            if (m_Provider is PlaybackLocationServiceProvider playbackProvider)
            {
                playbackProvider.SetPlaybackDatasetReader(reader);
            }
            else
            {
                m_Provider = new PlaybackLocationServiceProvider();
                ((_IPlaybackDatasetUser)m_Provider).SetPlaybackDatasetReader(reader);
            }
        }

        void _ILightshipSettingsUser.SetLightshipSettings(LightshipSettings settings)
        {
            m_LightshipSettings = settings;
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

        private class PlaybackLocationServiceProvider : ILocationServiceProvider, _IPlaybackDatasetUser
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

            private _PlaybackDatasetReader _datasetReader;

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

            private UnityEngine.LocationInfo ConvertToUnity(Playback._PlaybackDataset.LocationInfo info)
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

            public void SetPlaybackDatasetReader(_PlaybackDatasetReader reader)
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
