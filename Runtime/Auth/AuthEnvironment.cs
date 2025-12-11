
using System;

namespace Niantic.Lightship.AR.Auth
{
    /// <summary>
    /// The type of environment to use for authentication (selects which portal and identity server to use)
    /// </summary>
    public enum AuthEnvironmentType {
        Dev,
        Staging,
        Production
    }

    /// <summary>
    /// Interface to an instance of the Utility class for getting URLs for authentication, based on the environment
    /// </summary>
    internal interface IAuthEnvironment
    {
        /// <summary>
        /// Get the identity endpoint for the given environment type.
        /// </summary>
        /// <param name="type">the type of environment (dev, staging, prod)</param>
        /// <returns>URL for the identity endpoint</returns>
        string GetIdentityEndpoint(AuthEnvironmentType type);
    }

    /// <summary>
    /// Utility class for getting URLs for authentication, based on the selected environment
    /// </summary>
    internal class AuthEnvironment : IAuthEnvironment
    {
        /// <summary>
        /// URL for the sample app website.
        /// This is used for login/logout of the sample apps, along with Unity Editor itself.
        /// </summary>
        /// <param name="type">the type of environment (dev, staging, prod)</param>
        /// <returns>URL for the sample app website</returns>
        internal static string GetSampleAppWebsite(AuthEnvironmentType type)
        {
            return type switch
            {
                AuthEnvironmentType.Dev => "https://sample-app-frontend-internal-dev.eng.nianticspatial.com",
                AuthEnvironmentType.Staging => "https://sample-app-frontend-internal-stg.eng.nianticspatial.com",
                AuthEnvironmentType.Production => "https://sample-app-frontend-internal.nianticspatial.com",
                _ => throw new Exception($"Unknown AuthEnvironmentType: {type}")
            };
        }

        private static string GetIdentityUrl(AuthEnvironmentType type)
        {
            return type switch
            {
                AuthEnvironmentType.Dev => "https://spatial-identity-dev.eng.nianticspatial.com",
                AuthEnvironmentType.Staging => "https://spatial-identity-stg.eng.nianticspatial.com",
                AuthEnvironmentType.Production => "https://spatial-identity.nianticspatial.com",
                _ => throw new Exception($"Unknown AuthEnvironmentType: {type}")
            };
        }

        public string GetIdentityEndpoint(AuthEnvironmentType type)
        {
            return $"{GetIdentityUrl(type)}/{AuthConstants.IdentityMethod}";
        }

        /// <summary>
        /// Private constructor to enforce singleton
        /// </summary>
        private AuthEnvironment()
        {}

        /// <summary>
        /// Instance of AuthEnvironment
        /// </summary>
        public static IAuthEnvironment Instance { get; } = new AuthEnvironment();
    }
}
