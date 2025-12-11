using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Auth;
using Niantic.Lightship.AR.Utilities.Auth;
using Niantic.Lightship.AR.Utilities.Http;
using UnityEngine.Networking;

namespace Niantic.Lightship.AR.Editor.Auth
{
    /// <summary>
    /// Interface to AuthGatewayAccess class for test mocking
    /// </summary>
    internal interface IAuthEditorGatewayAccess
    {
        /// <summary>
        /// Call to refresh the current access token.
        /// On success, the current refresh token is also updated.
        /// </summary>
        /// <param name="editorRefreshToken">the current refresh token</param>
        /// <returns>updated access and refresh tokens</returns>
        Task<AuthGatewayTokens> RefreshEditorAccessAsync(string editorRefreshToken);

        /// <summary>
        /// Call to request a runtime refresh token.
        /// Its purpose is to provide a unique runtime refresh token for every build or play mode session.
        /// Note: This is a first-time request, creating a new refresh token (not refreshing an existing one).
        /// An editor refresh token is required to make this request (it is used as an access token here).
        /// </summary>
        /// <param name="editorRefreshToken">a valid editor refresh token</param>
        /// <returns>a value runtime refresh token</returns>
        Task<AuthRuntimeRefreshToken> RequestRuntimeRefreshTokenAsync(string editorRefreshToken);
    }

    internal class AuthEditorGatewayAccess : IAuthEditorGatewayAccess
    {
        // constants public for test mocking (should not otherwise be used outside of this class)
        public const string GrantTypeRefreshUserSession = "refresh_user_session_access_token";
        public const string GrantTypeExchangeRefresh = "exchange_build_refresh_token";

        public const string RefreshEditorAccessContext = "Refresh of editor access";
        public const string RequestRuntimeRefreshTokenContext = "Request of runtime refresh token";

        // Disable warnings about naming rules as the following serialized fields are named for server fields
        // ReSharper disable InconsistentNaming

        // All identity requests hit the same end-point (grantType indicates the type of request)
        [Serializable]
        public class Request
        {
            public string grantType;
        }

        // ReSharper restore InconsistentNaming

        // Dependencies:
        private readonly IAuthGatewayUtils _utils;
        private readonly IAuthEditorSettings _editorSettings;
        private readonly IAuthEnvironment _environment;

        /// <summary>
        /// Constructor is private as this is a singleton
        /// </summary>
        private AuthEditorGatewayAccess(
            IAuthGatewayUtils utils, IAuthEditorSettings editorSettings, IAuthEnvironment environment)
        {
            _utils = utils;
            _editorSettings = editorSettings;
            _environment = environment;
        }

        /// <summary>
        /// Create() function for testing (allows mocking of dependencies)
        /// </summary>
        public static AuthEditorGatewayAccess Create(
            IAuthGatewayUtils utils, IAuthEditorSettings editorSettings, IAuthEnvironment environment)
        {
            return new AuthEditorGatewayAccess(utils, editorSettings, environment);
        }

        /// <summary>
        /// Singleton instance of this class
        /// </summary>
        public static IAuthEditorGatewayAccess Instance { get; } = Create(
            AuthGatewayUtils.Instance, AuthEditorSettings.Instance, AuthEnvironment.Instance);

        public async Task<AuthGatewayTokens> RefreshEditorAccessAsync(string editorRefreshToken)
        {
            var headers = new Dictionary<string, string>
            {
                { "Cookie", $"{AuthConstants.RefreshTokenCookieName}={editorRefreshToken}" }
            };

            // Clear any cached cookies associated with this endpoint. Sometimes UnityWebRequest seems to get stuck and
            // return the cached cookie rather than the new one (we never want that).
            UnityWebRequest.ClearCookieCache(new Uri(GetUrl()));

            var request = new Request { grantType = GrantTypeRefreshUserSession};

            var result = await HttpClient.SendPostAsync<Request, AuthGatewayAccess.RefreshAccessResponse>(
                GetUrl(), request, headers, new [] { "set-cookie" });

            if (result.Status == ResponseStatus.Success)
            {
                result.Headers.TryGetValue("set-cookie", out var cookieHeader);
                var newRefreshToken = HttpUtility.GetHeaderValue(cookieHeader, AuthConstants.RefreshTokenCookieName);
                return new AuthGatewayTokens
                {
                    AccessToken = result.Data.token,
                    RefreshToken = newRefreshToken,
                    AccessExpiresAt = result.Data.expiresAt
                };
            }

            _utils.LogAnyError(result, RefreshEditorAccessContext, editorRefreshToken);

            // On failure, return an empty struct
            return new AuthGatewayTokens();
        }

        public async Task<AuthRuntimeRefreshToken> RequestRuntimeRefreshTokenAsync(string editorRefreshToken)
        {
            var headers = new Dictionary<string, string>
            {
                { "Cookie", $"{AuthConstants.RefreshTokenCookieName}={editorRefreshToken}" }
            };

            var request = new Request { grantType = GrantTypeExchangeRefresh};

            var result = await HttpClient.SendPostAsync<Request, AuthGatewayAccess.RequestRuntimeRefreshTokenResponse>(
                GetUrl(), request, headers);

            if (result.Status == ResponseStatus.Success)
            {
                return new AuthRuntimeRefreshToken
                {
                    RefreshToken = result.Data.buildRefreshToken,
                    ExpiresAt = result.Data.expiresAt
                };
            }

            _utils.LogAnyError(result, RequestRuntimeRefreshTokenContext, editorRefreshToken);

            return new AuthRuntimeRefreshToken();
        }

        private string GetUrl()
        {
            return _environment.GetIdentityEndpoint(_editorSettings.AuthEnvironment);
        }
    }
}
