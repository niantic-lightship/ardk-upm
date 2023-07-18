using System;
using Unity.Collections;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    internal interface _IApi
    {
        public IntPtr Lightship_ARDK_Unity_PAM_Create(IntPtr unityContext);
        public void Lightship_ARDK_Unity_PAM_OnFrame(IntPtr handle, IntPtr frameData);

        public void Lightship_ARDK_Unity_PAM_Release(IntPtr handle);

        public void Lightship_ARDK_Unity_PAM_GetDataFormatsReadyForNewFrame(IntPtr handle,
            NativeArray<_DataFormat> readyFormats, out int formatsSize);
    }
}
