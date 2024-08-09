// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.SharedAR.Networking;
using Niantic.Lightship.SharedAR.Datastore;

namespace Niantic.Lightship.SharedAR.Rooms
{
    public class Room :
        IRoom
    {
        public Room(RoomParams roomParams)
        {
            RoomParams = roomParams;
        }

        public RoomParams RoomParams { get; internal set; }
        public INetworking Networking { get; private set; }
        public IDatastore Datastore { get; private set; }

        public void Initialize()
        {
            if (Networking == null)
            {
                Networking = new LightshipNetworking("", RoomParams.RoomID, RoomParams.Endpoint);
            }

            if (Datastore == null)
            {
                var lightshipNetworking = Networking as LightshipNetworking;
                if (lightshipNetworking != null)
                {
                    Datastore = new LightshipDatastore(lightshipNetworking._nativeHandle);
                }
            }
        }

        public void Join()
        {
            if (Networking != null)
            {
                Networking.Join();
            }
            else
            {
                Log.Warning("Attempting to join network but network is not initialized");
            }
        }

        public void Leave()
        {
            Networking?.Leave();
        }

        public void Dispose()
        {
            if (Networking != null)
            {
                Networking.Dispose();
                Networking = null;
            }
            if (Datastore != null)
            {
                Datastore.Dispose();
                Datastore = null;
            }
        }
    }
}
