// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT; // MonoPInvokeCallback attribute
using UnityEngine;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.SharedAR.Datastore.Api;
using Niantic.Lightship.AR.Utilities;
using IApi = Niantic.Lightship.SharedAR.Datastore.Api.IApi;
using NativeApi = Niantic.Lightship.SharedAR.Datastore.Api.NativeApi;

namespace Niantic.Lightship.SharedAR.Datastore
{
    public class LightshipDatastore : IDatastore
    {
        private IntPtr _nativeHandle;
        private IApi _api;

        #region Handles

        private IntPtr _cachedHandleIntPtr = IntPtr.Zero;
        private GCHandle _cachedHandle;
        private IntPtr _managedHandle
        {
            get
            {
                if (_cachedHandleIntPtr != IntPtr.Zero)
                    return _cachedHandleIntPtr;

                lock (this)
                {
                    if (_cachedHandleIntPtr != IntPtr.Zero)
                        return _cachedHandleIntPtr;

                    // https://msdn.microsoft.com/en-us/library/system.runtime.interopservices.gchandle.tointptr.aspx
                    _cachedHandle = GCHandle.Alloc(this, GCHandleType.Normal);
                    _cachedHandleIntPtr = GCHandle.ToIntPtr(_cachedHandle);
                }

                return _cachedHandleIntPtr;
            }
        }
        #endregion

        private Queue<DatastoreCallbackArgs> _eventQueue;

        public event Action<DatastoreCallbackArgs> DatastoreCallback;

        public LightshipDatastore(IntPtr nativeHandle) : this(nativeHandle, new NativeApi())
        {
        }

        // constructor with IApi. Expected to be used from unit tests
        internal LightshipDatastore(IntPtr nativeHandle, IApi api)
        {
            _eventQueue = new Queue<DatastoreCallbackArgs>();
            _nativeHandle = nativeHandle;
            _api = api;
            MonoBehaviourEventDispatcher.Updating.AddListener(Update);
            if (IsNativeHandleValid())
            {
                _api.SetDatastoreCallback(_managedHandle, _nativeHandle, _handleDatastoreCallback);
            }
        }

        public void Dispose()
        {
            MonoBehaviourEventDispatcher.Updating.RemoveListener(Update);
            lock (_eventQueue)
            {
                _eventQueue.Clear();
            }
            _nativeHandle = IntPtr.Zero;
        }

        private bool IsNativeHandleValid()
        {
            if (_nativeHandle == IntPtr.Zero || _api == null)
            {
                Log.Warning("Invalid native handle or native API");
                return false;
            }
            return true;
        }

        public void SetData(UInt32 requestId, string key, byte[] value)
        {
            if (!IsNativeHandleValid())
            {
                return;
            }
            _api.SetData(_nativeHandle, requestId, key, value, (ulong)value.Length);
        }

        public void GetData(UInt32 requestId, string key)
        {
            if (!IsNativeHandleValid())
            {
                return;
            }
            _api.GetData(_nativeHandle, requestId, key);
        }

        public void DeleteData(UInt32 requestId, string key)
        {
            if (!IsNativeHandleValid())
            {
                return;
            }
            _api.DeleteData(_nativeHandle, requestId, key);
        }

        // Called each frame. Invoke events if queued.
        private void Update()
        {
            lock (_eventQueue)
            {
                foreach (var datastoreEvent in _eventQueue)
                {
                    DatastoreCallback?.Invoke(datastoreEvent);
                }
                _eventQueue.Clear();
            }
        }

        // Queue event from native (run in native thread)
        private void HandleDatastoreCallback(
            Byte reqType,
            Byte result,
            UInt32  reqId,
            string key,
            byte[] data,
            UInt32 version
        )
        {
            DatastoreCallbackArgs arg = new DatastoreCallbackArgs(
                (DatastoreOperationType) reqType,
                (Result) result,
                reqId,
                key,
                data,
                version
                );
            lock (_eventQueue)
            {
                _eventQueue.Enqueue(arg);
            }

        }

        // A static C-API callback (called in native thread)
        [MonoPInvokeCallback(typeof(IApi.DatastoreCallback))]
        private static void _handleDatastoreCallback(
            IntPtr managedHandle,
            Byte reqType,
            Byte result,
            UInt32  reqId,
            string key,
            IntPtr dataPtr,
            UInt64 dataSize,
            UInt32 version
        )
        {
            var instance = GCHandle.FromIntPtr(managedHandle).Target as LightshipDatastore;

            if (instance == null )
            {
                return;
            }
            // make a copy of byte array from native buffer
            var data = new byte[dataSize];
            Marshal.Copy(dataPtr, data, 0, (int)dataSize);
            instance.HandleDatastoreCallback(
                reqType,
                result,
                reqId,
                key,
                data,
                version
            );
        }
    }
}
