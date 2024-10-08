// Copyright 2022-2024 Niantic.
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
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

        private DataFormat[] _addedDataFormats = new DataFormat[1];
        private int _addedDataFormatsSize;

        private DataFormat[] _readyDataFormats = new DataFormat[1];
        private int _readyDataFormatsSize;

        private DataFormat[] _removedDataFormats = new DataFormat[1];
        private int _removedDataFormatsSize;

        private bool _isLidarDepthEnabled = false;

        public IntPtr ARDK_SAH_Create(IntPtr unityContext, bool isLidarDepthEnabled)
        {
            _handle = new IntPtr((int)(Random.value * int.MaxValue));
            _isLidarDepthEnabled = isLidarDepthEnabled;
            return _handle;
        }

        public virtual void ARDK_SAH_OnFrame_Deprecated(IntPtr handle, IntPtr frameData)
        {
            if (handle == _handle)
            {
                Log.Info("Forwarded frame data");
            }
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
            NativeArray<DataFormat> dataFormatsReady, out int readySize
        )
        {
            readySize = _readyDataFormatsSize;
            NativeArray<DataFormat>.Copy(_readyDataFormats, dataFormatsReady, _readyDataFormats.Length);

            ClearDataFormats();
        }

        public void ARDK_SAH_GetDataFormatUpdatesForNewFrame
        (
            IntPtr handle,
            NativeArray<DataFormat> dataFormatsAdded, out int addedSize,
            NativeArray<DataFormat> dataFormatsReady, out int readySize,
            NativeArray<DataFormat> dataFormatsRemoved, out int removedSize
        )
        {
            addedSize = _addedDataFormatsSize;
            NativeArray<DataFormat>.Copy(_addedDataFormats, dataFormatsAdded, _addedDataFormats.Length);

            readySize = _readyDataFormatsSize;
            NativeArray<DataFormat>.Copy(_readyDataFormats, dataFormatsReady, _readyDataFormats.Length);

            removedSize = _removedDataFormatsSize;
            NativeArray<DataFormat>.Copy(_removedDataFormats, dataFormatsRemoved, _removedDataFormats.Length);

            ClearDataFormats();
        }

        public void ARDK_SAH_GetDispatchedFormatsToModules
        (
            IntPtr handle,
            out uint dispatchedFrameId,
            out ulong dispatchedToModules,
            out ulong dispatchedDataFormats
        )
        {
            dispatchedFrameId = 0;
            dispatchedToModules = 0;
            dispatchedDataFormats = 0;
        }

        public void MarkDataFormatsReady(int size, params DataFormat[] formats)
        {
            _readyDataFormats = formats;
            _readyDataFormatsSize = size;
        }

        private void ClearDataFormats()
        {
            Array.Clear(_addedDataFormats, 0, _addedDataFormats.Length);
            _addedDataFormatsSize = 0;

            Array.Clear(_readyDataFormats, 0, _readyDataFormats.Length);
            _readyDataFormatsSize = 0;

            Array.Clear(_removedDataFormats, 0, _removedDataFormats.Length);
            _removedDataFormatsSize = 0;
        }

        public void DeregisterDataFormats(params DataFormat[] formats)
        {
            _addedDataFormats = formats;
            _addedDataFormatsSize = formats.Length;
        }

        public void RegisterDataFormats(params DataFormat[] formats)
        {
            _removedDataFormats = formats;
            _removedDataFormatsSize = formats.Length;
        }

        public static ARDKFrameData IntPtrToFrameDataCStruct(IntPtr ptr)
        {
            return (ARDKFrameData)Marshal.PtrToStructure(ptr, typeof(ARDKFrameData));
        }

        public static DeprecatedFrameCStruct IntPtrToDeprecatedFrameCStruct(IntPtr ptr)
        {
            return (DeprecatedFrameCStruct)Marshal.PtrToStructure(ptr, typeof(DeprecatedFrameCStruct));
        }

    }
}
