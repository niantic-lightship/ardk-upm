// Copyright 2022-2024 Niantic.

using System;

namespace Niantic.Lightship.AR.Mapping
{
    internal interface IMappingApi :
        IDisposable
    {
        IntPtr Create(IntPtr moduleManager);

        void Start();

        void Stop();

        void Configure();

        void StartMapping();

        void StopMapping();

        bool GetDeviceMaps(out XRDeviceMap[] maps);

        bool GetDeviceGraphBlobs(out XRDeviceMapGraph[] blobs);
    }
}
