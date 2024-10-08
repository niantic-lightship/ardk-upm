// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.Mapping
{
    /// <summary>
    /// Class for rimitive Device mapping operations and configs
    /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
    /// </summary>
    [Experimental]
    public class DeviceMappingController
    {
        /// <summary>
        /// Config to enable/disable creation of tracking edges during mapping
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public bool TrackingEdgesEnabled { get; set; }  = true;

        /// <summary>
        /// Config to enable/disable use of learned features during mapping
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public bool LearnedFeaturesEnabled { get; set; }  = false;

        /// <summary>
        /// Target Framerate to run Mappers. The 0 value indicates maximun framerate
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public uint TargetFrameRate { get; set; } = 0;

        /// <summary>
        /// Node Splitter config for Max Distantance Travelled before creating a new map node
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public float SplitterMaxDistanceMeters { get; set; } = 5.0f;

        /// <summary>
        /// Node Splitter config for Max Duration Exceeded before creating a new map node
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public float SplitterMaxDurationSeconds { get; set; } = 10.0f;

        /// <summary>
        /// Status if running mapping or not
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public bool IsMapping
        {
            get => _isMapping;
        }

        private IMappingApi _mapper;

        private bool _isRunning;

        private bool _isMapping;

        internal void Init()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }
            _mapper = new NativeMappingApi();
            _mapper.Create(LightshipUnityContext.UnityContextHandle);
            _isRunning = false;
            _isMapping = false;
        }

        internal void Destroy()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }
            if (_isRunning)
            {
                // stop before dispose if still running
                _mapper.Stop();
                _isRunning = false;
            }
            _isMapping = false;
            _mapper.Dispose();
        }

        /// <summary>
        /// Start native module until implementing subsystem
        /// </summary>
        internal void StartNativeModule()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }
            _mapper.Configure(TrackingEdgesEnabled, LearnedFeaturesEnabled, TargetFrameRate, SplitterMaxDistanceMeters, SplitterMaxDurationSeconds);
            _mapper.Start();
            _isRunning = true;
            _isMapping = false;
        }

        /// <summary>
        /// Stop native module until implementing subsystem
        /// </summary>
        internal void StopNativeModule()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }
            _mapper.Stop();
            _isRunning = false;
            _isMapping = false;
        }

        /// <summary>
        /// Update configuration into the lower layer
        /// </summary>
        internal void UpdateConfiguration()
        {
            _mapper.Configure(TrackingEdgesEnabled, LearnedFeaturesEnabled, TargetFrameRate, SplitterMaxDistanceMeters, SplitterMaxDurationSeconds);
        }

        /// <summary>
        ///  Start map generation
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StartMapping()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }
            _mapper.StartMapping();
            _isMapping = true;
        }

        /// <summary>
        /// Stop map generation
        /// @note This is an experimental feature, and is subject to breaking changes or deprecation without notice
        /// </summary>
        [Experimental]
        public void StopMapping()
        {
            if (!DeviceMapFeatureFlag.IsFeatureEnabled())
            {
                return;
            }
            _mapper.StopMapping();
            _isMapping = false;
        }

        internal void UseFakeMappingApi(IMappingApi mappingApi)
        {
            _mapper = mappingApi;
        }
    }
}
