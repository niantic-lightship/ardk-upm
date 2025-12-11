using System;
using System.IO;
using Niantic.Lightship.AR.Auth;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Auth;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor.Auth
{
    /// <summary>
    /// Class that keeps track of auth build settings while in the editor.
    /// This is as opposed to AuthBuildSettings, which keeps track of these settings at runtime (play-mode or on-device)
    /// </summary>
    [Serializable]
    internal class AuthEditorBuildSettings : IAuthSettings
    {
        [SerializeField]
        private string _refreshToken = string.Empty;

        [SerializeField]
        private int _refreshExpiresAt;

        [SerializeField]
        private string _accessToken = string.Empty;

        [SerializeField]
        private int _accessExpiresAt;

        public string ApiKey => LightshipSettings.Instance.ApiKey;
        public AuthEnvironmentType AuthEnvironment => AuthEditorSettings.Instance.AuthEnvironment;

        /// <summary>
        /// The current runtime refresh token
        /// </summary>
        public string RefreshToken => _refreshToken;

        /// <summary>
        /// Time when the current runtime refresh token expires
        /// </summary>
        public int RefreshExpiresAt => _refreshExpiresAt;

        /// <summary>
        /// The current runtime access token
        /// </summary>
        public string AccessToken => _accessToken;

        /// <summary>
        /// Time when the current runtime access token expires
        /// </summary>
        public int AccessExpiresAt => _accessExpiresAt;

        // dependencies
        private readonly IAuthGatewayUtils _utils;
        private readonly IFileObjectStore<AuthEditorBuildSettings> _store;

        private AuthEditorBuildSettings(IAuthGatewayUtils utils, IFileObjectStore<AuthEditorBuildSettings> store)
        {
            _utils = utils;
            _store = store;
        }

        public static AuthEditorBuildSettings Create(
            IAuthGatewayUtils utils, IEditorSettingsUtility settingsUtility,
            FileObjectStore<AuthEditorBuildSettings>.Factory factory)
        {
            var path = Path.Combine(settingsUtility.ProjectSettingsPath, "authEditorBuildSettings.json");
            var store = factory(path);
            var instance = new AuthEditorBuildSettings(utils, store);
            // Load immediately and synchronously (if present)
            if (store.Exists)
            {
                store.Load(instance);
            }

            return instance;
        }

        public static AuthEditorBuildSettings Instance => Create(
            AuthGatewayUtils.Instance, EditorSettingsUtility.Instance, FileObjectStore<AuthEditorBuildSettings>.Create);

        public void UpdateAccess(string accessToken, int accessExpiresAt, string refreshToken, int refreshExpiresAt)
        {
            _refreshToken = refreshToken;
            _accessToken = accessToken;
            _accessExpiresAt = accessExpiresAt;
            _refreshExpiresAt = refreshExpiresAt;

            _store.Save(this);
            _utils.LogSettings(this, "Updated AuthEditorBuildSettings");
        }
    }
}
