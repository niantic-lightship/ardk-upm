using System;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Auth;
using Niantic.Lightship.AR.Utilities.Auth;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor.Auth
{
    internal interface IAuthEditorSettingsUpdater
    {
        /// <summary>
        /// Refresh access token data in editor settings
        /// </summary>
        /// <param name="editorSettings">editor-only settings</param>
        /// <returns>task can be awaited until complete</returns>
        Task RefreshAccessAsync(IAuthEditorSettings editorSettings);

        /// <summary>
        /// Refresh access token data if expiring soon (or expired)
        /// </summary>
        /// <param name="editorSettings">editor-only settings</param>
        /// <param name="nowUtc">the current time</param>
        /// <returns>task can be awaited until complete</returns>
        Task RefreshAccessIfExpiringAsync(IAuthEditorSettings editorSettings, DateTime nowUtc);

        /// <summary>
        /// Request a runtime refresh token and place it in Editor settings (whilst this token is intended
        /// for runtime use, we request it the first time from outside a runtime session).
        /// </summary>
        /// <param name="editorSettings">editor-only settings</param>
        /// <param name="settings">editor settings</param>
        /// <returns>task can be awaited until complete</returns>
        Task RequestRuntimeRefreshTokenAsync(IAuthEditorSettings editorSettings, IAuthSettings settings);

        /// <summary>
        /// Request a new runtime refresh token if the current one is close to expiring.
        /// </summary>
        /// <param name="editorSettings">editor-only settings</param>
        /// <param name="settings">the settings object that holds current tokens</param>
        /// <param name="nowUtc">the current time</param>
        /// <returns>task can be awaited until complete</returns>
        Task RequestRuntimeRefreshTokenIfExpiringAsync(IAuthEditorSettings editorSettings, IAuthSettings settings, DateTime nowUtc);
    }

    internal class AuthEditorSettingsUpdater : IAuthEditorSettingsUpdater
    {
        private readonly IAuthEditorGatewayAccess _gatewayAccess;
        private readonly IAuthGatewayUtils _utils;
        private readonly IAuthRuntimeSettingsStore _runtimeSettingsStore;

        private AuthEditorSettingsUpdater(
            IAuthEditorGatewayAccess gatewayAccess, IAuthGatewayUtils utils, IAuthRuntimeSettingsStore runtimeSettingsStore)
        {
            _gatewayAccess = gatewayAccess;
            _utils = utils;
            _runtimeSettingsStore = runtimeSettingsStore;
        }

        /// <summary>
        /// Create() function for testing (allows mocking of dependencies)
        /// </summary>
        public static IAuthEditorSettingsUpdater Create(
            IAuthEditorGatewayAccess gatewayAccess, IAuthGatewayUtils utils, IAuthRuntimeSettingsStore runtimeSettingsStore)
        {
            return new AuthEditorSettingsUpdater(gatewayAccess, utils, runtimeSettingsStore);
        }

        /// <summary>
        /// Singleton for runtime use
        /// </summary>
        public static IAuthEditorSettingsUpdater Instance { get; } =
            Create(AuthEditorGatewayAccess.Instance, AuthGatewayUtils.Instance, AuthRuntimeSettingsStore.Instance);

        public async Task RefreshAccessAsync(IAuthEditorSettings editorSettings)
        {
            var results = await _gatewayAccess.RefreshEditorAccessAsync(editorSettings.EditorRefreshToken);
            if (!string.IsNullOrEmpty(results.AccessToken))
            {
                var refreshExpiresAt = _utils.DecodeJwtTokenBody(results.RefreshToken).exp;
                editorSettings.UpdateEditorAccess(
                    results.AccessToken, results.AccessExpiresAt, results.RefreshToken, refreshExpiresAt);
            }
            else
            {
#if NIANTIC_ARDK_AUTH_DEBUG
                Debug.Log(
                    "Failed to refresh editor access token (refresh token may be invalid). Clearing out editor tokens (logged out)");
#endif
                editorSettings.UpdateEditorAccess(string.Empty, 0, string.Empty, 0);
            }
        }

        public async Task RefreshAccessIfExpiringAsync(IAuthEditorSettings editorSettings, DateTime nowUtc)
        {
            // Only refresh if we have a valid unexpired refresh token, and the access token is close to expiry.
            if (!string.IsNullOrEmpty(editorSettings.EditorRefreshToken) &&
                _utils.IsAccessCloseToExpiration(editorSettings.EditorAccessExpiresAt, nowUtc) &&
                !_utils.IsAccessExpired(editorSettings.EditorRefreshExpiresAt, nowUtc))
            {
                await RefreshAccessAsync(editorSettings);
            }
        }

        public async Task RequestRuntimeRefreshTokenAsync(IAuthEditorSettings editorSettings, IAuthSettings settings)
        {
            var results = await _gatewayAccess.RequestRuntimeRefreshTokenAsync(editorSettings.EditorRefreshToken);
            if (!string.IsNullOrEmpty(results.RefreshToken))
            {
                settings.UpdateAccess(string.Empty, 0, results.RefreshToken, results.ExpiresAt);

                // Clear the play-mode persistent data settings, as we have a new runtime refresh token.
                _runtimeSettingsStore.Clear();
            }
        }

        public async Task RequestRuntimeRefreshTokenIfExpiringAsync(
            IAuthEditorSettings editorSettings, IAuthSettings settings, DateTime nowUtc)
        {
            // If we have a valid editor refresh token, and our runtime access is either close to expiry or missing,
            // request a new runtime refresh token.
            if (!string.IsNullOrEmpty(editorSettings.EditorRefreshToken) &&
                !_utils.IsAccessExpired(editorSettings.EditorRefreshExpiresAt, nowUtc))
            {
                if (string.IsNullOrEmpty(settings.RefreshToken) ||
                    _utils.IsAccessCloseToExpiration(settings.RefreshExpiresAt, nowUtc))
                {
                    await RequestRuntimeRefreshTokenAsync(editorSettings, settings);
                }
            }
        }
    }
}
