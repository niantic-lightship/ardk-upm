// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using System.IO;

namespace Niantic.Lightship.SharedAR.Networking
{
    /// <summary>
    /// The current connection state of the device to Lightship rooms.
    /// </summary>
    [PublicAPI]
    public enum NetworkState :
        byte
    {
        JoiningRoom = 0,
        InRoom = 1,
        LeavingRoom = 2,
        NotInRoom = 3
    };

    /// <summary>
    /// Network events that are fired from INetworking.NetworkEvent
    /// </summary>
    [PublicAPI]
    public enum NetworkEvents :
        byte
    {
        Connected = 0,
        Disconnected = 1,
        ConnectionError = 2, // TODO: more detailed errors
        RoomFull = 3,
        ArdkShutdown = 100
    };

    /// <summary>
    /// Error code in network event
    /// </summary>
    [PublicAPI]
    public static class NetworkEventErrorCode
    {
        public const uint ServerInternalError = 4400;
        public const uint RoomNotFoundError = 4401;
        public const uint RoomFullError = 4402;
    }
    
    /// <summary>
    /// Event args fired from INetworking.NetworkEvent.
    /// </summary>
    [PublicAPI]
    public struct NetworkEventArgs
    {
        /// <summary>
        /// Type of networking event being fired.
        /// </summary>
        public NetworkEvents networkEvent { get; private set; }

        /// <summary>
        /// Error code. Only valid in Disconnected and ConnectionError
        /// </summary>
        public UInt32 errorCode { get; private set; }

        public NetworkEventArgs(NetworkEvents netEvent, UInt32 errCode)
        {
            networkEvent = netEvent;
            errorCode = errCode;
        }
    }

    /// <summary>
    /// Event args fired from INetworking.PeerAdded and INetworking.PeerRemoved.
    /// </summary>
    [PublicAPI]
    public struct PeerIDArgs
    {
        /// <summary>
        /// PeerID
        /// </summary>
        [PublicAPI]
        public PeerID PeerID { get; private set; }

        public PeerIDArgs(PeerID peerid)
        {
            PeerID = peerid;
        }
    }

    /// <summary>
    /// Event args fired from INetworking.DataReceived.
    /// </summary>
    [PublicAPI]
    public struct DataReceivedArgs
    {
        /// <summary>
        /// Id of the peer that is sending the data.
        /// </summary>
        [PublicAPI]
        public PeerID PeerID { get; private set; }

        /// <summary>
        /// The tag that catagorizes the sent message.
        /// </summary>
        [PublicAPI]
        public uint Tag { get; private set; }
        private readonly byte[] _data;

        /// <summary>
        /// The length of the message.
        /// </summary>
        [PublicAPI]
        public int DataLength
        {
            get { return _data.Length; }
        }

        public DataReceivedArgs(PeerID peerID, uint tag, byte[] data)
        {
            PeerID = peerID;
            Tag = tag;
            _data = data;
        }

        /// <summary>
        /// Create a MemoryStream to read the message.
        /// <returns>A MemoryStream pointed at the message</returns>
        /// </summary>
        [PublicAPI]
        public MemoryStream CreateDataReader()
        {
            return new MemoryStream(_data, false);
        }

        /// <summary>
        /// Make a full copy of the message.
        /// <returns>A copy of the message</returns>
        /// </summary>
        [PublicAPI]
        public byte[] CopyData()
        {
            var result = new byte[DataLength];
            Buffer.BlockCopy(_data, 0, result, 0, DataLength);
            return result;
        }
    }

    /// <summary>
    /// Low level networking interface used by the LightshipNetcodeTransport to talk to the
    /// Lightship relay servers. This interface allows you to relay a message directly to another
    /// user, halfing the latency of Netcode For Gameobject messages which have to be double-relayed
    /// by the "Host" client.
    /// </summary>
    [PublicAPI]
    public interface INetworking :
        IDisposable
    {
        /// <summary>
        /// Send data to the specified peers. Receiving peers will have a DataReceived event fired.
        /// This function is used by LightshipNetcodeTransport to send all Netcode messages.
        /// <param name="dest">Destination of the message. Passing an empty list will broadcast the
        ///     message to all other peers in the room.</param>
        /// <param name="tag">Data tag that peers will receive</param>
        /// <param name="data">Byte[] to send</param>
        /// </summary>
        [PublicAPI]
        void SendData(List<PeerID> dest, uint tag, byte[] data);

        /// <summary>
        /// Get the latest connection state
        /// </summary>
        [PublicAPI]
        NetworkState NetworkState { get; }

        /// <summary>
        /// This client's PeerID.
        /// </summary>
        [PublicAPI]
        PeerID SelfPeerID { get; }

        /// <summary>
        /// Get all PeerIDs actively connected to the room.
        /// </summary>
        [PublicAPI]
        List<PeerID> PeerIDs { get; }

        /// <summary>
        /// Establish the network connection configured by the network's construction.
        /// Calling "Join" on the Room automatically calls this method, consider using the Room API
        /// instead!
        /// </summary>
        [PublicAPI]
        void Join();

        /// <summary>
        /// Disconnect from the room.
        /// Calling "Leave" on the Room automatically calls this method, consider using the Room API
        /// instead!
        /// </summary>
        [PublicAPI]
        void Leave();

        /// <summary>
        /// Event fired when the client's connection to the network changes state.
        /// </summary>
        [PublicAPI]
        event Action<NetworkEventArgs> NetworkEvent;

        /// <summary>
        /// Event fired when a peer joins the room.
        /// All other clients in the room are considered "Peers" by this API.
        /// </summary>
        [PublicAPI]
        event Action<PeerIDArgs> PeerAdded;

        /// <summary>
        /// Event fired when a peer is removed, either from intentional action, timeout, or error.
        /// All other clients in the room are considered "Peers" by this API.
        /// </summary>
        [PublicAPI]
        event Action<PeerIDArgs> PeerRemoved;

        /// <summary>
        /// Data received from another peer in the room sent through the SendData method.
        /// </summary>
        [PublicAPI]
        event Action<DataReceivedArgs> DataReceived;
    }
}
