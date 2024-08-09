// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.SharedAR.Networking;
using Niantic.Lightship.SharedAR.Datastore;

namespace Niantic.Lightship.SharedAR.Rooms
{
    /// <summary>
    /// A room is an entity to connect multiple peers through server relayed network. The IRoom provides access to
    /// properties and network connectivity of the room.
    /// </summary>
    [PublicAPI]
    public interface IRoom :
        IDisposable
    {
        /// <summary>
        /// Room properties
        /// </summary>
        [PublicAPI]
        public RoomParams RoomParams { get; }

        /// <summary>
        /// Initialize networking connectivity to connect to the room on the server. INetworking should be available after Initialize() is called.
        /// </summary>
        [PublicAPI]
        public void Initialize();

        /// <summary>
        /// Join to the room. After joined to the room, data can be sent and/or received via INetworking.
        /// </summary>
        [PublicAPI]
        public void Join();

        /// <summary>
        /// Get INetworking object to send/receive data, as well as listening to networking events
        /// </summary>
        [PublicAPI]
        public INetworking Networking { get; }

        /// <summary>
        /// Get IDatastore object to access realtime key-value store attached to the room
        /// </summary>
        [PublicAPI]
        public IDatastore Datastore { get; }

        /// <summary>
        /// Leave from the room. Disconnect from the server and no longer can send/receive data afterwards.
        /// </summary>
        [PublicAPI]
        public void Leave();
    }
} // namespace Niantic.ARDK.SharedAR
