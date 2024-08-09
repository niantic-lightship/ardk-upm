// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.SharedAR.Rooms.MarshMessages;
using System.Threading.Tasks;

namespace Niantic.Lightship.SharedAR.Rooms.Implementation
{
    /// @note This is an experimental feature. Experimental features should not be used in
    /// production products as they are subject to breaking changes, not officially supported, and
    /// may be deprecated without notice
    internal class _FakeRoomManagementServiceImpl :
        _IRoomManagementServiceImpl
    {
        internal static _FakeRoomManagementServiceImpl _Instance = new _FakeRoomManagementServiceImpl();
        private string _appId;
        private string _endpoint;
        private readonly Dictionary<string, _RoomInternal> _rooms = new Dictionary<string, _RoomInternal>();

        public void InitializeService(string endpoint, string appId, string apiKey)
        {
            _endpoint = endpoint;
            _appId = appId;
            _rooms.Clear();
            // ignore API key in the fake impl
        }

        public _CreateRoomResponse CreateRoom
        (
            _CreateRoomRequest request,
            out RoomManagementServiceStatus status
        )
        {
            var expId = request.experienceId;
            if (expId == "")
            {
                expId = RoomManagementService.DefaultExperienceId;
            }

            var room = new _RoomInternal()
            {
                roomId = Guid.NewGuid().ToString(),
                name = request.name,
                description = request.description,
                capacity = request.capacity,
                experienceId = expId,
                passcodeEnabled = !string.IsNullOrEmpty(request.passcode)
            };

            _rooms[room.roomId] = room;

            var res = new _CreateRoomResponse() { room = room };

            status = RoomManagementServiceStatus.Ok;
            return res;
        }

        public _CreateRoomResponse GetOrCreateRoom(_CreateRoomRequest request, out RoomManagementServiceStatus status)
        {
            foreach (var room in _rooms)
            {
                if (request.name == room.Value.name)
                {
                    status = RoomManagementServiceStatus.Ok;
                    return new _CreateRoomResponse() { room = room.Value };

                }
            }

            return CreateRoom(request, out status);
        }

        public _GetRoomResponse GetRoom(_GetRoomRequest request, out RoomManagementServiceStatus status)
        {
            if (!_rooms.ContainsKey(request.roomId))
            {
                status = RoomManagementServiceStatus.NotFound;
                return new _GetRoomResponse();
            }

            status = RoomManagementServiceStatus.Ok;
            return new _GetRoomResponse() { room = _rooms[request.roomId] };
        }

        public _GetRoomForExperienceResponse GetRoomsForExperience
        (
            _GetRoomForExperienceRequest request,
            out RoomManagementServiceStatus status
        )
        {
            if (_rooms.Count == 0)
            {
                status = RoomManagementServiceStatus.NotFound;
                return new _GetRoomForExperienceResponse() { };
            }
            var roomList = new List<_RoomInternal>();
            foreach (var room in _rooms.Values)
            {
                if (request.experienceIds == null ||
                    request.experienceIds.Count == 0 ||
                    room.experienceId.Equals(request.experienceIds.First())
                )
                {
                    // make order random, as server response may not be deterministic
                    if (UnityEngine.Random.value < 0.5)
                    {
                        roomList.Add(room);
                    }
                    else
                    {
                        roomList.Insert(0, room);
                    }
                }
            }

            status = RoomManagementServiceStatus.Ok;
            return new _GetRoomForExperienceResponse() { rooms = roomList };
        }

        public void DestroyRoom(_DestroyRoomRequest request, out RoomManagementServiceStatus status)
        {
            _rooms.Remove(request.roomId);
            status = RoomManagementServiceStatus.Ok;
        }

        public void ReleaseService()
        {
            _endpoint = null;
            _appId = null;
            _rooms.Clear();
            ;
        }
#pragma warning disable CS1998 // suppress warning due to not calling await function in async funcs
        public async Task<_IRoomManagementServiceImpl._AsyncCreateRoomResponse> CreateRoomAsync(
            _CreateRoomRequest request
        )
        {
            var response = CreateRoom(request, out var status);
            var res = new _IRoomManagementServiceImpl._AsyncCreateRoomResponse();
            res.CreateRoomResponse = response;
            res.Status = status;
            return res;
        }

        public async Task<_IRoomManagementServiceImpl._Async_GetRoomForExperienceResponse> GetRoomsForExperienceAsync
        (
            _GetRoomForExperienceRequest request
        )
        {
            var response = GetRoomsForExperience(request, out var status);
            var res = new _IRoomManagementServiceImpl._Async_GetRoomForExperienceResponse();
            res.GetRoomForExperienceResponse = response;
            res.Status = status;
            return res;
        }

        public async Task<_IRoomManagementServiceImpl._Async_CreateRoomResponse> GetOrCreateRoomAsync(_CreateRoomRequest request)
        {
            var response = GetOrCreateRoom(request, out var status);
            var res = new _IRoomManagementServiceImpl._Async_CreateRoomResponse();
            res.CreateRoomResponse = response;
            res.Status = status;
            return res;
        }

#pragma warning restore CS1998

    }
}
