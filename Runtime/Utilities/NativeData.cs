// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Runtime.InteropServices;

namespace Niantic.Lightship.AR.Utilities
{
    /// <summary>
    /// This definition must precisely match the native layer's definition
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct NativeStringStruct
    {
        public IntPtr CharArrayIntPtr;
        public UInt32 ArrayLength;
    }
}
