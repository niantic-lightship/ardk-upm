// Copyright 2022-2025 Niantic.

using System;
using System.Runtime.InteropServices;

namespace Niantic.Lightship.AR.API
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ArdkBuffer
    {
        public IntPtr data; // const uint8_t*
        public UInt32 data_size;
    }
}
