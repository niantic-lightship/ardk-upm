using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Niantic.Lightship.AR.Scanning
{
    internal class NativeSqcApi : ISqcApi
    {
        public IntPtr SQCCreate(IntPtr unityContext)
        {
            if (unityContext == IntPtr.Zero)
            {
                Debug.LogError("handle is nullptr, lightship is disabled");
                return IntPtr.Zero;
            }

            return Native.Lightship_ARDK_Unity_Scanning_SQC_Create(unityContext);
        }

        public void SQCRelease(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Debug.LogError("handle is nullptr, lightship is disabled");
                return;
            }

            Native.Lightship_ARDK_Unity_Scanning_SQC_Release(handle);
        }

        public bool SQCRun(IntPtr handle, float framerate, string scanPath)
        {
            if (handle == IntPtr.Zero)
            {
                Debug.LogError("handle is nullptr, lightship is disabled");
                return false;
            }

            return Native.Lightship_ARDK_Unity_Scanning_SQC_Run(handle, framerate, scanPath);
        }

        public void SQCCancelCurrentRun(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Debug.LogError("handle is nullptr, lightship is disabled");
                return;
            }

            Native.Lightship_ARDK_Unity_Scanning_SQC_Interrupt(handle);
        }

        public bool SQCIsRunning(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Debug.LogError("handle is nullptr, lightship is disabled");
                return false;
            }

            return Native.Lightship_ARDK_Unity_Scanning_SQC_Is_Running(handle);
        }

        public float SQCGetProgress(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Debug.LogError("handle is nullptr, lightship is disabled");
                return 0.0f;
            }

            return Native.Lightship_ARDK_Unity_Scanning_SQC_Get_Progress(handle);
        }

        public void SQCGetResult(IntPtr handle,
            string scanPath, IntPtr scores, out int scoresSize)
        {
            if (handle == IntPtr.Zero)
            {
                Debug.LogError("handle is nullptr, lightship is disabled");
                scoresSize = 0;
                return;
            }

            Native.Lightship_ARDK_Unity_Scanning_SQC_Get_Result(handle,
                scanPath, scores, out UInt32 size);
            scoresSize = (int)(size);
        }

        private static class Native
        {
            [DllImport(_LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_Scanning_SQC_Create(IntPtr unityContext);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Scanning_SQC_Release(IntPtr handle);

            [DllImport(_LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_Scanning_SQC_Run(IntPtr handle, float framerate,
                string scanPath);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Scanning_SQC_Interrupt(IntPtr handle);

            [DllImport(_LightshipPlugin.Name)]
            public static extern bool Lightship_ARDK_Unity_Scanning_SQC_Is_Running(IntPtr handle);

            [DllImport(_LightshipPlugin.Name)]
            public static extern float Lightship_ARDK_Unity_Scanning_SQC_Get_Progress(IntPtr handle);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_Scanning_SQC_Get_Result(
                IntPtr handle, string scanPath, IntPtr out_scores, out UInt32 out_size);
        }
    }
}
