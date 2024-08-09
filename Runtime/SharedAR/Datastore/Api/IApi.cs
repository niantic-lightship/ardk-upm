// Copyright 2022-2024 Niantic.

using System;

namespace Niantic.Lightship.SharedAR.Datastore.Api
{
    // Adapter to the native Datastore API
    internal interface IApi
    {
        delegate void DatastoreCallback
        (
            IntPtr managedHandle,
            Byte reqType,
            Byte result,
            UInt32  reqId,
            string key,
            IntPtr data,
            UInt64 dataSize,
            UInt32 version
        );

        void SetData(
            IntPtr nativeHandle,
            UInt32 reqId,
            string key,
            byte[] data,
            UInt64 dataSize
        );

        void GetData(
            IntPtr nativeHandle,
            UInt32 reqId,
            string key
        );

        /// Delete key value data
        void DeleteData(
            IntPtr nativeHandle,
            UInt32 reqId,
            string key
        );

        void SetDatastoreCallback(
            IntPtr managedHandle,
            IntPtr nativeHandle,
            DatastoreCallback cb
        );
    }

}
