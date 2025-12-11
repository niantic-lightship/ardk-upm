using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Niantic.Lightship.AR.Utilities.Http
{
    /// <summary>
    /// Interface for the HttpListener class. This exists so that instances of HttpListener can be mocked in tests via
    /// HttpListenerFactory. Only methods and properties that are currently used are implemented.
    /// </summary>
    internal interface IHttpListener
    {
        ICollection<string> Prefixes { get; }

        void Start();
        void Stop();
        void Close();
        Task<IHttpListenerContext> GetContextAsync();
    }

    /// <summary>
    /// Interface for the IHttpListenerContext class. This exists so that instances of IHttpListenerContext can be
    /// mocked in tests via IHttpListener. Only properties that are currently used are implemented.
    /// </summary>
    internal interface IHttpListenerContext
    {
        IHttpListenerRequest Request { get; }
        IHttpListenerResponse Response { get; }
    }

    /// <summary>
    /// Interface for the IHttpListenerRequest class. This exists so that instances of IHttpListenerRequest can be
    /// mocked in tests via IHttpListenerContext. Only properties that are currently used are implemented.
    /// </summary>
    internal interface IHttpListenerRequest
    {
        Uri Url { get; }
        string RawUrl { get; }
    }

    /// <summary>
    /// Interface for the IHttpListenerResponse class. This exists so that instances of IHttpListenerResponse can be
    /// mocked in tests via IHttpListenerContext. Only methods and properties that are currently used are implemented.
    /// </summary>
    internal interface IHttpListenerResponse
    {
        WebHeaderCollection Headers { get; }
        int StatusCode { set; }
        string ContentType { set; }
        System.Text.Encoding ContentEncoding { set; }
        long ContentLength64 { set; }
        System.IO.Stream OutputStream { get; }
        void Close();
    }

    /// <summary>
    /// Delegate for creating an instance of IHttpListener.
    /// </summary>
    delegate IHttpListener HttpListenerFactory();

    /// <summary>
    /// Class that wraps calls to HttpListener, along with child objects such as IHttpListenerContext,
    /// HttpListenerRequest and HttpListenerResponse. Wrapping this API allows us to mock it in tests.
    /// An instance of this class is created by the static method Create(), which corresponds to the signature
    /// of HttpListenerFactory.
    /// </summary>
    internal class HttpListenerWrapper : IHttpListener
    {
        private readonly HttpListener _listener = new();

        private HttpListenerWrapper()
        {
        }

        public static IHttpListener Create()
        {
            return new HttpListenerWrapper();
        }

        public ICollection<string> Prefixes => _listener.Prefixes;

        public void Start()
        {
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
        }

        public async Task<IHttpListenerContext> GetContextAsync()
        {
            var context = await _listener.GetContextAsync();
            return new HttpListenerContextWrapper(context);
        }

        public void Close()
        {
            _listener.Close();
        }

        private class HttpListenerContextWrapper : IHttpListenerContext
        {
            private readonly HttpListenerRequestWrapper _request;
            private readonly HttpListenerResponseWrapper _response;

            public HttpListenerContextWrapper(HttpListenerContext context)
            {
                _request = new HttpListenerRequestWrapper(context.Request);
                _response = new HttpListenerResponseWrapper(context.Response);
            }

            public IHttpListenerRequest Request => _request;
            public IHttpListenerResponse Response => _response;

            private class HttpListenerRequestWrapper : IHttpListenerRequest
            {
                private readonly HttpListenerRequest _request;

                public HttpListenerRequestWrapper(HttpListenerRequest request)
                {
                    _request = request;
                }

                public Uri Url => _request.Url;
                public string RawUrl => _request.RawUrl;
            }

            private class HttpListenerResponseWrapper : IHttpListenerResponse
            {
                private readonly HttpListenerResponse _response;

                public HttpListenerResponseWrapper(HttpListenerResponse response)
                {
                    _response = response;
                }

                public WebHeaderCollection Headers => _response.Headers;
                public int StatusCode { set => _response.StatusCode = value; }
                public string ContentType { set => _response.ContentType = value; }
                public Encoding ContentEncoding { set => _response.ContentEncoding = value; }
                public long ContentLength64 { set => _response.ContentLength64 = value; }
                public Stream OutputStream => _response.OutputStream;
                public void Close()
                {
                    _response.Close();
                }
            }
        }
    }
}
