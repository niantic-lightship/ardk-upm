// Copyright 2022 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Niantic.Lightship.AR
{
    internal static class _HttpClient
    {
        // This method only works with [Serializable] TRequest and TResponse
        internal static async Task<_HttpResponse<TResponse>> SendPostAsync<TRequest, TResponse>
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
                    response = JsonUtility.FromJson<TResponse>(webRequest.downloadHandler.text);
                else
                    response = default;

                return new _HttpResponse<TResponse>
                (_ResponseStatusTranslator.FromHttpStatus(webRequest.result, webRequest.responseCode), response,
                    webRequest.responseCode);
            }
        }

        internal static async Task<Texture> DownloadImageAsync(string imageUrl)
        {
            UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(imageUrl);
            webRequest.downloadHandler = new DownloadHandlerTexture();
            await webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Image download failed: " + webRequest.error + "\nURL: " + imageUrl);
                return null;
            }

            return ((DownloadHandlerTexture)webRequest.downloadHandler).texture;
        }
    }

    internal class _HttpResponse<TResponse>
    {
        public ResponseStatus Status { get; set; }
        public TResponse Data { get; }

        public long HttpStatusCode { get; }

        public _HttpResponse(ResponseStatus status, TResponse data, long httpStatusCode)
        {
            Status = status;
            Data = data;
            HttpStatusCode = httpStatusCode;
        }
    }

    #region Custom Awaiter for SendWebRequest()

    public class UnityWebRequestAwaiter : INotifyCompletion
    {
        private UnityWebRequestAsyncOperation asyncOp;
        private Action continuation;

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
            this.continuation = continuation;
        }

        private void OnRequestCompleted(AsyncOperation obj)
        {
            continuation();
        }
    }

    public static class ExtensionMethods
    {
        public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
        {
            return new UnityWebRequestAwaiter(asyncOp);
        }
    }

    #endregion
}
