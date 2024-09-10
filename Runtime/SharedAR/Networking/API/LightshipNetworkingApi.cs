// Copyright 2022-2024 Niantic.

using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Loader;
using UnityEngine;

#pragma warning disable 0067

namespace Niantic.Lightship.SharedAR.Networking.API
{
    internal class LightshipNetworkingApi : INetworkingApi
    {
        public IntPtr Init(string serverAddr, string roomId, string endpointPrefix)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            if (LightshipUnityContext.UnityContextHandle == IntPtr.Zero)
            {
                Log.Warning("Could not initialize networking. Lightship context is not initialized.");
                return IntPtr.Zero;
            }
            var endpointPrefixToNative = endpointPrefix;
            if (String.IsNullOrEmpty(endpointPrefixToNative))
            {
                // if empty, set the default prefix from config
                var settings = LightshipSettingsHelper.ActiveSettings.EndpointSettings;
                var marshEndpointSplit = settings.SharedArEndpoint.Split('.');
                if (marshEndpointSplit.Length > 0)
                {
                    endpointPrefixToNative = marshEndpointSplit[0];
                }
                else
                {
                    Log.Debug("Server prefix is empty and default server address is likely malformed");
                }
            }
            return _InitRoom(LightshipUnityContext.UnityContextHandle, roomId, endpointPrefixToNative);
#else
        throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public void Join(IntPtr nativeHandle)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _Join(nativeHandle);
#else
        throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public void Leave(IntPtr nativeHandle)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _Leave(nativeHandle);
#else
        throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public void Release(IntPtr nativeHandle)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _Release(nativeHandle);
#else
        throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public void SendData(
            IntPtr nativeHandle,
            UInt32 tag,
            byte[] data,
            UInt64 dataSize,
            UInt32[] peerIdentifiers
        )
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _SendData(nativeHandle, tag, data, dataSize, peerIdentifiers, (UInt64)peerIdentifiers.Length);
#else
      throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public byte GetNetworkingState(IntPtr nativeHandle)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            return _GetNetworkingState(nativeHandle);
#else
      throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public UInt32 GetSelfPeerId(IntPtr nativeHandle)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            return _GetSelfPeerId(nativeHandle);
#else
      throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public UInt64 GetPeerIds(IntPtr nativeHandle, UInt32[] outPeerIds, UInt64 maxPeers)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            return _GetPeerIds(nativeHandle, outPeerIds, maxPeers);
#else
      throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public void SetNetworkEventCallback
            (IntPtr managedHandle, IntPtr nativeHandle, INetworkingApi.NetworkEventCallback cb)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _SetConnectionEventCallback(managedHandle, nativeHandle, cb);
#else
      throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public void SetPeerAddedCallback
        (
            IntPtr managedHandle,
            IntPtr nativeHandle,
            INetworkingApi.PeerAddedOrRemovedCallback cb
        )
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _SetPeerAddedCallback(managedHandle, nativeHandle, cb);
#else
      throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public void SetPeerRemovedCallback
        (
            IntPtr managedHandle,
            IntPtr nativeHandle,
            INetworkingApi.PeerAddedOrRemovedCallback cb
        )
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _SetPeerRemovedCallback(managedHandle, nativeHandle, cb);
#else
      throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        public void SetDataReceivedCallback
        (
            IntPtr managedHandle,
            IntPtr nativeHandle,
            INetworkingApi.DataReceivedCallback cb
        )
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _SetDataReceivedCallback(managedHandle, nativeHandle, cb);
#else
      throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Sharc_Room_Init_With_Region")]
        private static extern IntPtr _InitRoom(IntPtr unityContextHandle, string roomId, string endpointPrefix);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Room_Join")]
        private static extern void _Join(IntPtr nativeHandle);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Room_Leave")]
        private static extern void _Leave(IntPtr nativeHandle);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Release")]
        private static extern void _Release(IntPtr nativeHandle);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Networking_SendData")]
        private static extern void _SendData
        (
            IntPtr nativeHandle,
            UInt32 tag,
            byte[] data,
            UInt64 dataSize,
            UInt32[] peerIdentifiers,
            UInt64 peerIdentifiersSize
        );

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Networking_GetNetworkingState")]
        private static extern byte _GetNetworkingState(IntPtr nativeHandle);

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Networking_GetSelfPeerId")]
        private static extern UInt32 _GetSelfPeerId
        (
            IntPtr nativeHandle
        );

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Networking_GetPeerIds")]
        private static extern UInt64 _GetPeerIds
        (
            IntPtr nativeHandle,
            UInt32[] outPeerIds,
            UInt64 maxPeers
        );

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Networking_SetNetworkingEventCallback")]
        private static extern void _SetConnectionEventCallback
        (
            IntPtr managedHandle,
            IntPtr nativeHandle,
            INetworkingApi.NetworkEventCallback cb
        );

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Networking_SetPeerAddedCallback")]
        private static extern void _SetPeerAddedCallback
        (
            IntPtr managedHandle,
            IntPtr nativeHandle,
            INetworkingApi.PeerAddedOrRemovedCallback cb
        );

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Networking_SetPeerRemovedCallback")]
        private static extern void _SetPeerRemovedCallback
        (
            IntPtr managedHandle,
            IntPtr nativeHandle,
            INetworkingApi.PeerAddedOrRemovedCallback cb
        );

        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Sharc_Networking_SetDataReceivedCallback")]
        private static extern void _SetDataReceivedCallback
        (
            IntPtr managedHandle,
            IntPtr nativeHandle,
            INetworkingApi.DataReceivedCallback cb
        );

#endif
    }
}

#pragma warning restore 0067
