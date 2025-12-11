using System;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Auth;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Niantic.Lightship.AR.Editor.Auth
{
    /// <summary>
    /// Class to handle auth-related build tasks.
    /// </summary>
    public class AuthBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            ResetSavedBuildSettings();

            var editorSettings = AuthEditorSettings.Instance;
            var settings = AuthEditorBuildSettings.Instance;

            // Request a new runtime refresh token if we have the means to do so (an unexpired editor refresh token).
            // The current runtime refresh token has been baked into a build and cannot be reused outside that build.
            if (!string.IsNullOrEmpty(editorSettings.EditorRefreshToken) &&
                !AuthGatewayUtils.Instance.IsAccessExpired(editorSettings.EditorRefreshExpiresAt, DateTime.UtcNow))
            {
                // Request a new runtime refresh token.
                _ = AuthEditorSettingsUpdater.Instance.RequestRuntimeRefreshTokenAsync(editorSettings, settings);
            }
            else
            {
                settings.UpdateAccess(string.Empty, 0, string.Empty, 0);
            }
        }

        private static void ResetSavedBuildSettings()
        {
            // Reset the build settings (as stored on disk) so that no files appear modified:
            var targetBuildSettings = LightshipSettings.Instance.AuthBuildSettings;
            targetBuildSettings.Reset();
            SettingsUtils.SaveImmediatelyInEditor(targetBuildSettings);
        }
    }
}
