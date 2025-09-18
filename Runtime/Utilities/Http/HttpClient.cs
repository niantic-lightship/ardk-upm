// Copyright 2022-2025 Niantic.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine.Networking;
using Niantic.Protobuf;
using Niantic.Protobuf.Reflection;

namespace Niantic.Lightship.AR.Utilities.Http
{
    /// <summary>
    /// HTTP client that handles [Serializable]/JSON compatible types, as well as Protobufs.
    /// Outgoing Protobuf messages can be handled with SendPostAsync due to direct serialization.
    /// Messages that expect Protobuf responses need to go through SendPostAsyncProto for
    ///   generic deserialization
    /// </summary>
    internal static class HttpClient
    {
        /// <summary>
        /// Send an async POST request with the specified Request/Response types
        /// A JsonUtility deserializer will be used to parse the output
        /// </summary>
        /// <param name="uri">URI to address the request to</param>
        /// <param name="request">Serializable request object</param>
        /// <param name="headers">Headers to send</param>
        /// <typeparam name="TRequest">Type of request for serialization</typeparam>
        /// <typeparam name="TResponse">Type of response for deserialization</typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">TResponse is a Protobuf</exception>
        /// <exception cref="ArgumentException">TRequest cannot be serialized</exception>
        internal static async Task<HttpResponse<TResponse>> SendPostAsync<TRequest, TResponse>
        (
            string uri,
            TRequest request,
            Dictionary<string, string> headers = null
        )
            where TRequest : class
            where TResponse : class
        {
            string jsonRequest;

            // If the response type is a proto, use SendRequestAsyncProto instead
            if (IsProtobufMessageType<TResponse>())
            {
                throw new InvalidOperationException("Protobuf response type is not supported, use SendRequestAsyncProto instead");
            }

            // Check if TRequest is a protobuf message (implements IMessage)
            if (request is IMessage protoRequest)
            {
                var formatter = new JsonFormatter(JsonFormatter.Settings.Default);
                jsonRequest = formatter.Format(protoRequest);
            }
            else
            {
                // Use Unity's JsonUtility for regular serializable types
                if (!typeof(TRequest).IsSerializable)
                {
                    throw new ArgumentException(typeof(TRequest) + " is not serializable.");
                }
                jsonRequest = JsonUtility.ToJson(request);
            }

            byte[] data = Encoding.UTF8.GetBytes(jsonRequest);

            return await SendRequestAsync<TResponse>(uri, data, "POST", "application/json", headers);
        }

        /// <summary>
        /// Send an async POST request with the specified Request/Response types
        /// TResponse must be a Protobuf type with default constructor exposed for deserialization
        ///   (this works with any protoc compiled protobuf).
        /// </summary>
        /// <param name="uri">URI to address the request to</param>
        /// <param name="request">Serializable request object</param>
        /// <param name="headers">Headers to send</param>
        /// <param name="contentType">Content type to request. Current supports json and octet-stream</param>
        /// <typeparam name="TRequest">Type of request for serialization</typeparam>
        /// <typeparam name="TResponse">Type of response for deserialization</typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException">TRequest cannot be serialized</exception>
        internal static async Task<HttpResponse<TResponse>> SendPostAsyncProto<TRequest, TResponse>(
            string uri,
            TRequest request,
            Dictionary<string, string> headers = null,
            string contentType = "application/json"
        )
            where TRequest : class
            where TResponse : class, IMessage<TResponse>, new()
        {
            string jsonRequest;

            // Check if TRequest is a protobuf message (implements IMessage)
            if (request is IMessage protoRequest)
            {
                var formatter = new JsonFormatter(JsonFormatter.Settings.Default);
                jsonRequest = formatter.Format(protoRequest);
            }
            else
            {
                // Use Unity's JsonUtility for regular serializable types
                if (!typeof(TRequest).IsSerializable)
                {
                    throw new ArgumentException(typeof(TRequest) + " is not serializable.");
                }
                jsonRequest = JsonUtility.ToJson(request);
            }

            byte[] data = Encoding.UTF8.GetBytes(jsonRequest);

            return await SendRequestAsyncProto<TResponse>(uri, data, "POST", contentType, headers);
        }

        // This method only works with [Serializable] TResponse
        internal static async Task<HttpResponse<TResponse>> SendPutAsync<TResponse>
        (
            string uri,
            byte[] body,
            Dictionary<string, string> headers = null
        )
        where TResponse : class
        {
            return await SendRequestAsync<TResponse>(uri, body, "PUT", "", headers);
        }

        internal static async Task<Texture> DownloadImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return null;
            }

            UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(imageUrl);
            webRequest.downloadHandler = new DownloadHandlerTexture();

            await webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Log.Warning("Image download failed: " + webRequest.error + "\nURL: " + imageUrl);
                return null;
            }

            return ((DownloadHandlerTexture)webRequest.downloadHandler).texture;
        }

        // Helper to process request/response for non-proto responses
        private static async Task<HttpResponse<TResponse>> SendRequestAsync<TResponse>
        (
            string uri,
            byte[] requestBody,
            string method,
            string contentType = "application/json",
            Dictionary<string, string> headers = null
        )
        where TResponse : class
        {
            using UnityWebRequest webRequest = new UnityWebRequest(uri, method);
            webRequest.uploadHandler = new UploadHandlerRaw(requestBody);
            webRequest.uploadHandler.contentType = contentType;
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }
            }

            await webRequest.SendWebRequest();

            TResponse response = null;

            // Hold this here, and set it to empty if deserialization succeeds so that we don't send
            //  parsed data twice
            var responseText = webRequest.downloadHandler.text;

            // Only attempt to parse response content when request was successful
            if (webRequest.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(responseText))
            {
                // Check if TResponse is a protobuf message type. This method isn't made to handle protobufs
                //   cleanly, but if we end up here, use reflection as a last resort.
                if (IsProtobufMessageType<TResponse>())
                {
                    try
                    {
                        var parser = new JsonParser(JsonParser.Settings.Default);
                        response = (TResponse)parser.Parse(responseText, GetProtobufMessageDescriptor<TResponse>());
                        responseText = String.Empty;
                    }
                    catch (Exception ex)
                    {
                        response = null;
                    }
                }
                else
                {
                    try
                    {
                        // Use Unity's JsonUtility for regular serializable types
                        response = JsonUtility.FromJson<TResponse>(responseText);
                        responseText = String.Empty;
                    }
                    catch (Exception ex)
                    {
                        // If JSON parsing fails, leave response as null
                        response = null;
                    }
                }
            }

            var status = ResponseStatusTranslator.FromHttpStatus(webRequest.result, webRequest.responseCode);
            return new HttpResponse<TResponse>(status, response, webRequest.responseCode, responseText);
        }

        // Helper to process request/response for proto responses
        private static async Task<HttpResponse<TResponse>> SendRequestAsyncProto<TResponse>
        (
            string uri,
            byte[] requestBody,
            string method,
            string contentType = "application/json",
            Dictionary<string, string> headers = null
        )
            where TResponse : class, IMessage<TResponse>, new()
        {
            using UnityWebRequest webRequest = new UnityWebRequest(uri, method);
            webRequest.uploadHandler = new UploadHandlerRaw(requestBody);
            webRequest.uploadHandler.contentType = contentType;
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }
            }

            await webRequest.SendWebRequest();

            TResponse response = null;

            // Hold this here, and set it to empty if deserialization succeeds so that we don't send
            //  parsed data twice
            var responseText = webRequest.downloadHandler.text;

            // Only attempt to parse response content when request was successful
            if (webRequest.result == UnityWebRequest.Result.Success &&
                (!string.IsNullOrEmpty(responseText) ||
                 (webRequest.downloadHandler.data != null && webRequest.downloadHandler.data.Length > 0)))
            {
                try
                {
                    var parser = new MessageParser<TResponse>(() => new TResponse());
                    if (contentType.Equals("application/json"))
                    {
                        response = parser.ParseJson(responseText);
                        responseText = String.Empty;
                    }
                    else if (contentType.Equals("application/octet-stream"))
                    {
                        response = parser.ParseFrom(webRequest.downloadHandler.data);
                        responseText = String.Empty;
                    }
                    else
                    {
                        Debug.LogError($"Unknown content type: {contentType}, handling as bytes");
                        response = parser.ParseFrom(webRequest.downloadHandler.data);
                        responseText = String.Empty;
                    }
                }
                catch (Exception ex)
                {
                    response = null;
                }
            }

            var status = ResponseStatusTranslator.FromHttpStatus(webRequest.result, webRequest.responseCode);
            return new HttpResponse<TResponse>(status, response, webRequest.responseCode, responseText);
        }

        // Checks if a type is a protobuf. Useful when working with generics with no object access.
        private static bool IsProtobufMessageType<T>()
        {
            return typeof(IMessage).IsAssignableFrom(typeof(T));
        }

        // Use reflection to try to grab the proto descriptor from a type.
        private static MessageDescriptor GetProtobufMessageDescriptor<T>()
        {
            var type = typeof(T);
            var descriptorProperty = type.GetProperty("Descriptor",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);

            if (descriptorProperty != null)
            {
                var prop = descriptorProperty.GetValue(null);
                if (prop is MessageDescriptor descriptor)
                {
                    return descriptor;
                }
            }

            throw new InvalidOperationException($"Unable to get protobuf descriptor for type {type.Name}");
        }
    }

    internal class HttpResponse<TResponse>
    {
        public ResponseStatus Status { get; set; }
        public TResponse Data { get; }

        /// <summary>
        /// Raw response text from the server, available when Data parsing fails
        /// This will be set to String.Empty on successful parsing into Data
        /// </summary>
        public string RawText { get; }

        public long HttpStatusCode { get; }

        public HttpResponse(ResponseStatus status, TResponse data, long httpStatusCode, string rawText = null)
        {
            Status = status;
            Data = data;
            HttpStatusCode = httpStatusCode;
            RawText = rawText;
        }
    }

    #region Custom Awaiter for SendWebRequest()

    internal class UnityWebRequestAwaiter : INotifyCompletion
    {
        private readonly UnityWebRequestAsyncOperation asyncOp;
        private Action _continuation;

        public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp)
        {
            this.asyncOp = asyncOp;
            asyncOp.completed += OnRequestCompleted;
        }

        public bool IsCompleted
        {
            get { return asyncOp.isDone; }
        }

        public void GetResult()
        {
        }

        public void OnCompleted(Action continuation)
        {
            this._continuation = continuation;
        }

        private void OnRequestCompleted(AsyncOperation obj)
        {
            _continuation();
        }
    }

    internal static class UnityWebRequestAwaiterExtensions
    {
        public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
        {
            return new UnityWebRequestAwaiter(asyncOp);
        }
    }

    #endregion
}
