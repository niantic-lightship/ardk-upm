using System;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Utilities.Auth;

namespace Niantic.Lightship.AR.Auth
{
    internal interface IAuthRuntimeSettingsUpdater
    {
        /// <summary>
        /// Refresh runtime access.
        /// Can be called either at runtime or from the editor (RuntimeLightshipSettings or LightshipSettings)
        /// </summary>
        /// <param name="settings">runtime auth setttings</param>
        /// <returns>task can be awaited until complete</returns>
        Task RefreshRuntimeAccessAsync(IAuthSettings settings);

        /// <summary>
        /// Refresh runtime access token data if expiring soon (or expired)
        /// Can be called either at runtime or from the editor (RuntimeLightshipSettings or LightshipSettings)
        /// </summary>
        /// <param name="settings">runtime auth setttings</param>
        /// <param name="nowUtc">the current time</param>
        /// <returns>task can be awaited until complete</returns>
        Task RefreshRuntimeAccessIfExpiringAsync(IAuthSettings settings, DateTime nowUtc);
    }

    internal class AuthRuntimeSettingsUpdater : IAuthRuntimeSettingsUpdater
    {
        private readonly IAuthGatewayAccess _gatewayAccess;
        private readonly IAuthGatewayUtils _utils;
        private readonly IAuthRuntimeSettingsStore _settingsStore;

        private AuthRuntimeSettingsUpdater(
            IAuthGatewayAccess gatewayAccess, IAuthGatewayUtils utils, IAuthRuntimeSettingsStore settingsStore)
        {
            _gatewayAccess = gatewayAccess;
            _utils = utils;
            _settingsStore = settingsStore;
        }

        /// <summary>
        /// Create() function for testing (allows mocking of dependencies)
        /// </summary>
        public static IAuthRuntimeSettingsUpdater Create(
            IAuthGatewayAccess gatewayAccess, IAuthGatewayUtils utils, IAuthRuntimeSettingsStore settingsStore)
        {
            return new AuthRuntimeSettingsUpdater(gatewayAccess, utils, settingsStore);
        }

        /// <summary>
        /// Singleton for runtime use
        /// </summary>
        public static IAuthRuntimeSettingsUpdater Instance { get; } =
            new AuthRuntimeSettingsUpdater(
                AuthGatewayAccess.Instance, AuthGatewayUtils.Instance, AuthRuntimeSettingsStore.Instance);

        public async Task RefreshRuntimeAccessAsync(IAuthSettings settings)
        {
            var results = await _gatewayAccess.RefreshRuntimeAccessAsync(settings.RefreshToken);
            if (!string.IsNullOrEmpty(results.AccessToken))
            {
                // If the refresh token has changed, then update the refresh token expiry
                var refreshExpiry = settings.RefreshToken != results.RefreshToken
                    ? _utils.DecodeJwtTokenBody(results.RefreshToken).exp
                    : settings.RefreshExpiresAt;

                settings.UpdateAccess(
                    results.AccessToken, results.AccessExpiresAt, results.RefreshToken, refreshExpiry);
                _settingsStore.Save(settings);
            }
        }

        public async Task RefreshRuntimeAccessIfExpiringAsync(IAuthSettings settings, DateTime nowUtc)
        {
            // Only refresh if we have a refresh token, and the access token is close to expiry.
            if (!string.IsNullOrEmpty(settings.RefreshToken))
            {
                // If we don't have an access token, or either token is close to expiring, refresh.
                if (string.IsNullOrEmpty(settings.AccessToken) ||
                   _utils.IsAccessCloseToExpiration(settings.AccessExpiresAt, nowUtc) ||
                   _utils.IsAccessCloseToExpiration(settings.RefreshExpiresAt, nowUtc))
                {
                    await RefreshRuntimeAccessAsync(settings);
                }
            }
        }
    }
}
