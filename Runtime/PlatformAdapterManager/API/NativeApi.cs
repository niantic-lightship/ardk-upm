// Copyright 2022-2024 Niantic.
using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Niantic.Lightship.AR.PAM
{
    internal class NativeApi : IApi
    {
        public IntPtr ARDK_SAH_Create(IntPtr unityContext, bool isLidarDepthEnabled)
        {
            IntPtr coreContext = LightshipUnityContext.GetCoreContext(unityContext);
            IntPtr componentManager = ARDK_CoreContext_GetComponentManagerHandle(coreContext);

            return Native.ARDK_SAH_Create(componentManager, isLidarDepthEnabled);
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
            NativeArray<DataFormat> dataFormatsReady,
            out int readySize
        )
        {
            unsafe
            {
                Native.ARDK_SAH_GetDataFormatsReadyForNewFrame(handle,
                    (IntPtr)dataFormatsReady.GetUnsafePtr(), out readySize);
            }
        }

        public void ARDK_SAH_GetDataFormatUpdatesForNewFrame(IntPtr handle, NativeArray<DataFormat> dataFormatsAdded,
            out int addedSize, NativeArray<DataFormat> dataFormatsReady, out int readySize, NativeArray<DataFormat> dataFormatsRemoved,
            out int removedSize)
        {
            unsafe
            {
                Native.ARDK_SAH_GetDataFormatUpdatesForNewFrame(handle,
                    (IntPtr)dataFormatsAdded.GetUnsafePtr(), out UInt32 addedDataFormatsSize,
                    (IntPtr)dataFormatsReady.GetUnsafePtr(), out UInt32 readyDataFormatsSize,
                    (IntPtr)dataFormatsRemoved.GetUnsafePtr(), out UInt32 removedDataFormatsSize);

                addedSize = (int)addedDataFormatsSize;
                readySize = (int)readyDataFormatsSize;
                removedSize = (int)removedDataFormatsSize;
            }
        }

        public void ARDK_SAH_GetDispatchedFormatsToModules
        (
            IntPtr handle,
            out uint dispatchedFrameId,
            out ulong dispatchedToModules,
            out ulong dispatchedDataFormats
        )
        {
            unsafe
            {
                Native.ARDK_SAH_GetDispatchedFormatsToModules
                (
                    handle,
                    out uint outDispatchedFrameId,
                    out ulong outDispatchedToModules,
                    out ulong outDispatchedDataFormats
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
            public static extern IntPtr ARDK_SAH_Create(IntPtr componentManager, bool isLidarDepthEnabled);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_OnFrame_Deprecated(IntPtr handle, IntPtr frameData);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_OnFrame(IntPtr handle, IntPtr frameData);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_GetDataFormatsReadyForNewFrame
            (
                IntPtr handle,
                IntPtr readyDataFormats,
                out Int32 readyDataFormatsSize
            );

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_GetDataFormatUpdatesForNewFrame(IntPtr handle,
                IntPtr addedDataFormats, out UInt32 addedDataFormatsSize,
                IntPtr readyDataFormats, out UInt32 readyDataFormatsSize,
                IntPtr removedDataFormats, out UInt32 removedDataFormatsSize);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_GetDispatchedFormatsToModules(IntPtr handle,
                out uint dispatchedFrameId,
                out ulong dispatchedToModules,
                out ulong dispatchedDataFormats);

            [DllImport(LightshipPlugin.Name)]
            public static extern void ARDK_SAH_Release(IntPtr handle);
        }

        [DllImport(LightshipPlugin.Name)]
        public static extern IntPtr ARDK_CoreContext_GetComponentManagerHandle(IntPtr coreContext);
    }
}
