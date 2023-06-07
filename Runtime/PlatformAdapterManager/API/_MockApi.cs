using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    internal class _MockApi : _IApi
    {
        public IntPtr Handle
        {
            get => _handle;
        }

        private IntPtr _handle;

        private _DataFormat[] _readyDataFormats = new _DataFormat[1];
        private int _readyFormatsSize;

        public IntPtr Lightship_ARDK_Unity_PAM_Create(IntPtr unityContext)
        {
            _handle = new IntPtr((int)(Random.value * int.MaxValue));

            return _handle;
        }

        public virtual void Lightship_ARDK_Unity_PAM_OnFrame(IntPtr handle, IntPtr frameData)
        {
            if (handle == _handle)
            {
                Debug.Log("Forwarded frame data");
                ClearReadyDataFormats();
            }
        }

        public void Lightship_ARDK_Unity_PAM_Release(IntPtr handle)
        {
            if (handle == _handle)
            {
                _handle = IntPtr.Zero;
            }
        }

        public void Lightship_ARDK_Unity_PAM_GetDataFormatsReadyForNewFrame(IntPtr handle,
            NativeArray<_DataFormat> readyFormats, out int formatsSize)
        {
            formatsSize = _readyFormatsSize;
            NativeArray<_DataFormat>.Copy(_readyDataFormats, readyFormats, _readyDataFormats.Length);
        }

        public void MarkDataFormatsReady(int size, params _DataFormat[] formats)
        {
            _readyDataFormats = formats;
            _readyFormatsSize = size;
        }

        public void ClearReadyDataFormats()
        {
            Array.Clear(_readyDataFormats, 0, _readyDataFormats.Length);
            _readyFormatsSize = 0;
        }

        public static _FrameCStruct IntPtrToFrameCStruct(IntPtr ptr)
        {
            return (_FrameCStruct)Marshal.PtrToStructure(ptr, typeof(_FrameCStruct));
        }
    }
}
