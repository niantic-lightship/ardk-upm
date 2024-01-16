// Copyright 2022-2024 Niantic.
using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;

namespace Niantic.Lightship.AR.Utilities.Metrics
{
    internal static class MetricsUtility
    {
        public static void Init()
        {
            Native.Lightship_ARDK_Unity_SystemInfo_Init();
        }

        public static bool GetCpuLoad(out float cpuLoad)
        {
            return Native.Lightship_ARDK_Unity_SystemInfo_GetCpuLoad(out cpuLoad);
        }

        public static bool GetDeviceMemoryUsage(out ulong used, out ulong free)
        {
            return Native.Lightship_ARDK_Unity_SystemInfo_GetDeviceMemoryUsage(out used, out free);
        }

        public static bool GetProcessMemoryUsage(out ulong mem)
        {
            return Native.Lightship_ARDK_Unity_SystemInfo_GetProcessMemoryUsage(out mem);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_SystemInfo_Init();

            [DllImport(LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_SystemInfo_GetCpuLoad(out float cpuLoad);

            [DllImport(LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_SystemInfo_GetDeviceMemoryUsage(out UInt64 used, out UInt64 free);

            [DllImport(LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_SystemInfo_GetProcessMemoryUsage(out UInt64 mem);
        }
    }
}
