// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Core;
using UnityEngine;

namespace Niantic.Lightship.SharedAR.Datastore.Api
{
    // Implementation on the native Datastore API
    internal class NativeApi: IApi
    {

        public void SetData(
            IntPtr nativeHandle,
            UInt32 reqId,
            string key,
            byte[] data,
            UInt64 dataSize
        )
        {
            Native.SetData(nativeHandle, reqId, key, data, dataSize);
        }

        public void GetData(
            IntPtr nativeHandle,
            UInt32 reqId,
            string key
        )
        {
            Native.GetData(nativeHandle, reqId, key);
        }

        public void DeleteData(
            IntPtr nativeHandle,
            UInt32 reqId,
            string key
        )
        {
            Native.DeleteData(nativeHandle, reqId, key);
        }

        public void SetDatastoreCallback(
            IntPtr managedHandle,
            IntPtr nativeHandle,
            IApi.DatastoreCallback cb
        )
        {
            Native.SetDatastoreCallback(managedHandle, nativeHandle, cb);
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Datastore_SetData")]
            public static extern void SetData(  IntPtr nativeHandle,
                UInt32 reqId,
                string key,
                byte[] data,
                UInt64 dataSize);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Datastore_GetData")]
            public static extern void GetData(IntPtr nativeHandle,
                UInt32 reqId,
                string key);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Datastore_DeleteData")]
            public static extern void DeleteData(IntPtr nativeHandle,
                UInt32 reqId,
                string key);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Datastore_SetDatastoreCallback")]
            public static extern void SetDatastoreCallback(IntPtr managedHandle,
                IntPtr nativeHandle,
                IApi.DatastoreCallback cb);
        }
    }
}
