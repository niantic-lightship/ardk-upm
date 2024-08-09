// Copyright 2022-2024 Niantic.

#pragma warning disable 0067

using System;
using System.Collections.Concurrent;

using AOT; // MonoPInvokeCallback attribute
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.SharedAR.Networking.API;
using Niantic.Lightship.SharedAR.Settings;


namespace Niantic.Lightship.SharedAR.Networking
{
    // @note This is an experimental feature. Experimental features should not be used in
    // production products as they are subject to breaking changes, not officially supported,
    // and may be deprecated without notice
    public class LightshipNetworking : INetworking
    {
        private bool _isDestroyed;
        private bool _didSubscribeToNativeEvents;
        private PeerID _selfPeerId = PeerID.InvalidID;
        private INetworkingApi _nativeApi;
        internal IntPtr _nativeHandle;

        #region Handles

        private IntPtr _cachedHandleIntPtr = IntPtr.Zero;
        private GCHandle _cachedHandle;

        // Approx memory size of native object
        // Magic number for 64
        private const long GCPressure = 64L * 1024L;

        // Used to round-trip a pointer through c++,
        // so that we can keep our this pointer even in c# functions
        // marshaled and called by native code
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

        private const string DEFAULT_SESSION = "default_session_id";
        private const UInt64 MAX_PEER_COUNT = 32;

        public string SessionId { get; private set; } = DEFAULT_SESSION;
        public event Action<NetworkEventArgs> NetworkEvent;
        public event Action<PeerIDArgs> PeerAdded;
        public event Action<PeerIDArgs> PeerRemoved;
        public event Action<DataReceivedArgs> DataReceived;

        // It is better to lock on a private readonly object rather than the queues themselves, this prevents
        //  the lock from being replaced by a new object if the queues are replaced.
        // It also prevents future refactor problems (if the queues are ever made public, another class
        //  locking on them can cause deadlocks)
        private readonly object _eventQueueLock = new object();

        // Use separate queues instead of a generic "object" to avoid casting and boxing structs
        private readonly List<NetworkEventArgs> _networkEventQueue = new List<NetworkEventArgs>();
        private readonly List<PeerIDArgs> _peerAddedQueue = new List<PeerIDArgs>();
        private readonly List<PeerIDArgs> _peerRemovedQueue = new List<PeerIDArgs>();
        private readonly List<DataReceivedArgs> _dataReceivedQueue = new List<DataReceivedArgs>();
        private readonly List<NetworkEventType> _eventOrderQueue = new List<NetworkEventType>();

        private enum NetworkEventType
        {
            Unknown,
            NetworkEvent,
            PeerAdded,
            PeerRemoved,
            DataReceived
        }

        public LightshipNetworking
        (
            string serverAddr,
            string roomId,
            string endpointPrefix = ""
        ) : this(serverAddr, roomId, new LightshipNetworkingApi(), endpointPrefix
        )
        {
        }

        internal LightshipNetworking
        (
            string serverAddr,
            string roomId,
            INetworkingApi api,
            string endpointPrefix = ""
        )
        {
            LightshipUnityContext.OnDeinitialized += HandleArdkDeinitialized;
            MonoBehaviourEventDispatcher.Updating.AddListener(FlushQueue, (int)SharedAREventExecutionOrder.Networking);
            _nativeApi = api;
            _nativeHandle = _nativeApi.Init(serverAddr, roomId, endpointPrefix);
            if (!IsNativeHandleValid())
            {
                return;
            }
            GC.AddMemoryPressure(GCPressure);
            SessionId = roomId;
            SubscribeToNativeCallbacks();
        }

        private void FlushQueue()
        {
            // If the object has been disposed, don't surface any events
            if (!IsNativeHandleValid())
            {
                return;
            }

            // Make local copies of the lists so that we don't hold the lock while invoking the events
            List<NetworkEventArgs> networkEventQueueCopy;
            List<PeerIDArgs> peerAddedQueueCopy;
            List<PeerIDArgs> peerRemovedQueueCopy;
            List<DataReceivedArgs> dataReceivedQueueCopy;
            List<NetworkEventType> eventOrderQueueCopy;
            lock (_eventQueueLock)
            {
                networkEventQueueCopy = new List<NetworkEventArgs>(_networkEventQueue);
                _networkEventQueue.Clear();
                peerAddedQueueCopy = new List<PeerIDArgs>(_peerAddedQueue);
                _peerAddedQueue.Clear();
                peerRemovedQueueCopy = new List<PeerIDArgs>(_peerRemovedQueue);
                _peerRemovedQueue.Clear();
                dataReceivedQueueCopy = new List<DataReceivedArgs>(_dataReceivedQueue);
                _dataReceivedQueue.Clear();
                eventOrderQueueCopy = new List<NetworkEventType>(_eventOrderQueue);
                _eventOrderQueue.Clear();
            }

            // Cache exceptions so that one exception doesn't prevent other events from being surfaced
            var exceptionList = new List<Exception>();
            if (eventOrderQueueCopy.Count !=
                networkEventQueueCopy.Count +
                peerAddedQueueCopy.Count +
                peerRemovedQueueCopy.Count +
                dataReceivedQueueCopy.Count)
            {
                Log.Error("Sum of events does not match total event count");
            }

            foreach (var netEvent in eventOrderQueueCopy)
            {
                switch (netEvent)
                {
                    case NetworkEventType.DataReceived:
                        if (dataReceivedQueueCopy.Count == 0)
                        {
                            Log.Error("DataReceived event was not surfaced");
                            break;
                        }
                        else
                        {
                            var eventArgs = dataReceivedQueueCopy[0];
                            dataReceivedQueueCopy.RemoveAt(0);
                            try
                            {
                                DataReceived?.Invoke(eventArgs);
                            }
                            catch (Exception e)
                            {
                                Log.Exception(e);
                                exceptionList.Add(e);
                            }
                        }

                        break;
                    case NetworkEventType.NetworkEvent:
                        if (networkEventQueueCopy.Count == 0)
                        {
                            Log.Error("NetworkEvent event was not surfaced");
                            break;
                        }
                        else
                        {
                            var eventArgs = networkEventQueueCopy[0];
                            networkEventQueueCopy.RemoveAt(0);
                            try
                            {
                                NetworkEvent?.Invoke(eventArgs);
                            }
                            catch (Exception e)
                            {
                                Log.Exception(e);
                                exceptionList.Add(e);
                            }
                        }

                        break;
                    case NetworkEventType.PeerAdded:
                        if (peerAddedQueueCopy.Count == 0)
                        {
                            Log.Error("PeerAdded event was not surfaced");
                            break;
                        }
                        else
                        {
                            var eventArgs = peerAddedQueueCopy[0];
                            peerAddedQueueCopy.RemoveAt(0);
                            try
                            {
                                PeerAdded?.Invoke(eventArgs);
                            }
                            catch (Exception e)
                            {
                                Log.Exception(e);
                                exceptionList.Add(e);
                            }
                        }

                        break;
                    case NetworkEventType.PeerRemoved:
                        if (peerRemovedQueueCopy.Count == 0)
                        {
                            Log.Error("PeerRemoved event was not surfaced");
                            break;
                        }
                        else
                        {
                            var eventArgs = peerRemovedQueueCopy[0];
                            peerRemovedQueueCopy.RemoveAt(0);
                            try
                            {
                                PeerRemoved?.Invoke(eventArgs);
                            }
                            catch (Exception e)
                            {
                                Log.Exception(e);
                                exceptionList.Add(e);
                            }
                        }

                        break;
                    case NetworkEventType.Unknown:
                    default:
                        Log.Error("Unknown event type");
                        break;
                }
            }

            // Fire exceptions in list
            foreach (var exception in exceptionList)
            {
                throw exception;
            }
        }

        private bool IsNativeHandleValid()
        {
            if (_nativeHandle == IntPtr.Zero)
            {
                Log.Warning("Invalid native handle");
                return false;
            }
            return true;
        }

        public void Join()
        {
            if (!IsNativeHandleValid())
            {
                return;
            }
            _nativeApi.Join(_nativeHandle);
        }

        public void SendData
        (
            List<PeerID> targetPeers,
            uint tag,
            byte[] data
        )
        {
            if (!IsNativeHandleValid())
            {
                return;
            }
            var peerIdentifiers = new UInt32[targetPeers.Count];
            for (var i = 0; i < targetPeers.Count; i++)
            {
                peerIdentifiers[i] = targetPeers[i].ToUint32();
            }

            _nativeApi.SendData
            (
                _nativeHandle,
                tag,
                data,
                (ulong)data.Length,
                peerIdentifiers
            );
        }

        public NetworkState NetworkState
        {
            get
            {
                if (!IsNativeHandleValid())
                {
                    return NetworkState.NotInRoom;
                }
                return (NetworkState)_nativeApi.GetNetworkingState(_nativeHandle);
            }
        }

        public PeerID SelfPeerID
        {
            get
            {
                if (!IsNativeHandleValid())
                {
                    return PeerID.InvalidID;
                }

                if (_selfPeerId.Equals(PeerID.InvalidID))
                    _selfPeerId = new PeerID(_nativeApi.GetSelfPeerId(_nativeHandle));

                return _selfPeerId;
            }
        }

        public List<PeerID> PeerIDs
        {
            get
            {
                if (!IsNativeHandleValid())
                {
                    return null;
                }

                var outPeers = new UInt32[MAX_PEER_COUNT];
                var count = _nativeApi.GetPeerIds(_nativeHandle, outPeers, MAX_PEER_COUNT);

                var list = new List<PeerID>();
                for (UInt64 i = 0; i < count; ++i)
                    list.Add(new PeerID(outPeers[i]));

                return list;
            }
        }

        public void Leave()
        {
            if (!IsNativeHandleValid())
            {
                return;
            }
            _nativeApi.Leave(_nativeHandle);
        }

        public void Dispose()
        {
            LightshipUnityContext.OnDeinitialized -= HandleArdkDeinitialized;
            MonoBehaviourEventDispatcher.Updating.RemoveListener(FlushQueue);
            if (!IsNativeHandleValid())
            {
                return;
            }
            _nativeApi.Release(_nativeHandle);
            _registeredNetworking = null;
            _nativeHandle = IntPtr.Zero;
            lock (_eventQueueLock)
            {
                _networkEventQueue.Clear();
                _peerAddedQueue.Clear();
                _peerRemovedQueue.Clear();
                _dataReceivedQueue.Clear();
                _eventOrderQueue.Clear();
            }
        }

        // TODO: Temporary solution until AR-16347
        private static LightshipNetworking _registeredNetworking;

        private void SubscribeToNativeCallbacks()
        {
            if (_didSubscribeToNativeEvents)
                return;

            lock (this)
            {
                if (_didSubscribeToNativeEvents)
                    return;

                _registeredNetworking = this;

                _nativeApi.SetNetworkEventCallback
                    (_managedHandle, _nativeHandle, _networkEventReceivedNative);
                _nativeApi.SetPeerAddedCallback(_managedHandle, _nativeHandle, _didAddPeerNative);
                _nativeApi.SetPeerRemovedCallback(_managedHandle, _nativeHandle, _didRemovePeerNative);
                _nativeApi.SetDataReceivedCallback(_managedHandle, _nativeHandle, _dataReceivedNative);

                _didSubscribeToNativeEvents = true;
            }
        }

        private void HandleArdkDeinitialized()
        {
            // Invoke ArdkShutdown event when ARDK is deinitializing, so that user of Networking can dispose
            // Networking resources
            LightshipUnityContext.OnDeinitialized -= HandleArdkDeinitialized;
            var args = new NetworkEventArgs(NetworkEvents.ArdkShutdown, 0);
            NetworkEvent?.Invoke(args);
        }

        [MonoPInvokeCallback(typeof(INetworkingApi.NetworkEventCallback))]
        private static void _networkEventReceivedNative(IntPtr managedHandle, byte networkEvent, UInt32 errorCode)
        {
            var instance = GCHandle.FromIntPtr(managedHandle).Target as LightshipNetworking;

            if (instance == null || instance._isDestroyed)
                return;

            var handler = instance.NetworkEvent;

            if (handler != null)
            {
                lock (instance._eventQueueLock)
                {
                    instance._eventOrderQueue.Add(NetworkEventType.NetworkEvent);
                    instance._networkEventQueue.Add(new NetworkEventArgs((NetworkEvents)networkEvent, errorCode));
                }
            }
        }

        [MonoPInvokeCallback(typeof(INetworkingApi.PeerAddedOrRemovedCallback))]
        private static void _didAddPeerNative(IntPtr managedHandle, UInt32 peerIdUint)
        {
            var instance = GCHandle.FromIntPtr(managedHandle).Target as LightshipNetworking;

            if (instance == null || instance._isDestroyed)
            {
                return;
            }

            var peerId = new PeerID(peerIdUint);
            var handler = instance.PeerAdded;

            if (handler != null)
            {
                lock (instance._eventQueueLock)
                {
                    instance._eventOrderQueue.Add(NetworkEventType.PeerAdded);
                    instance._peerAddedQueue.Add(new PeerIDArgs(peerId));
                }
            }
        }

        [MonoPInvokeCallback(typeof(INetworkingApi.PeerAddedOrRemovedCallback))]
        private static void _didRemovePeerNative(IntPtr managedHandle, UInt32 peerIdUint)
        {
            var instance = GCHandle.FromIntPtr(managedHandle).Target as LightshipNetworking;

            if (instance == null || instance._isDestroyed)
            {
                Log.Warning("_didRemovePeerNative invoked after C# instance was destroyed.");
                return;
            }

            var peerId = new PeerID(peerIdUint);
            var handler = instance.PeerRemoved;

            if (handler != null)
            {
                lock (instance._eventQueueLock)
                {
                    instance._eventOrderQueue.Add(NetworkEventType.PeerRemoved);
                    instance._peerRemovedQueue.Add(new PeerIDArgs(peerId));
                }
            }
        }

        [MonoPInvokeCallback(typeof(INetworkingApi.DataReceivedCallback))]
        private static void _dataReceivedNative
        (
            IntPtr managedHandle,
            UInt32 fromPeerId,
            UInt32 tag,
            IntPtr rawData,
            UInt64 rawDataSize
        )
        {
            var instance = GCHandle.FromIntPtr(managedHandle).Target as LightshipNetworking;

            if (instance == null || instance._isDestroyed)
            {
                Log.Warning("_dataReceivedNative called after C# instance was destroyed.");
                return;
            }

            var data = new byte[rawDataSize];
            Marshal.Copy(rawData, data, 0, (int)rawDataSize);

            var peerId = new PeerID(fromPeerId);
            var handler = instance.DataReceived;

            if (handler != null)
            {
                lock (instance._eventQueueLock)
                {
                    instance._eventOrderQueue.Add(NetworkEventType.DataReceived);
                    instance._dataReceivedQueue.Add(new DataReceivedArgs(peerId, tag, data));
                }
            }
        }
    }
}
#pragma warning restore 0067
