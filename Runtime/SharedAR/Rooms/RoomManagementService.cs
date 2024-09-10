// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.SharedAR.Rooms.MarshMessages;
using Niantic.Lightship.SharedAR.Rooms.Implementation;
using UnityEngine;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities;
using AOT; // MonoPInvokeCallback attribute

namespace Niantic.Lightship.SharedAR.Rooms
{
    /// <summary>
    /// Status of Room Management Service requests. Values corresponds to HTTP response codes.
    /// </summary>
    [PublicAPI]
    public enum RoomManagementServiceStatus :
        Int32
    {
        Ok = 200,
        BadRequest = 400,
        Unauthorized = 401,
        NotFound = 404,
        AsyncApiFailure = 460, // Unused http code. Developers should check != Ok over == AsyncApiFailure
    }

    /// <summary>
    /// The RoomManagementService provides interface to access Room Management Service backend to create, remove,
    /// find Rooms. A room is an entity to connect multiple peers through server relayed network.
    /// </summary>
    [PublicAPI]
    public static class RoomManagementService
    {
        private static _IRoomManagementServiceImpl _serviceImpl;

        // Prod Marsh REST endpoint
        private static string _marshEndPoint;

        // Default ExperienceID
        internal static string DefaultExperienceId { get; private set; } = "";

        static RoomManagementService()
        {
            _serviceImpl = _HttpRoomManagementServiceImpl._Instance;
            var appId = Application.identifier;

            var lightshipSettings = LightshipSettingsHelper.ActiveSettings;
            var apiKey = lightshipSettings.ApiKey;
            _marshEndPoint = lightshipSettings.EndpointSettings.SharedArEndpoint;
            _serviceImpl.InitializeService(_marshEndPoint, appId, apiKey);
        }

        internal static void _InitializeServiceForIntegrationTesting(string apiKey, string marshEndpoint)
        {
            _serviceImpl = _HttpRoomManagementServiceImpl._Instance;
            var appId = Application.identifier;
            _marshEndPoint = marshEndpoint;
            _serviceImpl.InitializeService(_marshEndPoint, appId, apiKey);
            DefaultExperienceId = "";
        }

        internal static void _InitializeServiceForTesting()
        {
            _serviceImpl = _FakeRoomManagementServiceImpl._Instance;
            var appId = Application.identifier;
            _serviceImpl.InitializeService("", appId, "");
            DefaultExperienceId = "experienceId";
        }

        /// <summary>
        /// Create a new room on the server.
        /// </summary>
        /// <param name="roomParams">Parameters of the room</param>
        /// <param name="outRoom">Created room as IRoom object. null if failed to create.</param>
        /// <returns>Status of the operation</returns>
        [PublicAPI]
        public static RoomManagementServiceStatus CreateRoom
        (
            RoomParams roomParams,
            out IRoom outRoom
        )
        {
            outRoom = null;
            if (_serviceImpl == null)
            {
                Log.Error("Must initialize RoomManagementService before using");
                return RoomManagementServiceStatus.BadRequest;
            }

            var request = new _CreateRoomRequest()
            {
                experienceId = roomParams.ExperienceId,
                name = roomParams.Name,
                description = roomParams.Description,
                capacity = roomParams.Capacity,
                passcode = roomParams.Visibility == RoomVisibility.Private ? roomParams.Passcode : "",
                region = "AUTO",
            };

            var response = _serviceImpl.CreateRoom(request, out var status);
            if (status != RoomManagementServiceStatus.Ok)
            {
                Log.Warning($"Room Management Create request failed with status {status}");
                return status;
            }

            outRoom = new Room(response.room);

            return RoomManagementServiceStatus.Ok;
        }

        private static RoomManagementServiceStatus GetRoomsForExperience(string experienceId, out List<IRoom> rooms)
        {
            rooms = new List<IRoom>();
            if (_serviceImpl == null)
            {
                Log.Error("Must initialize RoomManagementService before using");
                return RoomManagementServiceStatus.BadRequest;
            }

            _GetRoomForExperienceRequest request;

            if (string.IsNullOrEmpty(experienceId))
            {
                request = new _GetRoomForExperienceRequest() { };
            }
            else
            {
                request = new _GetRoomForExperienceRequest() { experienceIds = new List<string>() { experienceId } };
            }


            var response = _serviceImpl.GetRoomsForExperience(request, out var status);
            if (status != RoomManagementServiceStatus.Ok)
            {
                Log.Warning($"Room Management Get request failed with status {status}");
                return status;
            }

            // return not found if count is zero
            if (response.rooms.Count == 0)
            {
                return RoomManagementServiceStatus.NotFound;
            }

            foreach (var room in response.rooms)
            {
                rooms.Add(new Room(room));
            }

            return RoomManagementServiceStatus.Ok;
        }

        /// <summary>
        /// Delete a room on the server.
        /// </summary>
        /// <param name="roomId">Room ID of the room to delete</param>
        /// <returns>Status of the operation</returns>
        [PublicAPI]
        public static RoomManagementServiceStatus DeleteRoom(string roomId)
        {
            if (_serviceImpl == null)
            {
                Log.Error("Must initialize RoomManagementService before using");
                return RoomManagementServiceStatus.BadRequest;
            }

            var request = new _DestroyRoomRequest() { roomId = roomId };

            _serviceImpl.DestroyRoom(request, out var status);
            if (status != RoomManagementServiceStatus.Ok)
            {
                Log.Warning($"Room Management Destroy request failed with status {status}. id={roomId}");
                return status;
            }

            return RoomManagementServiceStatus.Ok;
        }

        /// <summary>
        /// Get a room by Room ID on the server
        /// </summary>
        /// <param name="roomId">Room ID as a string</param>
        /// <param name="outRoom">Found Room object. Null if operation failed or room ID not found<//param>
        /// <returns>Status of the operation</returns>
        [PublicAPI]
        public static RoomManagementServiceStatus GetRoom(string roomId, out IRoom outRoom)
        {
            outRoom = null;
            if (_serviceImpl == null)
            {
                Log.Error("Must initialize RoomManagementService before using");
                return RoomManagementServiceStatus.BadRequest;
            }

            var request = new _GetRoomRequest() { roomId = roomId };

            var response = _serviceImpl.GetRoom(request, out var status);
            if (status != RoomManagementServiceStatus.Ok)
            {
                Log.Warning($"Room Management Get request failed with status {status}");
                return status;
            }

            outRoom = new Room(response.room);

            return RoomManagementServiceStatus.Ok;
        }

        /// <summary>
        /// Query room(s) by name on the server
        /// </summary>
        /// <param name="name">Name of the room to find</param>
        /// <param name="rooms">A List of rooms which has matching name </param>
        /// <returns>Status of the operation</returns>
        [PublicAPI]
        public static RoomManagementServiceStatus QueryRoomsByName(string name, out List<IRoom> rooms)
        {
            var status = GetRoomsForExperience(
                DefaultExperienceId, out rooms);
            if (status == RoomManagementServiceStatus.Ok)
            {
                for (int i = rooms.Count - 1; i >= 0; i--)
                {
                    if (rooms[i].RoomParams.Name != name)
                    {
                        rooms.RemoveAt(i);
                    }
                }
            }

            // return not found if count is zero
            if (rooms.Count == 0)
            {
                return RoomManagementServiceStatus.NotFound;
            }

            return status;
        }

        /// <summary>
        /// Get all rooms on the server, which was created by this app
        /// </summary>
        /// <param name="rooms">List of rooms available for this app</param>
        /// <returns>Status of the operation</returns>
        [PublicAPI]
        public static RoomManagementServiceStatus GetAllRooms(out List<IRoom> rooms)
        {
            var status = GetRoomsForExperience(
                DefaultExperienceId, out rooms);

            return status;
        }

        /// <summary>
        /// Get a IRoom object that has a given name on the server.
        /// If no room found with the name, create a new room using given room parameters
        /// </summary>
        /// <param name="roomParams">Room parameters of the room to get or create</param>
        /// <param name="outRoom">IRoom object. null if server operarion failed</param>
        /// <returns>Status of the operation</returns>
        [PublicAPI]
        public static RoomManagementServiceStatus GetOrCreateRoomForName
        (
            RoomParams roomParams,
            out IRoom outRoom
        )
        {
            outRoom = null;
            if (_serviceImpl == null)
            {
                Log.Error("Must initialize RoomManagementService before using");
                return RoomManagementServiceStatus.BadRequest;
            }

            var request = new _CreateRoomRequest()
            {
                experienceId = roomParams.ExperienceId,
                name = roomParams.Name,
                description = roomParams.Description,
                capacity = roomParams.Capacity,
                passcode = roomParams.Visibility == RoomVisibility.Private ? roomParams.Passcode : "",
                region = "AUTO",
            };

            var response = _serviceImpl.GetOrCreateRoom(request, out var status);
            if (status != RoomManagementServiceStatus.Ok)
            {
                Log.Warning($"Room Management GetOrCreateRoom request failed with status {status}");
                return status;
            }

            outRoom = new Room(response.room);

            return RoomManagementServiceStatus.Ok;
        }

        [Obsolete]
        public delegate void GetOrCreateRoomCallback(RoomManagementServiceStatus status, string room_id);

        private static uint _cbCounter = 0;
        [Obsolete]
        private static Dictionary<uint, GetOrCreateRoomCallback> _getOrCreateRoomCbs = new();

        [MonoPInvokeCallback(typeof(InternalGetOrCreateRoomCallback))]
        [Obsolete]
        private static void StaticGetOrCreateRoomCallback(UInt32 id, UInt32 status, string roomId)
        {
            RoomManagementServiceStatus translatedStatus = RoomManagementServiceStatus.AsyncApiFailure;
            switch (status) {
                case 0: // Failed
                    translatedStatus = RoomManagementServiceStatus.AsyncApiFailure;
                    break;
                case 1: // Created
                    translatedStatus = RoomManagementServiceStatus.Ok;
                    break;
                case 2: // Found
                    translatedStatus = RoomManagementServiceStatus.Ok;
                    break;

            }
            _getOrCreateRoomCbs[id].Invoke(translatedStatus, roomId);
            _getOrCreateRoomCbs.Remove(id);
        }

        /// <summary>
        /// An async implementation of the GetOrCreateRoom request. This function checks to see if
        /// any rooms of the name "roomName" exist and if not, it creates the room. Once the
        /// function has a valid RoomID from either of these operations, it returns it via the
        /// "doneCb". The "doneCb" has two parameters. The first is a response code in case there
        /// are service issues, and the second parameter is the room id that was found/created.
        /// </summary>
        /// <param name="roomName">Room name to check for</param>
        /// <param name="roomDesc">Room description to use if a room needs to be made</param>
        /// <param name="roomCapacity">Room capacity to use if a room needs to be made</param>
        /// <param name="doneCb">Callback that the function calls after it errors or receives a
        ///     valid room id.
        /// </param>
        [PublicAPI]
        [Obsolete]
        public static void GetOrCreateRoomAsync(string roomName, string roomDesc, uint roomCapacity, GetOrCreateRoomCallback doneCb)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            if (LightshipUnityContext.UnityContextHandle == IntPtr.Zero)
            {
                Log.Warning("Could not initialize networking. Lightship context is not initialized.");
                return;
            }
            var callbackId = _cbCounter++;
            _getOrCreateRoomCbs.Add(callbackId, doneCb);
            _GetOrCreateRoom(LightshipUnityContext.UnityContextHandle, roomName, roomDesc, roomCapacity, callbackId, StaticGetOrCreateRoomCallback);
#else
            throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        /// <summary>
        /// Struct which holds the return values for GetOrCreateRoomAsync.
        /// </summary>
        [PublicAPI]
        public readonly struct GetOrCreateRoomAsyncTaskResult
        {
            public readonly RoomManagementServiceStatus Status;
            [Obsolete]
            public string RoomId
            {
                get
                {
                    if (Room == null)
                    {
                        return "";
                    }
                    else
                    {
                        return Room.RoomParams.RoomID;
                    }
                }
            }
            public readonly IRoom Room;

            internal GetOrCreateRoomAsyncTaskResult(
                RoomManagementServiceStatus status,
                IRoom room)
            {
                Status = status;
                Room = room;
            }
        }

        /// <summary>
        /// An async implementation of the GetOrCreateRoom request. This function checks to see if
        /// any rooms of the name "roomName" exist and if not, it creates the room. This variant
        /// returns a Task so it can be `await`-ed by C#'s `async/await` feature. The task is given
        /// two results, the status of the request and, if successful, the roomId for the request.
        /// </summary>
        /// <param name="roomName">Room name to check for</param>
        /// <param name="roomDescription">Room description to use if a room needs to be made</param>
        /// <param name="roomCapacity">Room capacity to use if a room needs to be made</param>
        /// <returns>Task with the request status and the roomId on successful requests</returns>
        [PublicAPI]
        public static async Task<GetOrCreateRoomAsyncTaskResult> GetOrCreateRoomAsync(
            string roomName, string roomDescription, uint roomCapacity)
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            if (_serviceImpl == null)
            {
                Log.Error("Must initialize RoomManagementService before using");
                return new GetOrCreateRoomAsyncTaskResult(RoomManagementServiceStatus.BadRequest, null);
            }

            var request = new _CreateRoomRequest()
            {
                experienceId = "",
                name = roomName,
                description = roomDescription,
                capacity = (int) roomCapacity,
                passcode = "",
                region = "AUTO",
            };

            var response = await _serviceImpl.GetOrCreateRoomAsync(request);
            var status = response.Status;
            if (status != RoomManagementServiceStatus.Ok)
            {
                Log.Warning($"Room Management GetOrCreateRoom request failed with status {status}");
                return new GetOrCreateRoomAsyncTaskResult(status, null);;
            }
            return new GetOrCreateRoomAsyncTaskResult(
                status,
                new Room(response.CreateRoomResponse.room));
#else
            throw new PlatformNotSupportedException("Unsupported platform");
#endif
        }

        //

        private delegate void InternalGetOrCreateRoomCallback
        (
            UInt32 callbackId,
            UInt32 status,
            string roomId
        );


        [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_Sharc_Room_GetOrCreateAsync")]
        private static extern void _GetOrCreateRoom(IntPtr unityContextHandle, string roomName, string roomDescription, uint capacity, uint callbackId, InternalGetOrCreateRoomCallback callback);
    }
}
