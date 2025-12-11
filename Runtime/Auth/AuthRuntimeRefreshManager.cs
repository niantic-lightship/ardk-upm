using System;
using System.Threading;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Loader;
using UnityEngine;

namespace Niantic.Lightship.AR.Auth
{
    /// <summary>
    /// Manager to ensure that the access token is refreshed in the background (assuming we have a valid refresh token).
    /// </summary>
    internal static class AuthRuntimeRefreshManager
    {
        // Interval in seconds between checks for token expiration. Arbitrarily set to 10 seconds.
        private const double UpdateInterval = 10;

        private static CancellationTokenSource s_settingsUpdatedCts;

        public static void RestartRefreshLoop()
        {
#if NIANTIC_ARDK_AUTH_DEBUG
            Debug.Log("[Auth] Cancelling any existing refresh loop.");
#endif
            s_settingsUpdatedCts?.Cancel();
            if (!string.IsNullOrEmpty(LightshipSettingsHelper.ActiveSettings?.RefreshToken))
            {
                _ = RefreshAccessAsync();
            }
        }

        private static readonly IAuthRuntimeSettingsUpdater s_settingsUpdater = AuthRuntimeSettingsUpdater.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void SetupRuntimeRefresh()
        {
            // Start the refresh task as soon as settings are initialized
            // (if the access token is expired, we want to refresh immediately)
            if (LightshipSettingsHelper.ActiveSettings != null)
            {
                StartRuntimeRefresh();
            }
            else
            {
                LightshipSettingsHelper.OnRuntimeSettingsCreated += StartRuntimeRefresh;
            }
        }

        private static void StartRuntimeRefresh()
        {
            // Remove the event handler if we're subscribed
            LightshipSettingsHelper.OnRuntimeSettingsCreated -= StartRuntimeRefresh;

            // Start the refresh task.
            _ = RefreshAccessAsync();
        }

        private static async Task RefreshAccessAsync()
        {
            // Grab a copy of the application's cancellation token, so we can cancel the task if on exit
            // (the token is replaced on exit, so we need the current one)
            s_settingsUpdatedCts = new CancellationTokenSource();
            var cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                s_settingsUpdatedCts.Token, Application.exitCancellationToken).Token;

            // If we have an API key, don't load runtime settings and don't start the refresh loop.
            if (!string.IsNullOrEmpty(LightshipSettingsHelper.ActiveSettings.ApiKey))
            {
#if NIANTIC_ARDK_AUTH_DEBUG
                Debug.Log("[Auth] Refresh loop not started as we have an API key.");
#endif
                return;
            }

            // Load the current runtime settings from disk, if available
            // (otherwise we use the default settings)
            await AuthRuntimeSettingsStore.Instance.LoadAsync(LightshipSettingsHelper.ActiveSettings, cancellationToken);

            // Don't start the refresh loop if we don't have a refresh token.
            if (string.IsNullOrEmpty(LightshipSettingsHelper.ActiveSettings.RefreshToken))
            {
#if NIANTIC_ARDK_AUTH_DEBUG
                Debug.Log("[Auth] Refresh loop not started as we don't have a refresh token.");
#endif
                return;
            }

#if NIANTIC_ARDK_AUTH_DEBUG
            Debug.Log("[Auth] Refresh loop starting ...");
#endif

            // Loop that runs forever during runtime, periodically refreshing the access token
            // (if we have a valid refresh token)
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // the active runtime settings may change during the lifecycle of the app
                    // update the latest active settings instance
                    await s_settingsUpdater.RefreshRuntimeAccessIfExpiringAsync(
                        LightshipSettingsHelper.ActiveSettings, DateTime.UtcNow);
                    await Task.Delay(TimeSpan.FromSeconds(UpdateInterval), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Exiting the application, so we don't need to do anything here.
            }
            finally
            {
#if NIANTIC_ARDK_AUTH_DEBUG
                Debug.Log("[Auth] Refresh loop stopped.");
#endif
            }
        }
    }
}
