// Copyright 2022-2024 Niantic.

using System;
using System.Text;
using System.Threading;
using Niantic.Lightship.SharedAR.Rooms.MarshMessages;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using Niantic.Lightship.AR.VpsCoverage;

namespace Niantic.Lightship.SharedAR.Rooms.Implementation
{
    /// @note This is an experimental feature. Experimental features should not be used in
    /// production products as they are subject to breaking changes, not officially supported, and
    /// may be deprecated without notice
    internal class _HttpRoomManagementServiceImpl :
        _IRoomManagementServiceImpl
    {
        internal static _HttpRoomManagementServiceImpl _Instance = new _HttpRoomManagementServiceImpl();
        private string _endpoint;
        private string _appId;
        private const string _appIdHeader = "x-apig-appid";
        private const string _apiKeyHeader = "Authorization";
        private string _apiKey;

        private string _createFormat = "https://{0}/room/create";
        private string _getOrCreateFormat = "https://{0}/room/get_or_create";
        private string _getFormat = "https://{0}/room/get_room";
        private string _destroyFormat = "https://{0}/room/destroy";
        private string _getRoomsForExperienceFormat = "https://{0}/room/get_rooms";

        public void InitializeService(string endpoint, string appId, string apiKey)
        {
            _endpoint = endpoint;
            _appId = appId;
            _apiKey = apiKey;
        }

        public _CreateRoomResponse CreateRoom
        (
            _CreateRoomRequest request,
            out RoomManagementServiceStatus status
        )
        {
            var json = JsonUtility.ToJson(request);

            var uri = String.Format(_createFormat, _endpoint);
            var response = SendBlockingWebRequest(uri, json, out var s);

            if (String.IsNullOrEmpty(response))
            {
                status = (RoomManagementServiceStatus)s;
                return new _CreateRoomResponse();
            }

            var res = JsonUtility.FromJson<_CreateRoomResponse>(response);
            status = RoomManagementServiceStatus.Ok;
            return res;
        }

        public _CreateRoomResponse GetOrCreateRoom(_CreateRoomRequest request, out RoomManagementServiceStatus status)
        {
            var json = JsonUtility.ToJson(request);

            var uri = String.Format(_getOrCreateFormat, _endpoint);
            var response = SendBlockingWebRequest(uri, json, out var s);

            if (String.IsNullOrEmpty(response) || s != 200)
            {
                status = (RoomManagementServiceStatus)s;
                return new _CreateRoomResponse();
            }

            var res = JsonUtility.FromJson<_CreateRoomResponse>(response);
            status = RoomManagementServiceStatus.Ok;
            return res;
        }

        public _GetRoomResponse GetRoom(_GetRoomRequest request, out RoomManagementServiceStatus status)
        {
            var json = JsonUtility.ToJson(request);

            var uri = String.Format(_getFormat, _endpoint);
            var response = SendBlockingWebRequest(uri, json, out var s);

            if (String.IsNullOrEmpty(response))
            {
                status = (RoomManagementServiceStatus)s;
                return new _GetRoomResponse();
            }

            var res = JsonUtility.FromJson<_GetRoomResponse>(response);
            status = RoomManagementServiceStatus.Ok;
            return res;
        }

        public _GetRoomForExperienceResponse GetRoomsForExperience
        (
            _GetRoomForExperienceRequest request,
            out RoomManagementServiceStatus status
        )
        {
            var json = JsonUtility.ToJson(request);

            var uri = String.Format(_getRoomsForExperienceFormat, _endpoint);
            var response = SendBlockingWebRequest(uri, json, out var s);

            if (String.IsNullOrEmpty(response))
            {
                status = (RoomManagementServiceStatus)s;
                return new _GetRoomForExperienceResponse();
            }

            var res = JsonUtility.FromJson<_GetRoomForExperienceResponse>(response);
            status = RoomManagementServiceStatus.Ok;
            return res;
        }

        public void DestroyRoom(_DestroyRoomRequest request, out RoomManagementServiceStatus status)
        {
            var json = JsonUtility.ToJson(request);

            var uri = String.Format(_destroyFormat, _endpoint);
            SendBlockingWebRequest(uri, json, out var s);

            status = (RoomManagementServiceStatus)s;
        }

        public void ReleaseService()
        {
            _endpoint = null;
            _appId = null;
        }

        // Async version of CreateRoomA
        public async Task<_IRoomManagementServiceImpl._AsyncCreateRoomResponse> CreateRoomAsync(
            _CreateRoomRequest request
        )
        {
            var json = JsonUtility.ToJson(request);
            var uri = String.Format(_createFormat, _endpoint);

            // send a http request
            var httpResponse = await SendWebRequestAsync(uri, json);

            var response  = new _IRoomManagementServiceImpl._AsyncCreateRoomResponse();
            response.Status = (RoomManagementServiceStatus)httpResponse.status;
            if (String.IsNullOrEmpty(httpResponse.responseData))
            {
                return response;
            }

            var res = JsonUtility.FromJson<_CreateRoomResponse>(httpResponse.responseData);
            response.CreateRoomResponse = res;

            return response;

        }

        // Async version of GetRoomsForExperience
        public async Task<_IRoomManagementServiceImpl._Async_GetRoomForExperienceResponse> GetRoomsForExperienceAsync
        (
            _GetRoomForExperienceRequest request
        )
        {
            var json = JsonUtility.ToJson(request);
            var uri = String.Format(_getRoomsForExperienceFormat, _endpoint);

            // send a http request
            var httpResponse = await SendWebRequestAsync(uri, json);

            var response  = new _IRoomManagementServiceImpl._Async_GetRoomForExperienceResponse();
            response.Status = (RoomManagementServiceStatus)httpResponse.status;
            if (String.IsNullOrEmpty(httpResponse.responseData))
            {
                return response;
            }

            var res = JsonUtility.FromJson<_GetRoomForExperienceResponse>(httpResponse.responseData);
            response.GetRoomForExperienceResponse = res;
            return response;
        }

        // Async version of GetOrCreateRoom
        public async Task<_IRoomManagementServiceImpl._Async_CreateRoomResponse> GetOrCreateRoomAsync(_CreateRoomRequest request)
        {
            var json = JsonUtility.ToJson(request);
            var uri = String.Format(_getOrCreateFormat, _endpoint);

            // send a http request
            var httpResponse = await SendWebRequestAsync(uri, json);

            var response  = new _IRoomManagementServiceImpl._Async_CreateRoomResponse();
            response.Status = (RoomManagementServiceStatus)httpResponse.status;
            if (String.IsNullOrEmpty(httpResponse.responseData))
            {
                return response;
            }

            var res = JsonUtility.FromJson<_CreateRoomResponse>(httpResponse.responseData);
            response.CreateRoomResponse = res;
            return response;
        }

        private string SendBlockingWebRequest(string uri, string body, out int status)
        {
            if (String.IsNullOrEmpty(_apiKey))
            {
                status = (int)RoomManagementServiceStatus.Unauthorized;
                return null;
            }
            using (UnityWebRequest webRequest = new UnityWebRequest(uri, "POST"))
            {
                byte[] data = Encoding.UTF8.GetBytes(body);
                webRequest.uploadHandler = new UploadHandlerRaw(data);
                webRequest.uploadHandler.contentType = "application/json";
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader(_apiKeyHeader, _apiKey);

                var awaiter = webRequest.SendWebRequest();

                while (!awaiter.isDone)
                {
                    Thread.Sleep(1);
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    status = (int)webRequest.responseCode;
                    return webRequest.downloadHandler.text;
                }
                else
                    status = (int)webRequest.responseCode;

                return null;
            }
        }

        // Async Web request
        private struct AsyncRequestResponse
        {
            public string responseData;
            public int status;
        }
        private async Task<AsyncRequestResponse> SendWebRequestAsync(string uri, string body)
        {
            if (String.IsNullOrEmpty(_apiKey))
            {
                return new AsyncRequestResponse()
                {
                    responseData = "",
                    status = (int)RoomManagementServiceStatus.Unauthorized
                };
            }
            using (UnityWebRequest webRequest = new UnityWebRequest(uri, "POST"))
            {
                byte[] data = Encoding.UTF8.GetBytes(body);
                webRequest.uploadHandler = new UploadHandlerRaw(data);
                webRequest.uploadHandler.contentType = "application/json";
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader(_apiKeyHeader, _apiKey);

                await webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    return new AsyncRequestResponse()
                    {
                        responseData = webRequest.downloadHandler.text,
                        status = (int)webRequest.responseCode
                    };
                }
                else
                {
                    return new AsyncRequestResponse()
                    {
                        responseData = "",
                        status = (int)webRequest.responseCode
                    };

                }
            }
        }
    }
}
