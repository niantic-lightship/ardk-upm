// Copyright 2022-2024 Niantic.

using System;
using System.Reflection;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// [Experimental] A static class that allows you to spoof the location of the device.
    /// </summary>
    public class LightshipLocationSpoof
    {
        private bool _needsUpdate = true;

        private static LightshipLocationSpoof s_instance;

        public static LightshipLocationSpoof Instance
        {
            get
            {
                // MagicLeap does not have GPS so we require spoofing to be enabled.
#if !NIANTIC_ARDK_EXPERIMENTAL_FEATURES && !NIANTIC_LIGHTSHIP_ML2_ENABLED
                Log.Error("LightshipLocationSpoof is an experimental feature and is not enabled. Please enable NIANTIC_ARDK_EXPERIMENTAL_FEATURES in your project settings.");
                return null;
#endif

                s_instance ??= new LightshipLocationSpoof();

                return s_instance;
            }
        }

        public LocationInfo LocationInfo
        {
            get
            {
                if (_needsUpdate)
                {
                    _locationInfo = SetLocationInfo();
                    _needsUpdate = false;
                }

                return _locationInfo;
            }
        }

        private LocationInfo _locationInfo;

        private float _latitude = 0.0f;

        public float Latitude
        {
            set
            {
                _latitude = value;
                _needsUpdate = true;
            }
        }

        private float _longitude = 0.0f;

        public float Longitude
        {
            set
            {
                _longitude = value;
                _needsUpdate = true;
            }
        }

        private float _altitude = 0.0f;

        public float Altitude
        {
            set
            {
                _altitude = value;
                _needsUpdate = true;
            }
        }

        private LocationInfo SetLocationInfo()
        {
            // Box here before passing into reflection method
            object unityInfo = new LocationInfo();

            SetFieldViaReflection(unityInfo, "m_Latitude", _latitude);
            SetFieldViaReflection(unityInfo, "m_Longitude", _longitude);
            SetFieldViaReflection(unityInfo, "m_Altitude", _altitude);

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
    }
}
