// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine.Networking;

namespace Niantic.Lightship.AR.VpsCoverage
{
    internal static class HttpClient
    {
        // This method only works with [Serializable] TRequest and TResponse
        internal static async Task<HttpResponse<TResponse>> SendPostAsync<TRequest, TResponse>
        (
            string uri,
            TRequest request,
            Dictionary<string, string> headers = null
        )
            where TRequest : class
            where TResponse : class
        {
            if (!typeof(TRequest).IsSerializable)
            {
                throw new ArgumentException(typeof(TRequest) + " is not serializable.");
            }

            string jsonRequest = JsonUtility.ToJson(request);

            using (UnityWebRequest webRequest = new UnityWebRequest(uri, "POST"))
            {
                byte[] data = Encoding.UTF8.GetBytes(jsonRequest);
                webRequest.uploadHandler = new UploadHandlerRaw(data);
                webRequest.uploadHandler.contentType = "application/json";
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                if (headers != null)
                    foreach (var header in headers)
                        webRequest.SetRequestHeader(header.Key, header.Value);

                await webRequest.SendWebRequest();

                TResponse response;

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    response = JsonUtility.FromJson<TResponse>(webRequest.downloadHandler.text);
                }
                else
                {
                    response = default;
                }

                return new HttpResponse<TResponse>
                (ResponseStatusTranslator.FromHttpStatus(webRequest.result, webRequest.responseCode), response,
                    webRequest.responseCode);
            }
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
    }

    internal class HttpResponse<TResponse>
    {
        public ResponseStatus Status { get; set; }
        public TResponse Data { get; }

        public long HttpStatusCode { get; }

        public HttpResponse(ResponseStatus status, TResponse data, long httpStatusCode)
        {
            Status = status;
            Data = data;
            HttpStatusCode = httpStatusCode;
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
