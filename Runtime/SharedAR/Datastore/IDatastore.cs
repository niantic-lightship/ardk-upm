// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.SharedAR.Datastore
{
    /// <summary>
    /// Result of the Datastore operation
    /// </summary>
    [PublicAPI]
    public enum Result : Byte
    {
        Success = 0,
        Error,
    };

    /// <summary>
    ///  Enum representing type of Datastore operation
    /// </summary>
    [PublicAPI]
    public enum DatastoreOperationType : Byte
    {
        Set = 0,
        Get,
        Delete,
        ServerChangeUpdated,
        ServerChangeDeleted
    };

    /// <summary>
    /// Data passed in the Datastore callback
    /// </summary>
    [PublicAPI]
    public struct DatastoreCallbackArgs {
        public DatastoreOperationType OperationType { get; set; }
        public Result Result  { get; set; }
        public UInt32 RequestId  { get; set; }
        public string Key  { get; set; }
        public byte[] Value  { get; set; }
        public UInt32 Version  { get; set; }

        public DatastoreCallbackArgs(
            DatastoreOperationType operationType,
            Result result,
            UInt32 requestId,
            string key,
            byte[] value,
            UInt32 version
        )
        {
            OperationType = operationType;
            Result = result;
            RequestId = requestId;
            Key = key;
            Value = value;
            Version = version;
        }
    };

    /// <summary>
    /// Server-backed data storage that is associated with sessions or rooms.
    /// Peers can set, update, and delete Key/Value pairs, and have the server notify
    /// all other peers in the session when updates occur.
    /// </summary>
    [PublicAPI]
    public interface IDatastore : IDisposable
    {
        /// <summary>
        /// Set/Add data into the server storage asynchronously
        /// </summary>
        /// <param name="requestId">ID to distinguish to identify th originated request in callback</param>
        /// <param name="key">Key of the data</param>
        /// <param name="value">Value to set</param>
        [PublicAPI]
        void SetData(UInt32 requestId, string key, byte[] value);

        /// <summary>
        /// Get data from the server storage asynchronously
        /// </summary>
        /// <param name="requestId">ID to distinguish to identify th originated request in callback</param>
        /// <param name="key">Key of the data</param>
        [PublicAPI]
        void GetData(UInt32 requestId, string key);

        /// <summary>
        /// Delete the key-value pair from the server storage asynchronously
        /// </summary>
        /// <param name="requestId">ID to distinguish to identify th originated request in callback</param>
        /// <param name="key">Key of the data to delete</param>
        [PublicAPI]
        void DeleteData(UInt32 requestId, string key);

        /// <summary>
        /// Callback to listen to server response or changes
        /// This is called either when receiving a response from the own request, or data changed
        /// on server side
        /// </summary>
        [PublicAPI]
        event Action<DatastoreCallbackArgs> DatastoreCallback;

    }
}
