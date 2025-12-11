using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Niantic.Lightship.AR.Auth
{
    /// <summary>
    /// Interface to the runtime persistent storage of auth settings.
    /// </summary>
    internal interface IAuthRuntimeSettingsStore
    {
        /// <summary>
        /// Save the current runtime auth settings to persistent storage.
        /// This operation is synchronous to ensure that the settings are saved as soon as they are received.
        /// If the application were to exit before this operation completes, the saved tokens would be invalid
        /// (which would potentially break the app)
        /// </summary>
        /// <param name="settings">the current runtime auth settings to save</param>
        void Save(IAuthSettings settings);

        /// <summary>
        /// Load the auth settings from persistent storage (if available), and overwrite the current runtime settings.
        /// If no settings are found, the current runtime settings are preserved.
        /// </summary>
        /// <param name="settings">the current runtime auth settings to be modified</param>
        /// <param name="cancellationToken">token to handle external cancellation</param>
        Task LoadAsync(IAuthSettings settings, CancellationToken cancellationToken);

#if UNITY_EDITOR
        /// <summary>
        /// Clear the runtime auth settings from persistent storage (only available in the editor).
        /// </summary>
        void Clear();

        /// <summary>
        /// Check if the store exists (only available in the editor).
        /// </summary>
        bool Exists { get; }
#endif
    }

    internal class AuthRuntimeSettingsStore : IAuthRuntimeSettingsStore
    {
        [Serializable]
        private class AuthSavedSettings
        {
            // Disable warnings about naming rules as the following serialized fields are properties
            // ReSharper disable InconsistentNaming
            public string AccessToken;
            public int AccessExpiresAt;
            public string RefreshToken;
            public int RefreshExpiresAt;
            // ReSharper restore InconsistentNaming

            public AuthSavedSettings(IAuthSettings settings)
            {
                AccessToken = settings.AccessToken;
                AccessExpiresAt = settings.AccessExpiresAt;
                RefreshToken = settings.RefreshToken;
                RefreshExpiresAt = settings.RefreshExpiresAt;
            }
        }

        private const string Filename = "AuthSettings.json";

        private readonly string _filePath = Path.Combine(Application.persistentDataPath, Filename);

        /// <summary>
        /// Private constructor to prevent instantiation outside of singleton.
        /// </summary>
        private AuthRuntimeSettingsStore() {}

        /// <summary>
        /// Singleton for runtime use
        /// </summary>
        public static IAuthRuntimeSettingsStore Instance { get; } = new AuthRuntimeSettingsStore();

        public void Save(IAuthSettings settings)
        {
#if  NIANTIC_ARDK_AUTH_DEBUG
            Debug.Log($"[Auth] Saving runtime settings to {_filePath}");
#endif
            var savedSettings = new AuthSavedSettings(settings);
            var json = JsonUtility.ToJson(savedSettings);
            File.WriteAllText(_filePath, json);
        }

        public async Task LoadAsync(IAuthSettings settings, CancellationToken cancellationToken)
        {
            try
            {
                string json = await File.ReadAllTextAsync(_filePath, cancellationToken);
#if  NIANTIC_ARDK_AUTH_DEBUG
                Debug.Log($"[Auth] Loaded runtime settings from {_filePath}");
#endif
                var output = JsonUtility.FromJson<AuthSavedSettings>(json);

                if (output.RefreshExpiresAt < settings.RefreshExpiresAt)
                {
#if NIANTIC_ARDK_AUTH_DEBUG
                    Debug.Log($"[Auth] Ignoring loaded runtime settings as they are older than the current settings.");
#endif
                    return;
                }

                if (!string.IsNullOrEmpty(output.RefreshToken) && string.IsNullOrEmpty(settings.RefreshToken))
                {
#if NIANTIC_ARDK_AUTH_DEBUG
                    Debug.Log("[Auth] Ignoring persistent runtime settings as they use a refresh token (ergo developer auth) but the current settings do not.");
#endif
                    return;
                }


                settings.UpdateAccess(
                    output.AccessToken, output.AccessExpiresAt, output.RefreshToken, output.RefreshExpiresAt);
            }
            catch (FileNotFoundException)
            {
                // No settings file found; do nothing (nothing to load).
#if NIANTIC_ARDK_AUTH_DEBUG
                Debug.Log("[Auth] No runtime settings file found.");
#endif
            }
        }

#if UNITY_EDITOR
        public void Clear()
        {
            File.Delete(_filePath);
        }

        public bool Exists => File.Exists(_filePath);
#endif
    }
}
