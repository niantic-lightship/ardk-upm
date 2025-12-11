using Niantic.Lightship.AR.Auth;
using Niantic.Lightship.AR.Loader;

namespace Niantic.Lightship.AR.Editor.Auth
{
    internal interface ICommand
    {
        void Execute();
    }

    /// <summary>
    /// Command that copies the editor copy of the auth build settings to the settings that will be deployed to the
    /// device.
    /// </summary>
    internal class AuthEditorDeploySettingsCommand : ICommand
    {
        // dependencies
        private readonly IAuthEditorSettings _editorSettings;
        private readonly IAuthSettings _buildSettings;

        public void Execute()
        {
            // LightshipSettings is a ScriptableObject, so its lifetime is unpredictable.
            // We can't declare a dependency on it here, so we grab it when we need it.
            var deployedSettings = LightshipSettings.Instance.AuthBuildSettings;

            // Only copy over the auth settings if we're using developer authentication.
            if (deployedSettings.UseDeveloperAuthentication)
            {
                deployedSettings.AuthEnvironment = _editorSettings.AuthEnvironment;
                // There are cases in CI automated builds where we inject a
                // refresh token into the settings file 'deployedSettings'.
                // We don't want to erase those tokens with an empty token
                // so early out if nothing is set in the buildSettings
                if (string.IsNullOrEmpty(_buildSettings.RefreshToken))
                {
                    return;
                }
                deployedSettings.UpdateAccess(
                    _buildSettings.AccessToken, _buildSettings.AccessExpiresAt,
                    _buildSettings.RefreshToken, _buildSettings.RefreshExpiresAt);
            }
        }

        // Private constructor to enforce use of singleton instance or Create method (for testing).
        private AuthEditorDeploySettingsCommand(IAuthEditorSettings editorSettings, IAuthSettings buildSettings)
        {
            _editorSettings = editorSettings;
            _buildSettings = buildSettings;
        }

        public static ICommand Create(IAuthEditorSettings editorSettings, IAuthSettings buildSettings)
        {
            return new AuthEditorDeploySettingsCommand(editorSettings, buildSettings);
        }

        public static ICommand Instance { get; } = Create(
            AuthEditorSettings.Instance, AuthEditorBuildSettings.Instance);
    }
}
