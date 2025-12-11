
namespace Niantic.Lightship.AR.Auth
{
    /// <summary>
    /// A central place for all auth constants that are shared across runtime and editor.
    /// </summary>
    internal static class AuthConstants
    {
        /// <summary>
        /// Method for all identity calls
        /// </summary>
        public const string IdentityMethod = "oauth/token";

        /// <summary>
        /// Name of the cookie we use when we send the current refresh token for refresh (for both editor and runtime)
        /// </summary>
        public const string RefreshTokenCookieName = "refresh_token";
    }
}
