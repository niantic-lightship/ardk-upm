// Copyright 2022-2025 Niantic.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// Class that deserializes global pose data from the server.
    /// Public properties are conditional as fields can be missing, depending on the data.
    /// </summary>
    [Serializable]
    internal class GlobalPoseData
    {
        private Quaternion ConvertServerRotationToUnity(float[] rotation)
        {
            // TODO: ascertain the right conversion for Edn to Unity
            return rotation?.Length == 4
                ? new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3])
                : Quaternion.identity;
        }

        private const double MissingValue = double.MaxValue;

        // Disable naming rules as fields are named for deserialisation purposes
        // ReSharper disable InconsistentNaming
        [Serializable]
        private class GpsData
        {
            public double altitude_accuracy_m = MissingValue;
            public double altitude_m = MissingValue;
            public double latitude_deg = MissingValue;
            public double longitude_deg = MissingValue;
            public double position_accuracy_m = MissingValue;
        }

        [SerializeField] private GpsData gps;
        [SerializeField] private double heading_accuracy_deg = MissingValue;
        [SerializeField] private double heading_deg = MissingValue;
        [SerializeField] private float[] rotation_edn;
        // Resharper restore InconsistentNaming

        // ReSharper disable CompareOfFloatsByEqualityOperator
        public double? AltitudeAccuracyMeters => gps.altitude_accuracy_m != MissingValue ? gps.altitude_accuracy_m : null;
        public double? AltitudeMeters => gps.altitude_m != MissingValue ? gps.altitude_m : null;
        public double? LatitudeDegrees => gps.latitude_deg != MissingValue ? gps.latitude_deg : null;
        public double? LongitudeDegrees => gps.longitude_deg != MissingValue ? gps.longitude_deg : null;
        public double? PositionAccuracyMeters => gps.position_accuracy_m != MissingValue ? gps.position_accuracy_m : null;
        public double? HeadingAccuracyDegrees => heading_accuracy_deg != MissingValue ? heading_accuracy_deg : null;
        public double? HeadingDegrees => heading_deg != MissingValue ? heading_deg : null;
        // Resharper restore CompareOfFloatsByEqualityOperator

        // Rotation East-Down-North (reference frame for rotations)
        public Quaternion RotationEdn => ConvertServerRotationToUnity(rotation_edn);
    }
}
