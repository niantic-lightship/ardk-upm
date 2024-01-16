// Copyright 2022-2024 Niantic.

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// Status of a response from the VPS Coverage server.
    public enum ResponseStatus
    {
        // From UnityWebRequest.Result
        /// Could not reach the server
        ConnectionError,
        ProtocolError, // all 4xx and 5xx => see Gateway

        // From API Gateway
        /// No API key specified
        ApiKeyMissing = 400,

        /// API key is not valid
        Forbidden = 403,

        /// Too many requests in a short time triggered Rate Limiting
        TooManyRequests = 429,
        InternalGatewayError = 500,

        // From VPS Coverage backend API
        Unset,
        Success,
        InvalidRequest,
        InternalError,

        /// Over 100 localization targets requested in single request
        TooManyEntitiesRequested
    }
}
