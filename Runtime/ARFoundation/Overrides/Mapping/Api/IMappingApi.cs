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
            float splitterMaxDistanceMeters,
            float splitterMaxDurationSeconds
        );

        void StartMapping();

        void StopMapping();

        bool GetDeviceMaps(out XRDeviceMap[] maps);

        bool GetDeviceGraphBlobs(out XRDeviceMapGraph[] blobs);

        void CreateAnchorPayloadFromDeviceMap(XRDeviceMap map, Matrix4x4 pose, out byte[] anchorPayload);
    }
}
