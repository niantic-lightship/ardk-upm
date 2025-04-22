// Copyright 2022-2025 Niantic.
using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Utilities.Logging;
using Random = UnityEngine.Random;

namespace Niantic.Lightship.AR.PAM
{
    internal class MockApi : IApi
    {
        public IntPtr Handle
        {
            get => _handle;
        }

        private IntPtr _handle;

        private DataFormatFlags _readyDataFormats = DataFormatFlags.kNone;

        private bool _isLidarDepthEnabled = false;

        public IntPtr ARDK_SAH_Create(IntPtr unityContext, bool isLidarDepthEnabled)
        {
            _handle = new IntPtr((int)(Random.value * int.MaxValue));
            _isLidarDepthEnabled = isLidarDepthEnabled;
            return _handle;
        }

        public virtual void ARDK_SAH_OnFrame(IntPtr handle, IntPtr frameData)
        {
            if (handle == _handle)
            {
                Log.Info("Forwarded frame data");
            }
        }

        public void ARDK_SAH_Release(IntPtr handle)
        {
            if (handle == _handle)
            {
                _handle = IntPtr.Zero;
            }
        }

        public void ARDK_SAH_GetDataFormatsReadyForNewFrame
        (
            IntPtr handle,
            out uint dataFormatsReady
        )
        {
            dataFormatsReady = (uint)_readyDataFormats;
            ClearDataFormats();
        }

        public void ARDK_SAH_GetDispatchedFormatsToModules
        (
            IntPtr handle,
            out uint dispatchedFrameId,
            out ulong dispatchedToModules,
            out uint dispatchedDataFormats
        )
        {
            dispatchedFrameId = 0;
            dispatchedToModules = 0;
            dispatchedDataFormats = 0;
        }

        public void MarkDataFormatsReady(int size, DataFormatFlags formats)
        {
            _readyDataFormats = formats;
        }

        private void ClearDataFormats()
        {
            _readyDataFormats = DataFormatFlags.kNone;
        }

        public static ARDKFrameData IntPtrToFrameDataCStruct(IntPtr ptr)
        {
            return (ARDKFrameData)Marshal.PtrToStructure(ptr, typeof(ARDKFrameData));
        }
    }
}
