using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    internal class _NativeApi : _IApi
    {
        public IntPtr Lightship_ARDK_Unity_PAM_Create(IntPtr unityContext)
        {
            return Native.Lightship_ARDK_Unity_PAM_Create(unityContext);
        }

        public void Lightship_ARDK_Unity_PAM_OnFrame(IntPtr handle, IntPtr frameData)
        {
            Native.Lightship_ARDK_Unity_PAM_OnFrame(handle, frameData);
        }

        public void Lightship_ARDK_Unity_PAM_GetDataFormatsReadyForNewFrame(IntPtr handle,
            NativeArray<_DataFormat> readyFormats, out int formatsSize)
        {
            unsafe
            {
                Native.Lightship_ARDK_Unity_PAM_GetDataFormatsReadyForNewFrame(handle,
                    (IntPtr)readyFormats.GetUnsafePtr(), out UInt32 size);
                formatsSize = (int)size;
            }
        }

        public void Lightship_ARDK_Unity_PAM_Release(IntPtr handle)
        {
            Native.Lightship_ARDK_Unity_PAM_Release(handle);
        }

        private static class Native
        {
            [DllImport(_LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_PAM_Create(IntPtr unityContext);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_PAM_OnFrame(IntPtr handle, IntPtr frameData);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_PAM_GetDataFormatsReadyForNewFrame(IntPtr handle,
                IntPtr formats, out UInt32 formatsSize);

            [DllImport(_LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_PAM_Release(IntPtr handle);
        }
    }
}
