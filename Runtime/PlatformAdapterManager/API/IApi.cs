// Copyright 2022-2024 Niantic.
using System;
using Unity.Collections;

namespace Niantic.Lightship.AR.PAM
{
    internal interface IApi
    {
        public IntPtr ARDK_SAH_Create(IntPtr unityContext, bool isLidarDepthEnabled);
        public void ARDK_SAH_OnFrame_Deprecated(IntPtr handle, IntPtr frameData);
        public void ARDK_SAH_OnFrame(IntPtr handle, IntPtr frameData);

        public void ARDK_SAH_Release(IntPtr handle);

        public void ARDK_SAH_GetDataFormatsReadyForNewFrame
        (
            IntPtr handle,
            NativeArray<DataFormat> dataFormatsReady,
            out int readySize
        );

        public void ARDK_SAH_GetDataFormatUpdatesForNewFrame
        (
            IntPtr handle,
            NativeArray<DataFormat> dataFormatsAdded,
            out int addedSize,
            NativeArray<DataFormat> dataFormatsReady,
            out int readySize,
            NativeArray<DataFormat> dataFormatsRemoved,
            out int removedSize
        );

        public void ARDK_SAH_GetDispatchedFormatsToModules
        (
            IntPtr handle,
            out uint dispatchedFrameId,
            out ulong dispatchedToModules,
            out ulong dispatchedDataFormats
        );
    }
}
