// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Subsystems.Playback;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR
{
    /// <summary>
    ///   <para>Interface into compass functionality.</para>
    /// </summary>
    public class Compass: IPlaybackDatasetUser
    {
        private readonly SensorsCompassProvider _sensorsProvider;
        private readonly SpoofCompassProvider _spoofProvider;

        /// <summary>
        ///   <para>The heading in degrees relative to the magnetic North Pole. (Read Only)</para>
        /// </summary>
        public float magneticHeading => ActiveProvider.magneticHeading;

        /// <summary>
        ///   <para>The heading in degrees relative to the geographic North Pole. (Read Only)</para>
        /// </summary>
        public float trueHeading => ActiveProvider.trueHeading;

        /// <summary>
        ///   <para>Accuracy of heading reading in degrees.</para>
        /// </summary>
        public float headingAccuracy => ActiveProvider.headingAccuracy;

        /// <summary>
        ///   <para>The raw geomagnetic data measured in microteslas. (Read Only)</para>
        /// </summary>
        public Vector3 rawVector => ActiveProvider.rawVector;

        /// <summary>
        ///   <para>Timestamp when the heading was last time updated. (Read Only)</para>
        /// </summary>
        /// <remarks>
        /// Android: The time elapsed is represented in nanoseconds since the device was last turned on.
        /// iOS: The time elapsed is represented in seconds since the Unix epoch January 1, 1970.
        /// </remarks>
        public double timestamp => ActiveProvider.timestamp;

        /// <summary>
        ///   <para>Used to enable or disable compass. Note, that if you want Input.compass.trueHeading property to contain a valid value, you must also enable location updates by calling Input.location.Start().</para>
        /// </summary>
        public bool enabled
        {
            get => ActiveProvider.IsHeadingUpdatesEnabled();
            set => ActiveProvider.SetHeadingUpdatesEnabled(value);
        }

        internal ICompassProvider ActiveProvider
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

        internal Compass()
        {
            _sensorsProvider = new SensorsCompassProvider();
            _spoofProvider = new SpoofCompassProvider();
        }

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            _sensorsProvider.SetPlaybackDatasetReader(reader);
        }

        internal interface ICompassProvider
        {
            float magneticHeading { get; }
            float trueHeading { get; }
            float headingAccuracy { get; }
            Vector3 rawVector { get; }
            double timestamp { get; }

            bool IsHeadingUpdatesEnabled();
            void SetHeadingUpdatesEnabled(bool value);
        }

        internal class SensorsCompassProvider : ICompassProvider, IPlaybackDatasetUser
        {
            private readonly PlaybackCompassProvider _playbackProvider = new();
            private readonly UnityCompassProvider _unityProvider = new();
            private bool _isLightshipLoaderActive;

            public ICompassProvider ActiveSensorsProvider
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
                                "In order for the compass to return playback data, " +
                                "the LightshipLoader must be re-initialized."
                            );

                            return _unityProvider;
                        }

                        return _playbackProvider;
                    }

                    return _unityProvider;
                }
            }

            public float magneticHeading => ActiveSensorsProvider.magneticHeading;
            public float trueHeading => ActiveSensorsProvider.trueHeading;
            public float headingAccuracy => ActiveSensorsProvider.headingAccuracy;
            public Vector3 rawVector => ActiveSensorsProvider.rawVector;
            public double timestamp => ActiveSensorsProvider.timestamp;
            public bool IsHeadingUpdatesEnabled() => ActiveSensorsProvider.IsHeadingUpdatesEnabled();
            public void SetHeadingUpdatesEnabled(bool value) => ActiveSensorsProvider.SetHeadingUpdatesEnabled(value);

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                _playbackProvider.SetPlaybackDatasetReader(reader);
            }
        }

        internal class PlaybackCompassProvider: ICompassProvider, IPlaybackDatasetUser
        {
            private bool _enabled;
            private PlaybackDatasetReader _datasetReader;
            private PlaybackDataset.LocationInfo _lastData;

            // TODO [AR-17775]
            // Magnetic Heading was not recorded in playback datasets,
            // so this will return the true heading (for now)
            public float magneticHeading
            {
                get { return CheckForValidData() ? _lastData.Heading : 0; }
            }

            public float trueHeading
            {
                get { return CheckForValidData() ? _lastData.Heading : 0; }
            }

            public float headingAccuracy
            {
                get { return CheckForValidData() ? _lastData.HeadingAccuracy : 0; }
            }

            // TODO [AR-17775]
            // The raw geomagnetic data was not recorded in playback datasets,
            // so this will invalid (but non-zero) values for now.
            public Vector3 rawVector
            {
                get
                {
                    return CheckForValidData() ? Vector3.one : Vector3.zero;
                }
            }

            public double timestamp
            {
                get { return CheckForValidData() ? _lastData.HeadingTimestamp : 0; }
            }

            public bool HasPlaybackDatasetReader => _datasetReader != null;

            public bool IsHeadingUpdatesEnabled() => _enabled && _datasetReader.GetCompassEnabled();

            public void SetHeadingUpdatesEnabled(bool enabled)
            {
                _enabled = enabled;
                if (_enabled)
                {
                    CheckForNewData();
                }
            }

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                if (_datasetReader != null)
                {
                    _datasetReader.FrameChanged -= CheckForNewData;
                }

                if (reader != null)
                {
                    reader.FrameChanged += CheckForNewData;
                }

                _datasetReader = reader;
            }

            private void CheckForNewData()
            {
                if (_enabled && _datasetReader != null)
                {
                    _lastData = _datasetReader.CurrFrame?.LocationInfo;
                }
            }

            private bool CheckForValidData()
            {
                // On iOS, the compass APIs will return the last seen values (but not continue to update)
                // once the compass is disabled. We replicate that behavior.
                return _lastData != null && _lastData.HeadingTimestamp != 0;
            }
        }

        internal class UnityCompassProvider : ICompassProvider
        {
            public float magneticHeading => UnityEngine.Input.compass.magneticHeading;

            public float trueHeading => UnityEngine.Input.compass.trueHeading;
            public float headingAccuracy => UnityEngine.Input.compass.headingAccuracy;
            public Vector3 rawVector => UnityEngine.Input.compass.rawVector;
            public double timestamp => UnityEngine.Input.compass.timestamp;

            public bool IsHeadingUpdatesEnabled() => UnityEngine.Input.compass.enabled;

            public void SetHeadingUpdatesEnabled(bool value) => UnityEngine.Input.compass.enabled = value;
        }

        internal class SpoofCompassProvider : ICompassProvider
        {
            private bool _wasEnabledEver;
            private bool _enabled;

            private SpoofCompassInfo _spoofInfo = LightshipSettingsHelper.ActiveSettings.SpoofCompassInfo;

            public float magneticHeading
            {
                get { return CheckForValidData() ? _spoofInfo.MagneticHeading : 0; }
            }

            public float trueHeading
            {
                get { return CheckForValidData() ? _spoofInfo.TrueHeading : 0; }
            }

            public float headingAccuracy
            {
                get { return CheckForValidData() ? _spoofInfo.HeadingAccuracy : 0; }
            }

            public Vector3 rawVector
            {
                get { return CheckForValidData() ? _spoofInfo.RawVector : Vector3.zero; }
            }

            public double timestamp
            {
                get { return CheckForValidData() ? _spoofInfo.Timestamp : 0; }
            }

            public bool IsHeadingUpdatesEnabled() => _enabled;

            public void SetHeadingUpdatesEnabled(bool value)
            {
                _enabled = value;

                if (_enabled)
                {
                    _wasEnabledEver = true;
                }
            }

            private bool CheckForValidData()
            {
                // On iOS, the compass APIs will return the last seen values (but not continue to update)
                // once the compass is disabled. We replicate that behavior.
                return _wasEnabledEver;
            }
        }
    }
}
