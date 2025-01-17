// Copyright 2022-2024 Niantic.
using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;

namespace Niantic.Lightship.AR.PAM
{
    internal class NativeApi : IApi
    {
        public IntPtr ARDK_SAH_Create(IntPtr unityContext, bool isLidarDepthEnabled)
        {
            IntPtr coreContext = LightshipUnityContext.GetCoreContext(unityContext);
            return Native.ARDK_SAH_Create(coreContext, isLidarDepthEnabled);
        }

        public void ARDK_SAH_OnFrame_Deprecated(IntPtr handle, IntPtr frameData)
        {
            Native.ARDK_SAH_OnFrame_Deprecated(handle, frameData);
        }

        public void ARDK_SAH_OnFrame(IntPtr handle, IntPtr frameData)
        {
            Native.ARDK_SAH_OnFrame(handle, frameData);
        }

        public void ARDK_SAH_GetDataFormatsReadyForNewFrame(
            IntPtr handle,
            out uint dataFormatsReady
        )
        {
            unsafe
            {
                Native.ARDK_SAH_GetDataFormatsReadyForNewFrame(handle,
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
                Native.ARDK_SAH_GetDispatchedFormatsToModules
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
            Native.ARDK_SAH_Release(handle);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name)]
            public static extern IntPtr ARDK_SAH_Create(IntPtr coreContext, bool isLidarDepthEnabled);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_OnFrame_Deprecated(IntPtr handle, IntPtr frameData);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_OnFrame(IntPtr handle, IntPtr frameData);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_GetDataFormatsReadyForNewFrame
            (
                IntPtr handle,
                out UInt32 readyDataFormats
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_GetDispatchedFormatsToModules(IntPtr handle,
                out uint dispatchedFrameId,
                out ulong dispatchedToModules,
                out uint dispatchedDataFormats);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_Release(IntPtr handle);
        }

        [DllImport(LightshipPlugin.Name)]
        public static extern IntPtr ARDK_CoreContext_GetComponentManagerHandle(IntPtr coreContext);
    }
}
