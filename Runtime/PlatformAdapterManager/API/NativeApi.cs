// Copyright 2022-2024 Niantic.
using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Niantic.Lightship.AR.PAM
{
    internal class NativeApi : IApi
    {
        public IntPtr Lightship_ARDK_Unity_PAM_Create(IntPtr unityContext, bool isLidarDepthEnabled)
        {
            return Native.Lightship_ARDK_Unity_PAM_Create(unityContext, isLidarDepthEnabled);
        }

        public void Lightship_ARDK_Unity_PAM_OnFrame(IntPtr handle, IntPtr frameData)
        {
            Native.Lightship_ARDK_Unity_PAM_OnFrame(handle, frameData);
        }

        public void Lightship_ARDK_Unity_PAM_GetDataFormatUpdatesForNewFrame(IntPtr handle, NativeArray<DataFormat> dataFormatsAdded,
            out int addedSize, NativeArray<DataFormat> dataFormatsReady, out int readySize, NativeArray<DataFormat> dataFormatsRemoved,
            out int removedSize)
        {
            unsafe
            {
                Native.Lightship_ARDK_Unity_PAM_GetDataFormatUpdatesForNewFrame(handle,
                    (IntPtr)dataFormatsAdded.GetUnsafePtr(), out UInt32 addedDataFormatsSize,
                    (IntPtr)dataFormatsReady.GetUnsafePtr(), out UInt32 readyDataFormatsSize,
                    (IntPtr)dataFormatsRemoved.GetUnsafePtr(), out UInt32 removedDataFormatsSize);

                addedSize = (int)addedDataFormatsSize;
                readySize = (int)readyDataFormatsSize;
                removedSize = (int)removedDataFormatsSize;
            }
        }

        public void Lightship_ARDK_Unity_PAM_Release(IntPtr handle)
        {
            Native.Lightship_ARDK_Unity_PAM_Release(handle);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_PAM_Create(IntPtr unityContext, bool isLidarDepthEnabled);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_PAM_OnFrame(IntPtr handle, IntPtr frameData);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_PAM_GetDataFormatUpdatesForNewFrame(IntPtr handle,
                IntPtr addedDataFormats, out UInt32 addedDataFormatsSize,
                IntPtr readyDataFormats, out UInt32 readyDataFormatsSize,
                IntPtr removedDataFormats, out UInt32 removedDataFormatsSize);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_PAM_Release(IntPtr handle);
        }
    }
}
