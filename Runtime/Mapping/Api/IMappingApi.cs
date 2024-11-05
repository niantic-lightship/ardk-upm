// Copyright 2022-2024 Niantic.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR.Mapping
{
    internal interface IMappingApi :
        IDisposable
    {
        IntPtr Create(IntPtr moduleManager);

        void Start();

        void Stop();

        void Configure
        (
            bool trackingEdgesEnabled,
            bool slickLearnedFeaturesEnabled,
            bool useCpuLeanedFeatures,
            UInt32 slickMapperFps,
            float splitterMaxDistanceMeters,
            float splitterMaxDurationSeconds
        );

        void StartMapping();

        void StopMapping();
    }
}
