using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Auth;
using Niantic.Lightship.AR.Utilities.Auth;
using Niantic.Lightship.AR.Utilities.Http;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor.Auth
{
    /// <summary>
    /// Command that triggers login and handles the redirect.
    /// </summary>
    internal interface IAuthEditorLoginCommand
    {
        /// <summary>
        /// The port to listen on when login is in progress (defaults to 8080)
        /// </summary>
        int RedirectPort { set; }

        /// <summary>
        /// Timeout in seconds. Defaults to 30 seconds.
        /// </summary>
        int TimeoutSeconds { set; }

        /// <summary>
        /// Check for whether the command is already in progress.
        /// </summary>
        bool InProgress { get; }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="cancellationToken">token to cancel the command</param>
        /// <returns>task to await for when it finishes</returns>
        Task ExecuteAsync(CancellationToken cancellationToken);
    }

    internal class AuthEditorLoginCommand : IAuthEditorLoginCommand
    {
        public const string LoginUrl = "{0}/signin?redirectType=unity-editor&port={1}";
        public const string RedirectUri = "http://127.0.0.1:{0}/";
        public const string SuccessMethod = "login/success";
        public const string FailureMethod = "login/failure";

        public int TimeoutSeconds { set; private get; } = 300;

        public int RedirectPort { set; private get; } = 8080;

        public bool InProgress {  get; private set; }

        // Dependencies
        private readonly IAuthEditorGatewayAccess _gatewayAccess;
        private readonly IAuthEditorSettings _editorSettings;
        private readonly IAuthSettings _settings;
        private readonly IOpenUrlCommand _openUrlCommand;
        private readonly HttpListenerFactory _listenerFactory;
        private readonly IAuthGatewayUtils _utils;

        /// <summary>
        /// Constructor is private to control instantiation
        /// </summary>
        private AuthEditorLoginCommand(
            IAuthEditorSettings editorSettings, IAuthSettings settings, IAuthEditorGatewayAccess gatewayAccess,
            IOpenUrlCommand openUrlCommand, HttpListenerFactory listenerFactory, IAuthGatewayUtils utils)
        {
            _editorSettings = editorSettings;
            _settings = settings;
            _gatewayAccess = gatewayAccess;
            _openUrlCommand = openUrlCommand;
            _listenerFactory = listenerFactory;
            _utils = utils;
        }

        /// <summary>
        /// Create() function for testing
        /// </summary>
        public static IAuthEditorLoginCommand Create(
            IAuthEditorSettings editorSettings, IAuthSettings settings, IAuthEditorGatewayAccess gatewayAccess,
            IOpenUrlCommand openUrlCommand, HttpListenerFactory listenerFactory, IAuthGatewayUtils utils)
        {
            return new AuthEditorLoginCommand(
                editorSettings, settings, gatewayAccess, openUrlCommand, listenerFactory, utils);
        }

        /// <summary>
        /// Singleton of this class for runtime use
        /// </summary>
        public static IAuthEditorLoginCommand Instance { get; } =
            new AuthEditorLoginCommand(
                AuthEditorSettings.Instance, AuthEditorBuildSettings.Instance, AuthEditorGatewayAccess.Instance,
                OpenURLCommand.Instance, HttpListenerWrapper.Create, AuthGatewayUtils.Instance);

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (InProgress)
            {
                Debug.LogError("Auth Editor Login command is already in progress");
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            InProgress = true;
            var httpListener = _listenerFactory();

            try
            {
                httpListener.Prefixes.Add(string.Format(RedirectUri, RedirectPort));
                httpListener.Start();

                var website = AuthEnvironment.GetSampleAppWebsite(_settings.AuthEnvironment);

                // Open the Login page ...
                _openUrlCommand.Execute(string.Format(LoginUrl, website, RedirectPort));

                var listenerTask = httpListener.GetContextAsync();

                // Wait for completion or time-out
                if (await Task.WhenAny(listenerTask, MakeTimeoutTask(cancellationToken)) == listenerTask)
                {
                    var context = await listenerTask;

                    var wasSuccess = false;
                    if (context.Request.Url.AbsolutePath.EndsWith(SuccessMethod))
                    {
                        var uriEncodedToken = HttpUtility.GetHeaderValue(context.Request.RawUrl, "RefreshToken");
                        var newRefreshToken = _utils.DecodeUriParameter(uriEncodedToken);
#if NIANTIC_ARDK_AUTH_DEBUG
                        var shortName = AuthGatewayUtils.GetTokenShortName(newRefreshToken);
                        Debug.Log($"[Auth] Received new refresh token: {shortName}");
#endif
                        var results = await _gatewayAccess.RefreshEditorAccessAsync(newRefreshToken);
                        if (!string.IsNullOrEmpty(results.AccessToken) && !cancellationToken.IsCancellationRequested)
                        {
                            var refreshExpiresAt = _utils.DecodeJwtTokenBody(results.RefreshToken).exp;
                            _editorSettings.UpdateEditorAccess(
                                results.AccessToken, results.AccessExpiresAt, results.RefreshToken, refreshExpiresAt);

                            // Generate a refresh token for the runtime
                            var runtimeRefresh =
                                await _gatewayAccess.RequestRuntimeRefreshTokenAsync(results.RefreshToken);

                            if (!string.IsNullOrEmpty(runtimeRefresh.RefreshToken))
                            {
                                _settings.UpdateAccess(
                                    string.Empty, 0, runtimeRefresh.RefreshToken, runtimeRefresh.ExpiresAt);
                                wasSuccess = true;
                            }
                        }
                    }

                    SendResponse(context.Response, wasSuccess, website);
                }
            }
            catch (OperationCanceledException)
            {
                // Catch any cancellation exceptions thrown
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occurred: {ex.Message}");
            }
            finally
            {
                httpListener.Stop();
                InProgress = false;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // Clean up any tokens that may've been set
                _editorSettings.UpdateEditorAccess(string.Empty, 0, string.Empty, 0);
                _settings.UpdateAccess(string.Empty, 0, string.Empty, 0);
            }
        }

        private Task MakeTimeoutTask(CancellationToken cancellationToken)
        {
            return Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds), cancellationToken);
        }

        private static void SendResponse(IHttpListenerResponse response, bool wasSuccess, string website)
        {
            // --- CORS Handling ---

            // 1. Allow requests from any origin
            response.Headers.Add("Access-Control-Allow-Origin", website);

            // 2. Allow common methods
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");

            // 3. Allow common headers
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");

            // 4. Allow cookies and Authorization headers:
            response.Headers.Add("Access-Control-Allow-Credentials", "true");

            response.StatusCode = (int) HttpStatusCode.OK;

            // Get the HTML content
            string responseString = wasSuccess
                ? AuthEditorLoginSuccessfulPage.HtmlContent
                : AuthEditorLoginFailedPage.HtmlContent;

            // Convert the HTML string to a byte array.
            var buffer = Encoding.UTF8.GetBytes(responseString);

            // Set the response headers.
            response.ContentType = "text/html";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;

            // Write the HTML content to the response stream.
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }
    }
}
