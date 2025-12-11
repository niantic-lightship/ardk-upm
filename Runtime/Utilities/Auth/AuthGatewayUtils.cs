using System;
using System.Text;
using Niantic.Lightship.AR.Auth;
using Niantic.Lightship.AR.Utilities.Http;
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities.Auth
{
    /// <summary>
    /// Interface to utility functions related to auth work
    /// </summary>
    internal interface IAuthGatewayUtils
    {
        /// <summary>
        /// Build the authorization header for an API call from the current settings
        /// </summary>
        /// <param name="settings">the current auth settings</param>
        /// <returns>the authorization header string</returns>
        string BuildAuthorizationHeader(IAuthSettings settings);

        /// <summary>
        /// Check an http response for errors and log them if found.
        /// </summary>
        /// <param name="response">the response (sans data)</param>
        /// <param name="context">human-readable context string (for messaging in the log)</param>
        void LogAnyError(HttpResponseBase response, string context, string inputToken);

        /// <summary>
        /// Log the current auth settings
        /// </summary>
        /// <param name="settings">the settings to log</param>
        /// <param name="context">the context</param>
        void LogSettings(IAuthSettings settings, string context);

        /// <summary>
        /// Log details for a single Jwt token.
        /// Details are extracted from the token itself, not from the settings.
        /// </summary>
        /// <param name="context">Context for what the token is and why we're logging it</param>
        /// <param name="token">the token string</param>
        void LogToken(string context, string token);

        /// <summary>
        /// Check if the given time is close to the token expiration time.
        /// </summary>
        /// <param name="accessExpiresAt">access expiry time (an integer, as it is stored in the tokens)</param>
        /// <param name="nowUtc">the current time, as a DateTime</param>
        /// <returns>true if the token is close to expiring</returns>
        bool IsAccessCloseToExpiration(int accessExpiresAt, DateTime nowUtc);

        /// <summary>
        /// Has the given access time expired?
        /// </summary>
        /// <param name="accessExpiresAt">the access expiry time</param>
        /// <param name="nowUtc">the current time, as a DateTime</param>
        /// <returns>true if expiry time is in the past</returns>
        bool IsAccessExpired(int accessExpiresAt, DateTime nowUtc);

        /// <summary>
        /// Decode the body of a JWT token.
        /// </summary>
        /// <param name="token">the jwt token, as an encoded string</param>
        /// <returns>the token body decoded and deserialised</returns>
        AuthGatewayUtils.JwtTokenBody DecodeJwtTokenBody(string token);

        /// <summary>
        /// URI Parameters need to be encoded to handle certain characters (e.g. spaces).
        /// This function decodes them.
        /// </summary>
        /// <param name="uriEncodedString">the string encoded to be part of a URI</param>
        /// <returns>the decoded string</returns>
        string DecodeUriParameter(string uriEncodedString);

        /// <summary>
        /// URI Parameters need to be encoded to handle certain characters (e.g. spaces).
        /// This function encodes them.
        /// </summary>
        /// <param name="parameter">the unencoded parameter</param>
        /// <returns>the string encoded to be part of a URI</returns>
        string EncodeUriParameter(string parameter);
    }

    internal class AuthGatewayUtils : IAuthGatewayUtils
    {
        // Time in seconds before token expiration that we should refresh (it's best not to wait until the token
        // expires). Arbitrarily set to 60 seconds.
        public const int MinTokenTimeLeft = 60;

        // Disable warnings about naming rules as the following serialized fields are named for server fields
        // ReSharper disable InconsistentNaming

        // Response from the refresh endpoint when there is an error
        [Serializable]
        internal class ErrorResponse
        {
            public string error;
        }

        /// <summary>
        /// The decoded body of a JWT token.
        /// Add fields as needed (currently only expiry is used).
        /// </summary>
        [Serializable]
        public class JwtTokenBody
        {
            public int exp;
        }

        // ReSharper restore InconsistentNaming

        private AuthGatewayUtils()
        {
        }

        /// <summary>
        /// Create() function for testing (allows mocking of dependencies)
        /// </summary>
        public static IAuthGatewayUtils Create()
        {
            return new AuthGatewayUtils();
        }

        /// <summary>
        /// Singleton for runtime use
        /// </summary>
        public static IAuthGatewayUtils Instance { get; } = AuthGatewayUtils.Create();

        /// <summary>
        /// Convert unix time (as an integer) to a UTC Datetime
        /// </summary>
        /// <param name="secondsSince1970">unix time as an integer</param>
        public static DateTime ConvertToUtc(int secondsSince1970)
        {
            return DateTimeOffset.FromUnixTimeSeconds(secondsSince1970).UtcDateTime;
        }

        public string BuildAuthorizationHeader(IAuthSettings settings)
        {
            // There are two ways to authenticate with the API Gateway: access token and api key (eventually obsolete).
            // If we have an access token, use it.
            if (!string.IsNullOrEmpty(settings.AccessToken))
            {
                return $"Bearer {settings.AccessToken}";
            }

            return settings.ApiKey;
        }

        public void LogAnyError(HttpResponseBase response, string context, string inputToken)
        {
            if (response.Status != ResponseStatus.Success)
            {
                // If the API call fails then any error from the server is returned as RawText:
                var errorResponse = HttpUtility.ParseResponse<ErrorResponse>(response.RawText);
                var msg = errorResponse?.error ?? response.Status.ToString();
                Debug.LogError($"[Auth] {context} failed: {msg}, token: {GetTokenShortName(inputToken)}");
            }
        }

        public void LogSettings(IAuthSettings settings, string context)
        {
#if NIANTIC_ARDK_AUTH_DEBUG
            var currentTimeSeconds = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var accessTail = GetTokenShortName(settings.AccessToken);
            var refreshTail = GetTokenShortName(settings.RefreshToken);
            string accessTimeLeft = GetTimeLeftString(settings.AccessExpiresAt, currentTimeSeconds);
            string refreshTimeLeft = GetTimeLeftString(settings.RefreshExpiresAt, currentTimeSeconds);
            Debug.Log(
                $"[Auth] {context} access: {accessTail}, timeLeft: {accessTimeLeft}. Refresh: {refreshTail}, refresh timeLeft: {refreshTimeLeft}.");
#endif
        }

        public void LogToken(string context, string token)
        {
#if NIANTIC_ARDK_AUTH_DEBUG
            var accessTail = GetTokenShortName(token);
            var expiresAt = DecodeJwtTokenBody(token)?.exp ?? 0;
            var currentTimeSeconds = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string accessTimeLeft = GetTimeLeftString(expiresAt, currentTimeSeconds);
            Debug.Log($"[Auth] {context} token: {accessTail}, timeLeft: {accessTimeLeft}.");
#endif
        }

        public bool IsAccessCloseToExpiration(int accessExpiresAt, DateTime nowUtc)
        {
            // Always fail if no expiry time is set.
            if (accessExpiresAt <= 0)
            {
                return false;
            }

            // Get the expiration date in UTC.
            var expiryUtc = ConvertToUtc(accessExpiresAt);

            return expiryUtc - nowUtc < TimeSpan.FromSeconds(MinTokenTimeLeft);
        }

        public bool IsAccessExpired(int accessExpiresAt, DateTime nowUtc)
        {
            return ConvertToUtc(accessExpiresAt) < nowUtc;
        }

        public JwtTokenBody DecodeJwtTokenBody(string token)
        {
            // If the token is null or empty, quit early:
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            // Split the token into its parts: Header, body, and signature. We are only interested in the body (the
            // 2nd part)
            var tokenParts = token.Split('.');
            var body = DecodeBase64Url(tokenParts[1]);

            return JsonUtility.FromJson<JwtTokenBody>(body);
        }

        public string DecodeUriParameter(string uriEncodedString)
        {
            return Uri.UnescapeDataString(uriEncodedString);
        }

        public string EncodeUriParameter(string parameter)
        {
            return Uri.EscapeDataString(parameter);
        }


        /// <summary>
        /// Build a shortened version of a token for logging.
        /// The front of the token generally never changes, whilst the back tends to be unique so return the last
        /// 4 characters.
        /// </summary>
        /// <param name="token">the token in full</param>
        /// <returns>Last four characters of the token or (none)</returns>
        public static string GetTokenShortName(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return "(none)";
            }

            // Arbitrary length so we don't truncate unit test tokens (real tokens are always 100+ chars)
            return token.Length < 100 ? token : token[^4..];
        }

        /// <summary>
        /// Decodes a Base64Url encoded string.
        /// </summary>
        /// <param name="base64Url">The Base64Url encoded string.</param>
        /// <returns>The decoded string.</returns>
        private static string DecodeBase64Url(string base64Url)
        {
            // Base64Url is a URL-safe version of Base64.
            // It replaces '+' with '-' and '/' with '_'.
            // It also removes any padding '=' characters.
            string base64 = base64Url.Replace('-', '+').Replace('_', '/');

            // Add padding back if necessary.
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string GetTimeLeftString(int expiresAt, int currentTimeSeconds)
        {
            return expiresAt > currentTimeSeconds ? (expiresAt - currentTimeSeconds).ToString() : "n/a";
        }
    }
}
