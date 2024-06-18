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
            _mapper.Configure(_splitterMaxDistanceMeters, _splitterMaxDurationSeconds);
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

        /// <summary>
        /// Get the map data generated
        /// </summary>
        /// <param name="maps">an array of map data</param>
        /// <returns>True if any maps generated. False if no map has been generated so far</returns>
        public bool GetDeviceMaps(out XRDeviceMap[] maps)
        {
            if (!IsFeatureEnabled())
            {
                maps = default;
                return false;
            }
            return _mapper.GetDeviceMaps(out maps);
        }

        /// <summary>
        /// Get graph data of map nodes
        /// </summary>
        /// <param name="blobs">an array of graphs</param>
        /// <returns>True if any graph generated. False if no graph has been generated so far</returns>
        public bool GetDeviceGraphs(out XRDeviceMapGraph[] blobs)
        {
            if (!IsFeatureEnabled())
            {
                blobs = default;
                return false;
            }
            return _mapper.GetDeviceGraphBlobs(out blobs);
        }

        /// <summary>
        /// Generates Anchor (as payload) from Device Map
        /// </summary>
        /// <param name="map">A map node,  device map</param>
        /// <param name="pose">A local pose of the anchor to create</param>
        /// <param name="anchorPayload"> anchor payload as byte array</param>
        /// <returns>True byte array representing the anchor that can be wrapped by namespace Niantic.Lightship.AR.PersistentAnchors</returns>
        public bool CreateAnchorFromDeviceMap(XRDeviceMap map, Matrix4x4 pose, out byte[] anchorPayload)
        {
            if (!IsFeatureEnabled())
            {
                anchorPayload = default;
                return false;
            }
            _mapper.CreateAnchorPayloadFromDeviceMap(map, pose, out anchorPayload);
            return true;
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
