using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.Auth
{
    /// <summary>
    /// Class that handles the asset that holds all auth settings passed to runtime.
    /// </summary>
    internal class AuthBuildSettings : ScriptableObject
    {
        [SerializeField]
        private AuthEnvironmentType _authEnvironment = AuthEnvironmentType.Production;

        [SerializeField]
        private string _refreshToken = string.Empty;

        [SerializeField]
        private int _refreshExpiresAt;

        [SerializeField]
        private string _accessToken = string.Empty;

        [SerializeField]
        private int _accessExpiresAt;

        [SerializeField]
        private bool _useDeveloperAuthentication = true;

        /// <summary>
        /// Get or set the current auth environment.
        /// </summary>
        public AuthEnvironmentType AuthEnvironment
        {
            get => _authEnvironment;
            set
            {
                if (_authEnvironment != value)
                {
                    _authEnvironment = value;
                    SettingsUtils.SaveImmediatelyInEditor(this);
                }
            }
        }

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

        /// <summary>
        /// Whether to use the initial access and refresh tokens passed to runtime.
        /// </summary>
        public bool UseDeveloperAuthentication
        {
            get => _useDeveloperAuthentication;
            set => _useDeveloperAuthentication = value;
        }

        public void UpdateAccess(string accessToken, int accessExpiresAt, string refreshToken, int refreshExpiresAt)
        {
            _refreshToken = refreshToken;
            _accessToken = accessToken;
            _accessExpiresAt = accessExpiresAt;
            _refreshExpiresAt = refreshExpiresAt;
        }

        public void Reset()
        {
            _refreshToken = string.Empty;
            _accessToken = string.Empty;
            _accessExpiresAt = 0;
            _refreshExpiresAt = 0;
        }
    }
}
