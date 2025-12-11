using System;
using System.Threading;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Auth;
using Niantic.Lightship.AR.Loader;
using UnityEditor;

namespace Niantic.Lightship.AR.Editor.Auth
{
    /// <summary>
    /// Class to handle high-level auth management whilst in the editor.
    /// </summary>
    [InitializeOnLoad]
    internal static class AuthEditorManager
    {
        // Interval in seconds between checks for token expiration. Arbitrarily set to 10 seconds.
        private const double UpdateInterval = 10;

        private const string AuthEditorManagerFirstTime = "AuthEditorManagerFirstTime";

        // Source for cancelling the editor update task.
        private static CancellationTokenSource s_editorUpdateCts = new();

        private static readonly IAuthEditorSettingsUpdater s_updater = AuthEditorSettingsUpdater.Instance;

        static AuthEditorManager()
        {
            // Subscribe to the event that fires when the editor is exiting play mode so that we can generate runtime
            // access tokens.
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Subscribe to the event that fires just before an assembly reload so that we are cleaned up, and
            // don't end up subscribed multiple times:
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            // If this is the first time we are running, start the editor update task here.
            if (!SessionState.GetBool(AuthEditorManagerFirstTime, false))
            {
                _ = EditorUpdateAsync(s_editorUpdateCts.Token);
                SessionState.SetBool(AuthEditorManagerFirstTime, true);
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            // Unsubscribe from everything (this class will be recreated when the assembly is reloaded).
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        private static async Task EditorUpdateAsync(CancellationToken cancellationToken)
        {
            var editorSettings = AuthEditorSettings.Instance;
            var settings = AuthEditorBuildSettings.Instance;
            // Bring the editor settings up to date with whatever happened in play-mode:
            await AuthRuntimeSettingsStore.Instance.LoadAsync(settings, cancellationToken);

            do
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await s_updater.RefreshAccessIfExpiringAsync(editorSettings, DateTime.UtcNow);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    await s_updater.RequestRuntimeRefreshTokenIfExpiringAsync(
                        editorSettings, settings, DateTime.UtcNow);
                }

                await Task.Delay(TimeSpan.FromSeconds(UpdateInterval), cancellationToken);
            } while (!cancellationToken.IsCancellationRequested);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredEditMode:
                    LightshipSettings.Instance.AuthBuildSettings.Reset();

                    s_editorUpdateCts = new();
                    _ = EditorUpdateAsync(s_editorUpdateCts.Token);
                    break;

                case PlayModeStateChange.ExitingEditMode:
                    s_editorUpdateCts.Cancel();
                    // Deploy all settings needed to AuthBuildSettings
                    AuthEditorDeploySettingsCommand.Instance.Execute();
                    break;
            }
        }
    }
}
