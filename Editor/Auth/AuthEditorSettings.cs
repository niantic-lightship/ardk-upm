using System;
using System.IO;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Auth;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor.Auth
{
    /// <summary>
    /// Interface to the class that holds editor-only authentication settings.
    /// </summary>
    public interface IAuthEditorSettings
    {
        /// <summary>
        /// Get the current auth environment.
        /// </summary>
        AuthEnvironmentType AuthEnvironment { get; set; }

        /// <summary>
        /// The current valid access token for editor login
        /// </summary>
        string EditorAccessToken { get; }

        /// <summary>
        /// Time when the editor login access token expires
        /// </summary>
        int EditorAccessExpiresAt { get; }

        /// <summary>
        /// Token for refreshing the editor login access token (if available)
        /// </summary>
        string EditorRefreshToken { get; }

        /// <summary>
        /// Time when the editor login refresh token expires
        /// </summary>
        int EditorRefreshExpiresAt { get; }

        /// <summary>
        /// Set login access token information in one block (which matches how it's received)
        /// </summary>
        /// <param name="accessToken">the current valid access token</param>
        /// <param name="accessExpiresAt">time when the access token expires</param>
        /// <param name="refreshToken">token for refreshing the access token (if available)</param>
        /// <param name="refreshExpiresAt">time when the refresh token expires</param>
        void UpdateEditorAccess(string accessToken, int accessExpiresAt, string refreshToken, int refreshExpiresAt);
    }

    /// <summary>
    /// Class that is used to write the file that holds all editor-only authentication settings.
    /// </summary>
    [Serializable]
    internal class AuthEditorSettings : IAuthEditorSettings
    {
        [SerializeField]
        private AuthEnvironmentType _authEnvironment = AuthEnvironmentType.Production;

        [SerializeField]
        private string _editorAccessToken = string.Empty;

        [SerializeField]
        private int _editorAccessExpiresAt;

        [SerializeField]
        private string _editorRefreshToken = string.Empty;

        [SerializeField]
        private int _editorRefreshExpiresAt;

        // Dependencies:
        // LightshipSettings is a functor because the lifetime of a ScriptableObject is unpredictable
        private readonly Func<LightshipSettings> _getLightshipSettings;
        private readonly IFileObjectStore<AuthEditorSettings> _store;

        private static AuthEditorSettings s_instance;

        public AuthEnvironmentType AuthEnvironment
        {
            get => _authEnvironment;
            set
            {
                _authEnvironment = value;
                // Keep the auth environment in LightshipSettings in-sync with the editor settings.
                _getLightshipSettings().AuthEnvironment = value;
            }
        }

        public string EditorAccessToken => _editorAccessToken;
        public int EditorAccessExpiresAt => _editorAccessExpiresAt;
        public string EditorRefreshToken => _editorRefreshToken;
        public int EditorRefreshExpiresAt => _editorRefreshExpiresAt;

        /// <summary>
        /// Event to allow notification of when this object has loaded the stored data
        /// </summary>
        public static event Action OnLoaded;

        private AuthEditorSettings(
            Func<LightshipSettings> getLightshipSettings, IFileObjectStore<AuthEditorSettings> store)
        {
            _getLightshipSettings = getLightshipSettings;
            _store = store;
        }

        /// <summary>
        /// Create function (public for testing)
        /// </summary>
        public static AuthEditorSettings Create(
            IEditorSettingsUtility settingsUtility,
            Func<LightshipSettings> getLightshipSettings,
            FileObjectStore<AuthEditorSettings>.Factory factory)
        {
            var path = Path.Combine(settingsUtility.ProjectSettingsPath, "authEditorSettings.json");
            var obj = new AuthEditorSettings(getLightshipSettings, factory(path));
            if (obj._store.Exists)
            {
                _ = obj.LoadAsync();
            }

            // Keep the auth environment in LightshipSettings in-sync with the auth environment in the editor settings.
            getLightshipSettings().AuthEnvironment = obj.AuthEnvironment;

            return obj;
        }

        /// <summary>
        /// Accessor to Auth editor settings asset instance.
        /// </summary>
        public static AuthEditorSettings Instance => GetOrCreateInstance();

        public void UpdateEditorAccess(string accessToken, int accessExpiresAt, string refreshToken, int refreshExpiresAt)
        {
            _editorRefreshToken = refreshToken;
            _editorAccessToken = accessToken;
            _editorAccessExpiresAt = accessExpiresAt;
            _editorRefreshExpiresAt = refreshExpiresAt;

#if NIANTIC_ARDK_AUTH_DEBUG
            Debug.Log($"[Auth] Saving Editor settings at {_store.Path}");
#endif
            _store.Save(this);
        }

        private static AuthEditorSettings GetOrCreateInstance()
        {
            if (s_instance == null)
            {
                s_instance = Create(
                    EditorSettingsUtility.Instance, () => LightshipSettings.Instance,
                    FileObjectStore<AuthEditorSettings>.Create);
            }

            return s_instance;
        }

        private async Task LoadAsync()
        {
            await _store.LoadAsync(this);
            OnLoaded?.Invoke();
        }
    }
}
