using System;
using Niantic.Lightship.AR.Auth;
using Niantic.Lightship.AR.Utilities.Auth;
using Niantic.Lightship.AR.Utilities.Http;

namespace Niantic.Lightship.AR.Editor.Auth
{
    /// <summary>
    /// Command that triggers logout.
    /// </summary>
    internal interface IAuthEditorLogoutCommand
    {
        /// <summary>
        /// Execute the command.
        /// </summary>
        void Execute();
    }


    internal class AuthEditorLogoutCommand : IAuthEditorLogoutCommand
    {
        public const string SignoutUrl = "{0}/signout?refreshToken={1}";

        // Response from the refresh endpoint
        [Serializable]
        private class LogoutResponse
        {
        }

        // Dependencies:
        private readonly IAuthEditorSettings _editorSettings;
        private readonly IAuthSettings _settings;
        private readonly IAuthRuntimeSettingsStore _runtimeSettingsStore;
        private readonly IOpenUrlCommand _openUrlCommand;
        private readonly IAuthGatewayUtils _utils;

        /// <summary>
        /// Constructor is private to control instantiation
        /// </summary>
        private AuthEditorLogoutCommand(
            IAuthEditorSettings editorSettings, IAuthSettings settings,
            IOpenUrlCommand openUrlCommand, IAuthRuntimeSettingsStore runtimeSettingsStore, IAuthGatewayUtils utils)
        {
            _editorSettings = editorSettings;
            _settings = settings;
            _openUrlCommand = openUrlCommand;
            _runtimeSettingsStore = runtimeSettingsStore;
            _utils = utils;
        }

        /// <summary>
        /// Create() function for testing
        /// </summary>
        public static IAuthEditorLogoutCommand Create(
            IAuthEditorSettings editorSettings, IAuthSettings settings,
            IOpenUrlCommand openUrlCommand, IAuthRuntimeSettingsStore runtimeSettingsStore, IAuthGatewayUtils utils)
        {
            return new AuthEditorLogoutCommand(editorSettings, settings, openUrlCommand, runtimeSettingsStore, utils);
        }

        /// <summary>
        /// Singleton of this class for editor use
        /// </summary>
        public static IAuthEditorLogoutCommand Instance { get; } = Create(
            AuthEditorSettings.Instance, AuthEditorBuildSettings.Instance,
            OpenURLCommand.Instance, AuthRuntimeSettingsStore.Instance, AuthGatewayUtils.Instance);

        public void Execute()
        {
            // The sample app website for this environment also handles logout
            var website = AuthEnvironment.GetSampleAppWebsite(_settings.AuthEnvironment);
            var uriEncodedParam = _utils.EncodeUriParameter(_editorSettings.EditorRefreshToken);

            // Open the Signout page. This will clear the tokens stored in the browser.
            _openUrlCommand.Execute(string.Format(SignoutUrl, website, uriEncodedParam));

            // Clean out the locally held tokens (whether or not the server command is successful)
            _editorSettings.UpdateEditorAccess(string.Empty, 0, string.Empty, 0);
            // Also clear out the runtime tokens
            _settings.UpdateAccess(string.Empty, 0, string.Empty, 0);
            // Clear out the runtime persistent tokens
            _runtimeSettingsStore.Clear();
        }
    }
}
