using Niantic.Lightship.AR.Playback;
using System;
using System.Collections.Generic;
using UnityEngine.XR;
using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    internal interface _ILightshipLoader
    {
        internal _PlaybackDatasetReader PlaybackDatasetReader { get; }

        internal bool InitializeWithSettings(LightshipSettings settings);

        internal static bool IsLidarSupported()
        {
            var subsystems = new List<XRMeshSubsystem>();
            SubsystemManager.GetInstances(subsystems);
            return subsystems.Count > 0;
        }
    }
}
