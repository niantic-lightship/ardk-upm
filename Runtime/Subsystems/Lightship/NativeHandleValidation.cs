// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR
{
    public static class NativeHandleValidation
    {
        public static bool IsValidHandle(this IntPtr handle)
        {
            if (handle != IntPtr.Zero)
                return true;

            // With AR-15672 upgrade, won't need to explicitly ifdef here
#if NIANTIC_LIGHTSHIP_DEVELOPMENT
            Log.Warning("Attempted to call native API with an invalid handle.");
#endif

            return false;
        }
    }
}
