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

        /// <summary>
        /// Workaround to start native module until implementing subsystem
        /// </summary>
        public void StartNativeModule()
        {
            if (!IsFeatureEnabled())
            {
                return;
            }
            _mapper.Start();
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

        private void Awake()
        {
            _mapper = new NativeMappingApi();
            _mapper.Create(LightshipUnityContext.UnityContextHandle);
            _mapper.Configure();
        }

        internal void UseFakeMappingApi(IMappingApi mappingApi)
        {
            _mapper = mappingApi;
        }

        private void OnDestroy()
        {
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
