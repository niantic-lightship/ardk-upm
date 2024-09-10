// Copyright 2022-2024 Niantic.
using System;
using Unity.Collections;

namespace Niantic.Lightship.AR.PAM
{
    internal interface IApi
    {
        public IntPtr Lightship_ARDK_Unity_PAM_Create(IntPtr unityContext, bool isLidarDepthEnabled);
        public void Lightship_ARDK_Unity_PAM_OnFrame_Deprecated(IntPtr handle, IntPtr frameData);
        public void Lightship_ARDK_Unity_PAM_OnFrame(IntPtr handle, IntPtr frameData);

        public void Lightship_ARDK_Unity_PAM_Release(IntPtr handle);

        public void Lightship_ARDK_Unity_PAM_GetDataFormatsReadyForNewFrame
        (
            IntPtr handle,
            NativeArray<DataFormat> dataFormatsReady,
            out int readySize
        );

        public void Lightship_ARDK_Unity_PAM_GetDataFormatUpdatesForNewFrame
        (
            IntPtr handle,
            NativeArray<DataFormat> dataFormatsAdded,
            out int addedSize,
            NativeArray<DataFormat> dataFormatsReady,
            out int readySize,
            NativeArray<DataFormat> dataFormatsRemoved,
            out int removedSize
        );

        public void Lightship_ARDK_Core_SAH_GetDispatchedFormatsToModules
        (
            IntPtr handle,
            out uint dispatchedFrameId,
            out ulong dispatchedToModules,
            out ulong dispatchedDataFormats
        );
    }
}
