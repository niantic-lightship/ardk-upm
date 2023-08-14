// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Playback;
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

        private ICompassProvider m_Provider;
        private LightshipSettings m_LightshipSettings;
        private bool m_PrevWasEnabled;

        private ICompassProvider GetOrCreateProvider()
        {
            if (m_Provider == null)
            {
                var isPlayback = false;
                if (m_LightshipSettings)
                {
                    isPlayback = m_LightshipSettings.EditorPlaybackEnabled || m_LightshipSettings.DevicePlaybackEnabled;
                }

                CreateProvider(isPlayback);
            }

            return m_Provider;
        }

        private void CreateProvider(bool isPlayback)
        {
            if (m_Provider != null)
                DestroyProvider();

            m_Provider = isPlayback
                ? new PlaybackCompassProvider()
                : new UnityCompassProvider();

            if (m_PrevWasEnabled)
                m_Provider.SetHeadingUpdatesEnabled(true);
        }

        internal void DestroyProvider()
        {
            m_Provider = null;
        }

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            if (m_Provider is PlaybackCompassProvider playbackProvider)
            {
                playbackProvider.SetPlaybackDatasetReader(reader);
            }
            else
            {
                m_Provider = new PlaybackCompassProvider();
                ((IPlaybackDatasetUser)m_Provider).SetPlaybackDatasetReader(reader);
            }
        }

        void ILightshipSettingsUser.SetLightshipSettings(LightshipSettings settings)
        {
            m_LightshipSettings = settings;
        }


        private interface ICompassProvider
        {
            float magneticHeading { get; }
            float trueHeading { get; }
            float headingAccuracy { get; }
            Vector3 raw { get; }
            double timestamp { get; }

            bool IsHeadingUpdatesEnabled();
            void SetHeadingUpdatesEnabled(bool value);
        }

        private class PlaybackCompassProvider: ICompassProvider, IPlaybackDatasetUser
        {
            private bool _enabled;
            private PlaybackDatasetReader _datasetReader;

            // TODO [AR-17775]
            // Magnetic Heading was not recorded in playback datasets,
            // so this will return the true heading (for now)
            public float magneticHeading
            {
                get
                {
                    if (_enabled)
                        return  _datasetReader.CurrFrame.LocationInfo.Heading;

                    return 0;
                }
            }

            public float trueHeading
            {
                get
                {
                    if (_enabled && Input.location.status == LocationServiceStatus.Running)
                        return  _datasetReader.CurrFrame.LocationInfo.Heading;

                    return 0;
                }
            }

            public float headingAccuracy
            {
                get
                {
                    if (_enabled)
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
                    if (_enabled)
                        return Vector3.one;

                    return Vector3.zero;
                }
            }

            public double timestamp
            {
                get
                {
                    if (_enabled)
                        return  _datasetReader.CurrFrame.LocationInfo.HeadingTimestamp;

                    return 0;
                }
            }

            public bool IsHeadingUpdatesEnabled() => _enabled && _datasetReader.GetCompassEnabled();

            public void SetHeadingUpdatesEnabled(bool enabled)
            {
                if (_datasetReader.GetCompassEnabled())
                {
                    _enabled = enabled;
                }
                else
                {
                    Debug.LogWarning("Compass data is not available through this dataset.");
                }
            }

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                _datasetReader = reader;
            }
        }

        private class UnityCompassProvider : ICompassProvider
        {
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
