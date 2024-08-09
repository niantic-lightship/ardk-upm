// Copyright 2022-2024 Niantic.

#pragma warning disable 0067

using System;
using System.Collections.Generic;

namespace Niantic.Lightship.SharedAR.Networking.API
{
    internal interface INetworkingApi
    {
        delegate void NetworkEventCallback
        (
            IntPtr managedHandle,
            byte connectionEvent,
            UInt32 errorCode
        );

        delegate void PeerAddedOrRemovedCallback
        (
            IntPtr managedHandle,
            UInt32 peerId
        );

        delegate void DataReceivedCallback
        (
            IntPtr managedHandle,
            UInt32 fromPeer,
            UInt32 tag,
            IntPtr rawData,
            UInt64 rawDataSize
        );

        IntPtr Init(string serverAddr, string roomId, string endpointPrefix = "");
        void Join(IntPtr nativeHandle);
        void Leave(IntPtr nativeHandle);
        void Release(IntPtr nativeHandle);

        void SendData(
            IntPtr nativeHandle,
            UInt32 tag,
            byte[] data,
            UInt64 dataSize,
            UInt32[] peerIdentifiers
        );

        byte GetNetworkingState(IntPtr nativeHandle);
        UInt32 GetSelfPeerId(IntPtr nativeHandle);
        UInt64 GetPeerIds(IntPtr nativeHandle, UInt32[] outPeerIds, UInt64 maxPeers);

        void SetNetworkEventCallback(
            IntPtr managedHandle,
            IntPtr nativeHandle,
            NetworkEventCallback cb);

        void SetPeerAddedCallback(
            IntPtr managedHandle,
            IntPtr nativeHandle,
            PeerAddedOrRemovedCallback cb);

        void SetPeerRemovedCallback(
            IntPtr managedHandle,
            IntPtr nativeHandle,
            PeerAddedOrRemovedCallback cb);

        void SetDataReceivedCallback(
            IntPtr managedHandle,
            IntPtr nativeHandle,
            DataReceivedCallback cb);
    }
}

#pragma warning restore 0067
