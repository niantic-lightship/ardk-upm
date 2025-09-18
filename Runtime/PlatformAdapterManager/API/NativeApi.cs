// Copyright 2022-2025 Niantic.
using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;

namespace Niantic.Lightship.AR.PAM
{
    internal class NativeApi : IApi
    {
        public IntPtr ARDK_SAH_Create(IntPtr unityContext, bool isLidarDepthEnabled)
        {
            if (!LightshipUnityContext.CheckUnityContext(unityContext))
            {
                return IntPtr.Zero;
            }
            return Native.Lightship_ARDK_Unity_SAH_Create(unityContext, isLidarDepthEnabled);
        }

        public void ARDK_SAH_OnFrame(IntPtr handle, IntPtr frameData)
        {
            Native.Lightship_ARDK_Unity_SAH_OnFrame(handle, frameData);
        }

        public void ARDK_SAH_GetDataFormatsReadyForNewFrame(
            IntPtr handle,
            out uint dataFormatsReady
        )
        {
            unsafe
            {
                Native.Lightship_ARDK_Unity_SAH_GetDataFormatUpdatesForNewFrame(handle,
                    out dataFormatsReady);
            }
        }

        public void ARDK_SAH_GetDispatchedFormatsToModules
        (
            IntPtr handle,
            out uint dispatchedFrameId,
            out ulong dispatchedToModules,
            out uint dispatchedDataFormats
        )
        {
            unsafe
            {
                Native.Lightship_ARDK_Unity_SAH_GetDispatchedFormatsToModules
                (
                    handle,
                    out uint outDispatchedFrameId,
                    out ulong outDispatchedToModules,
                    out uint outDispatchedDataFormats
                );

                dispatchedFrameId = outDispatchedFrameId;
                dispatchedToModules = outDispatchedToModules;
                dispatchedDataFormats = outDispatchedDataFormats;
            }
        }

        public void ARDK_SAH_Release(IntPtr handle)
        {
            Native.Lightship_ARDK_Unity_SAH_Release(handle);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern IntPtr Lightship_ARDK_Unity_SAH_Create(IntPtr unityContext, bool isLidarDepthEnabled);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_SAH_OnFrame(IntPtr handle, IntPtr frameData);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_SAH_GetDataFormatUpdatesForNewFrame
            (
                IntPtr handle,
                out UInt32 readyDataFormats
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_SAH_GetDispatchedFormatsToModules(IntPtr handle,
                out uint dispatchedFrameId,
                out ulong dispatchedToModules,
                out uint dispatchedDataFormats);

            [DllImport(LightshipPlugin.Name)]
            public static extern void Lightship_ARDK_Unity_SAH_Release(IntPtr handle);
        }
    }
}
