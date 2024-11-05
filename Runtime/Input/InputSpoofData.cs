// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR
{
    [Serializable]
    public class SpoofLocationInfo
    {
        [SerializeField]
        private float _latitude;

        [SerializeField]
        private float _longitude;

        [SerializeField]
        private double _timestamp;

        [SerializeField]
        private float _altitude;

        [SerializeField]
        private float _horizontalAccuracy;

        [SerializeField]
        private float _verticalAccuracy;

        public float Latitude
        {
            get => _latitude;
            set => _latitude = value;
        }

        public float Longitude
        {
            get => _longitude;
            set => _longitude = value;
        }

        public double Timestamp
        {
            get => _timestamp;
            set => _timestamp = value;
        }

        public float Altitude
        {
            get => _altitude;
            set => _altitude = value;
        }

        public float HorizontalAccuracy
        {
            get => _horizontalAccuracy;
            set => _horizontalAccuracy = value;
        }

        public float VerticalAccuracy
        {
            get => _verticalAccuracy;
            set => _verticalAccuracy = value;
        }

        public SpoofLocationInfo() { }

        public SpoofLocationInfo(SpoofLocationInfo source)
        {
            _latitude = source.Latitude;
            _longitude = source.Longitude;
            _timestamp = source.Timestamp;
            _altitude = source.Altitude;
            _horizontalAccuracy = source.HorizontalAccuracy;
            _verticalAccuracy = source.VerticalAccuracy;
        }

        public static SpoofLocationInfo Default
        {
            get
            {
                return new SpoofLocationInfo
                {
                    Latitude = 37.795322f,
                    Longitude = -122.39243f,
                    Timestamp = 123456,
                    Altitude = 16,
                    HorizontalAccuracy = 10,
                    VerticalAccuracy = 10
                };
            }
        }
    }

    [Serializable]
    public class SpoofCompassInfo
    {
        [SerializeField]
        private float _magneticHeading;

        [SerializeField]
        private float _trueHeading;

        [SerializeField]
        private float _headingAccuracy;

        [SerializeField]
        private Vector3 _rawVector;

        [SerializeField]
        private double _timestamp;

        public float MagneticHeading
        {
            get => _magneticHeading;
            set => _magneticHeading = value;
        }

        public float TrueHeading
        {
            get => _trueHeading;
            set => _trueHeading = value;
        }

        public float HeadingAccuracy
        {
            get => _headingAccuracy;
            set => _headingAccuracy = value;
        }

        public Vector3 RawVector
        {
            get => _rawVector;
            set => _rawVector = value;
        }

        public double Timestamp
        {
            get => _timestamp;
            set => _timestamp = value;
        }

        public SpoofCompassInfo() { }

        public SpoofCompassInfo(SpoofCompassInfo source)
        {
            _magneticHeading = source.MagneticHeading;
            _trueHeading = source.TrueHeading;
            _headingAccuracy = source.HeadingAccuracy;
            _rawVector = source.RawVector;
            _timestamp = source.Timestamp;
        }

        public static SpoofCompassInfo Default
        {
            get
            {
                return
                    new SpoofCompassInfo
                    {
                        MagneticHeading = 90,
                        TrueHeading = 1.430f,
                        HeadingAccuracy = 1.0f,
                        RawVector = new Vector3(0.1f, 0.2f, 0.3f),
                        Timestamp = 123456
                    };
            }
        }
    }
}
