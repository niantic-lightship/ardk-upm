// Copyright 2023-2024 Niantic.

using System;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems.WorldPositioning
{
    public class MockApi : IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            throw new NotImplementedException();
        }

        public void Start(IntPtr providerHandle)
        {
            throw new NotImplementedException();
        }

        public void Stop(IntPtr providerHandle)
        {
            throw new NotImplementedException();
        }

        public void Configure(IntPtr providerHandle)
        {
            throw new NotImplementedException();
        }

        public void Destruct(IntPtr providerHandle)
        {
            throw new NotImplementedException();
        }

        public WorldPositioningStatus TryGetXRToWorld
        (
            IntPtr providerHandle,
            out Matrix4x4 arToWorld,
            out double originLatitude,
            out double originLongitude,
            out double originAltitude
        )
        {
            throw new NotImplementedException();
        }
    }
}
