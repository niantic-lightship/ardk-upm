// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Subsystems.Semantics;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.Mapping
{
    public class ARMappingManager :
        MonoBehaviour
    {
        internal const string SlickMappingFeatureFlagName = "SlickMapping";

        private IMappingApi _mapper;

        private bool _isRunning;

        /// <summary>
        /// Config to enable/disable creation of tracking edges during mapping
        /// </summary>
        public bool TrackingEdgesEnabled
        {
            get => _trackingEdgesEnabled;
            set => _trackingEdgesEnabled = value;
        }

        /// <summary>
        /// Target Framerate to run Mappers. The 0 value indicates maximun framerate
        /// </summary>
        public uint TargetFrameRate
        {
            get => _targetFrameRate;
            set => _targetFrameRate = value;
        }

        /// <summary>
        /// Node Splitter config for Max Distantance Travelled before creating a new map node
        /// </summary>
        public float SplitterMaxDistanceMeters
        {
            get => _splitterMaxDistanceMeters;
            set => _splitterMaxDistanceMeters = value;
        }

        /// <summary>
        /// Node Splitter config for Max Duration Exceeded before creating a new map node
        /// </summary>
        public float SplitterMaxDurationSeconds
        {
            get => _splitterMaxDurationSeconds;
            set => _splitterMaxDurationSeconds = value;
        }

        [SerializeField]
        private bool _trackingEdgesEnabled = true;

        [SerializeField]
        private uint _targetFrameRate = 0;

        [SerializeField]
        private float _splitterMaxDistanceMeters = 5.0f;

        [SerializeField]
        private float _splitterMaxDurationSeconds = 10.0f;

        /// <summary>
        /// Workaround to start native module until implementing subsystem
        /// </summary>
        public void StartNativeModule()
        {
            if (!IsFeatureEnabled())
            {
                return;
            }
            _mapper.Configure(_trackingEdgesEnabled, _targetFrameRate, _splitterMaxDistanceMeters, _splitterMaxDurationSeconds);
            _mapper.Start();
            _isRunning = true;
        }

        /// <summary>
        /// Workaround to stop native module until implementing subsystem
        /// </summary>
        public void StopNativeModule()
        {
            if (!IsFeatureEnabled())
            {
                return;
            }
            _mapper.Stop();
            _isRunning = false;
        }

        /// <summary>
        ///  Start map generation
        /// </summary>
        public void StartMapping()
        {
            if (!IsFeatureEnabled())
            {
                return;
            }
            _mapper.StartMapping();
        }

        /// <summary>
        /// Stop map generation
        /// </summary>
        public void StopMapping()
        {
            if (!IsFeatureEnabled())
            {
                return;
            }
            _mapper.StopMapping();
        }

        private void Awake()
        {
            _mapper = new NativeMappingApi();
            _mapper.Create(LightshipUnityContext.UnityContextHandle);
            _isRunning = false;
        }

        internal void UseFakeMappingApi(IMappingApi mappingApi)
        {
            _mapper = mappingApi;
        }

        private void OnDestroy()
        {
            if (_isRunning)
            {
                // stop before dispose if still running
                _mapper.Stop();
                _isRunning = false;
            }
            _mapper.Dispose();
        }

        private bool IsFeatureEnabled()
        {
            if (!LightshipUnityContext.FeatureEnabled(SlickMappingFeatureFlagName))
            {
                Log.Debug($"{SlickMappingFeatureFlagName} is disabled. Enable in the feature flag file");
                return false;
            }

            return true;
        }
    }
}
