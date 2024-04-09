// Copyright 2022-2024 Niantic.

using UnityEngine.SubsystemsImplementation;

namespace Niantic.Lightship.AR.Subsystems.XR
{
    public interface ISubsystemWithModelMetadata
    {
        public bool IsMetadataAvailable { get; }
        
        public uint? LatestFrameId { get; }
    }
}
