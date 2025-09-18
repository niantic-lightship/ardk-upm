// Copyright 2022-2025 Niantic.

using System;
using System.Runtime.InteropServices;

namespace Niantic.Lightship.AR.API
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ArdkMatrix4F
    {
        // In C#, we'll represent this as a pointer that can be marshaled
        public IntPtr Values;
    }
}
