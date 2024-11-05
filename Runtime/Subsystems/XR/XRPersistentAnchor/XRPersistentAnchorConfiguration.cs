// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Mapping;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Configuration for PersistentAnchorSubsystem.
    /// </summary>
    /// <seealso cref="XRPersistentAnchorConfiguration"/>
    [StructLayout(LayoutKind.Sequential)]
    public class XRPersistentAnchorConfiguration
    {
        // Note: Current default values match AnchorModuleConfiguration defaults
        internal const bool DefaultContinuousLocalizationEnabled = false;
        internal const bool DefaultTemporalFusionEnabled = false;
        internal const bool DefaultTransformUpdateSmoothingEnabled = false;
        internal const bool DefaultCloudLocalizationEnabled = true;
        internal const bool DefaultDeviceMappingLocalizationEnabled = false;
        internal const DeviceMappingType DefaultDeviceMappingType = DeviceMappingType.Orb;
        internal const float DefaultCloudLocalizerInitialRequestsPerSecond = 1.0f;
        internal const float DefaultCloudLocalizerContinuousRequestsPerSecond = 0.2f;
        internal const float DefaultDeviceMappingLocalizationFps = 10.0f;
        internal const uint DefaultCloudLocalizationTemporalFusionWindowSize = 5;
        internal const uint DefaultDeviceMappingLocalizationTemporalFusionWindowSize = 100;
        internal const bool DefaultDiagnosticsEnabled = false;
        internal const bool DefaultLimitedLocalizationsOnly = false;
        internal const int DefaultJpegCompressionQuality = 50;

        private bool _continuousLocalizationEnabled;
        private bool _temporalFusionEnabled;
        private bool _transformUpdateSmoothingEnabled;
        private bool _cloudLocalizationEnabled;
        private bool _deviceMappingLocalizationEnabled;
        private DeviceMappingType _deviceMappingType;
        private float _cloudLocalizerInitialRequestsPerSecond;
        private float _cloudLocalizerContinuousRequestsPerSecond;
        private float _deviceMappingLocalizationFps;
        private uint _cloudLocalizationTemporalFusionWindowSize;
        private uint _deviceMappingLocalizationTemporalFusionWindowSize;
        private bool _diagnosticsEnabled;
        private bool _limitedLocalizationsOnly;
        private int _jpegCompressionQuality;

        public bool ContinuousLocalizationEnabled
        {
            get => _continuousLocalizationEnabled;
            set => _continuousLocalizationEnabled = value;
        }

        public bool TemporalFusionEnabled
        {
            get => _temporalFusionEnabled;
            set => _temporalFusionEnabled = value;
        }

        public bool TransformUpdateSmoothingEnabled
        {
            get => _transformUpdateSmoothingEnabled;
            set => _transformUpdateSmoothingEnabled = value;
        }

        public bool CloudLocalizationEnabled
        {
            get => _cloudLocalizationEnabled;
            set => _cloudLocalizationEnabled = value;
        }

        public bool DeviceMappingLocalizationEnabled
        {
            get => _deviceMappingLocalizationEnabled;
            set => _deviceMappingLocalizationEnabled = value;
        }

        public DeviceMappingType DeviceMappingType
        {
            get => _deviceMappingType;
            set => _deviceMappingType = value;
        }

        // Define the rate of server requests for initial localization
        public float CloudLocalizerInitialRequestsPerSecond
        {
            get => _cloudLocalizerInitialRequestsPerSecond;
            set
            {
                if (value < 0)
                {
                    Log.Error("CloudLocalizerInitialRequestsPerSecond must be greater or equal than zero.");
                    return;
                }
                _cloudLocalizerInitialRequestsPerSecond = value;
            }
        }

        // Define the rate of server requests for continuous localization
        // Requires ContinuousLocalizationEnabled to be true
        public float CloudLocalizerContinuousRequestsPerSecond
        {
            get => _cloudLocalizerContinuousRequestsPerSecond;
            set
            {
                if (value < 0)
                {
                    Log.Error("CloudLocalizerContinuousRequestsPerSecond must be greater or equal than zero.");
                    return;
                }
                _cloudLocalizerContinuousRequestsPerSecond = value;
            }
        }

        public float DeviceMappingLocalizationFps
        {
            get => _deviceMappingLocalizationFps;
            set
            {
                if (value < 0)
                {
                    Log.Error("DeviceMappingLocalizationFps must be greater or equal than zero.");
                    return;
                }
                _deviceMappingLocalizationFps = value;
            }
        }

        public uint CloudLocalizationTemporalFusionWindowSize
        {
            get => _cloudLocalizationTemporalFusionWindowSize;
            set
            {
                if (value <= 0)
                {
                    Log.Error("CloudLocalizationTemporalFusionWindowSize must be greater than zero.");
                    return;
                }
                _cloudLocalizationTemporalFusionWindowSize = value;
            }
        }

        public uint DeviceMappingLocalizationTemporalFusionWindowSize
        {
            get => _deviceMappingLocalizationTemporalFusionWindowSize;
            set
            {
                if (value <= 0)
                {
                    Log.Error("DeviceMappingLocalizationTemporalFusionWindowSize must be greater than zero.");
                    return;
                }
                _deviceMappingLocalizationTemporalFusionWindowSize = value;
            }
        }

        public bool DiagnosticsEnabled
        {
            get => _diagnosticsEnabled;
            set => _diagnosticsEnabled = value;
        }

        public bool LimitedLocalizationsOnly
        {
            get => _limitedLocalizationsOnly;
            set => _limitedLocalizationsOnly = value;
        }

        public int JpegCompressionQuality
        {
            get => _jpegCompressionQuality;
            set
            {
                if (value < 1 || value > 100)
                {
                    Log.Error("JpegCompressionQuality must be between 1 and 100.");
                    return;
                }
                _jpegCompressionQuality = value;
            }
        }

        /// <summary>
        /// Default constructor for the XRPersistentAnchorConfiguration.
        /// </summary>
        public XRPersistentAnchorConfiguration()
        {
            _continuousLocalizationEnabled = DefaultContinuousLocalizationEnabled;
            _temporalFusionEnabled = DefaultTemporalFusionEnabled;
            _transformUpdateSmoothingEnabled = DefaultTransformUpdateSmoothingEnabled;
            _cloudLocalizationEnabled = DefaultCloudLocalizationEnabled;
            _deviceMappingLocalizationEnabled = DefaultDeviceMappingLocalizationEnabled;
            _deviceMappingType = DefaultDeviceMappingType;
            _cloudLocalizerInitialRequestsPerSecond = DefaultCloudLocalizerInitialRequestsPerSecond;
            _cloudLocalizerContinuousRequestsPerSecond = DefaultCloudLocalizerContinuousRequestsPerSecond;
            _deviceMappingLocalizationFps = DefaultDeviceMappingLocalizationFps;
            _cloudLocalizationTemporalFusionWindowSize = DefaultCloudLocalizationTemporalFusionWindowSize;
            _deviceMappingLocalizationTemporalFusionWindowSize = DefaultDeviceMappingLocalizationTemporalFusionWindowSize;
            _diagnosticsEnabled = DefaultDiagnosticsEnabled;
            _limitedLocalizationsOnly = DefaultLimitedLocalizationsOnly;
            _jpegCompressionQuality = DefaultJpegCompressionQuality;
        }

        public XRPersistentAnchorConfiguration
        (
            bool continuousLocalizationEnabled = DefaultContinuousLocalizationEnabled,
            bool temporalFusionEnabled = DefaultTemporalFusionEnabled,
            bool transformUpdateSmoothingEnabled = DefaultTransformUpdateSmoothingEnabled,
            bool cloudLocalizationEnabled = DefaultCloudLocalizationEnabled,
            bool deviceMappingLocalizationEnabled = DefaultDeviceMappingLocalizationEnabled,
            DeviceMappingType deviceMappingType = DefaultDeviceMappingType,
            float cloudLocalizerInitialRequestsPerSecond = DefaultCloudLocalizerInitialRequestsPerSecond,
            float cloudLocalizerContinuousRequestsPerSecond = DefaultCloudLocalizerContinuousRequestsPerSecond,
            float deviceMappingLocalizationFps = DefaultDeviceMappingLocalizationFps,
            uint cloudLocalizationTemporalFusionWindowSize = DefaultCloudLocalizationTemporalFusionWindowSize,
            uint deviceMappingLocalizationTemporalFusionWindowSize = DefaultDeviceMappingLocalizationTemporalFusionWindowSize,
            bool diagnosticsEnabled = DefaultDiagnosticsEnabled,
            bool limitedLocalizationsOnly = DefaultLimitedLocalizationsOnly,
            int jpegCompressionQuality = DefaultJpegCompressionQuality
        )
        {
            _continuousLocalizationEnabled = continuousLocalizationEnabled;
            _temporalFusionEnabled = temporalFusionEnabled;
            _transformUpdateSmoothingEnabled = transformUpdateSmoothingEnabled;
            _cloudLocalizationEnabled = cloudLocalizationEnabled;
            _deviceMappingLocalizationEnabled = deviceMappingLocalizationEnabled;
            _deviceMappingType = deviceMappingType;
            _cloudLocalizerInitialRequestsPerSecond = cloudLocalizerInitialRequestsPerSecond;
            _cloudLocalizerContinuousRequestsPerSecond = cloudLocalizerContinuousRequestsPerSecond;
            _deviceMappingLocalizationFps = deviceMappingLocalizationFps;
            _cloudLocalizationTemporalFusionWindowSize = cloudLocalizationTemporalFusionWindowSize;
            _deviceMappingLocalizationTemporalFusionWindowSize = deviceMappingLocalizationTemporalFusionWindowSize;
            _diagnosticsEnabled = diagnosticsEnabled;
            _limitedLocalizationsOnly = limitedLocalizationsOnly;
            _jpegCompressionQuality = jpegCompressionQuality;
        }

        public XRPersistentAnchorConfiguration(XRPersistentAnchorConfiguration other) : this()
        {
            if (other == null)
            {
                return;
            }

            _continuousLocalizationEnabled = other._continuousLocalizationEnabled;
            _temporalFusionEnabled = other._temporalFusionEnabled;
            _transformUpdateSmoothingEnabled = other._transformUpdateSmoothingEnabled;
            _cloudLocalizationEnabled = other._cloudLocalizationEnabled;
            _deviceMappingLocalizationEnabled = other._deviceMappingLocalizationEnabled;
            _deviceMappingType = other._deviceMappingType;
            _cloudLocalizerInitialRequestsPerSecond = other._cloudLocalizerInitialRequestsPerSecond;
            _cloudLocalizerContinuousRequestsPerSecond = other._cloudLocalizerContinuousRequestsPerSecond;
            _deviceMappingLocalizationFps = other._deviceMappingLocalizationFps;
            _cloudLocalizationTemporalFusionWindowSize = other._cloudLocalizationTemporalFusionWindowSize;
            _deviceMappingLocalizationTemporalFusionWindowSize = other._deviceMappingLocalizationTemporalFusionWindowSize;
            _diagnosticsEnabled = other._diagnosticsEnabled;
            _limitedLocalizationsOnly = other._limitedLocalizationsOnly;
            _jpegCompressionQuality = other._jpegCompressionQuality;
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="XRPersistentAnchorConfiguration"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="XRPersistentAnchor"/>, otherwise false.</returns>
        public bool Equals(XRPersistentAnchorConfiguration other)
        {
            if (other == null)
            {
                return false;
            }

            return  _continuousLocalizationEnabled == other._continuousLocalizationEnabled &&
                    _temporalFusionEnabled == other._temporalFusionEnabled &&
                    _transformUpdateSmoothingEnabled == other._transformUpdateSmoothingEnabled &&
                    _cloudLocalizationEnabled == other._cloudLocalizationEnabled &&
                    _deviceMappingLocalizationEnabled == other._deviceMappingLocalizationEnabled &&
                    _deviceMappingType == other._deviceMappingType &&
                    FloatEqualityHelper.NearlyEquals(_cloudLocalizerInitialRequestsPerSecond, other._cloudLocalizerInitialRequestsPerSecond) &&
                    FloatEqualityHelper.NearlyEquals(_cloudLocalizerContinuousRequestsPerSecond, other._cloudLocalizerContinuousRequestsPerSecond) &&
                    FloatEqualityHelper.NearlyEquals(_deviceMappingLocalizationFps, other._deviceMappingLocalizationFps) &&
                    _cloudLocalizationTemporalFusionWindowSize == other._cloudLocalizationTemporalFusionWindowSize &&
                    _deviceMappingLocalizationTemporalFusionWindowSize == other._deviceMappingLocalizationTemporalFusionWindowSize &&
                    _diagnosticsEnabled == other._diagnosticsEnabled &&
                    _limitedLocalizationsOnly == other._limitedLocalizationsOnly &&
                    _jpegCompressionQuality == other._jpegCompressionQuality;
        }

        public new bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((XRPersistentAnchorConfiguration)obj);
        }

        public new int GetHashCode()
        {
            return ((uint)_cloudLocalizerInitialRequestsPerSecond).GetHashCode() ^
                ((uint)_cloudLocalizerContinuousRequestsPerSecond).GetHashCode() ^
                ((uint)_deviceMappingLocalizationFps).GetHashCode() ^
                _cloudLocalizationTemporalFusionWindowSize.GetHashCode() ^
                _deviceMappingLocalizationTemporalFusionWindowSize.GetHashCode() ^
                _jpegCompressionQuality.GetHashCode();
        }

        // Fusion wants to cache a window of requests to determine the best pose to surface
        // This should be around 5-25 seconds of requests to mitigate drift over time
        public static uint DetermineFusionWindowFromRequestRate(float requestsPerSecond)
        {
            // If requests per second is zero or negative, assume we are requesting every frame.
            // Cache up to 100 frames of requests (last 1.5-3 seconds).
            if (requestsPerSecond <= 0)
            {
                return 100;
            }

            // Request rate = interval between requests
            var rate = 1 / requestsPerSecond;

            // If requests are happening more than once per second, we can afford to have a smaller window
            // Cache 5 seconds of requests, up to 100
            if (rate <= 1.0f)
            {
                return Math.Min((uint)(5 / rate), 100);
            }

            // Between 1-5 requests per second, we want to hold older poses for a bit longer to have more samples
            // Cache between 10-25 seconds of requests depending on rate
            else if (rate <= 5.0f)
            {
                return Math.Max((uint)(10 / rate), 5);
            }

            // At any higher rates, we don't want to hold old poses for too long to avoid drift
            // Just fuse the last 3 results
            else
            {
                return 3;
            }
        }
    }
}
