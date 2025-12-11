
using System;

namespace Niantic.Lightship.AR.Utilities.Auth
{
    /// <summary>
    /// Static class for auth-related utility functions that are shared as part of the public API
    /// </summary>
    public static class AuthPublicUtils
    {
        /// <summary>
        /// Get the expiry time of a JWT token in seconds.
        /// </summary>
        /// <param name="token">the token</param>
        /// <returns>the expiry time or 0 if the token cannot be parsed</returns>
        public static int ExpiresAt(string token)
        {
            return AuthGatewayUtils.Instance.DecodeJwtTokenBody(token)?.exp ?? 0;
        }

        /// <summary>
        /// Is the token expired, about to expire, or not set?
        /// </summary>
        /// <param name="token">the token</param>
        /// <param name="minTimeLeftSeconds">minimum time left to qualify as not expiring</param>
        /// <returns>true if empty, expired, or expiring</returns>
        public static bool IsEmptyOrExpiring(string token, int minTimeLeftSeconds)
        {
            if (string.IsNullOrEmpty(token))
            {
                return true;
            }

            var expiresAt = ExpiresAt(token);
            if (expiresAt <= 0)
            {
                return true;
            }

            var currentTimeSeconds = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeLeft = expiresAt - currentTimeSeconds;
            return timeLeft <= minTimeLeftSeconds;
        }
    }
}
