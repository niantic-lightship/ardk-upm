// Copyright 2022-2025 Niantic.

using System;

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// Status of a response from the VPS Coverage server.
    /// ResponseStatus is a clone (potentially a subset) of Http.ResponseStatus, and any changes here must be reflected
    /// there. This public copy is maintained to serve where ResponseStatus is exposed in the VPS Coverage API
    /// (see AreaTargetsResult and CoverageAreasResult).
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

    internal static class ResponseStatusExtensions
    {
        /// <summary>
        /// Convert from Http.ResponseStatus to VpsCoverage.ResponseStatus.
        /// </summary>
        internal static ResponseStatus Convert(Niantic.Lightship.AR.Utilities.Http.ResponseStatus status) =>
            status switch
            {
                Utilities.Http.ResponseStatus.ConnectionError => ResponseStatus.ConnectionError,
                Utilities.Http.ResponseStatus.ProtocolError => ResponseStatus.ProtocolError,
                Utilities.Http.ResponseStatus.ApiKeyMissing => ResponseStatus.ApiKeyMissing,
                Utilities.Http.ResponseStatus.Forbidden => ResponseStatus.Forbidden,
                Utilities.Http.ResponseStatus.TooManyRequests => ResponseStatus.TooManyRequests,
                Utilities.Http.ResponseStatus.InternalGatewayError => ResponseStatus.InternalGatewayError,
                Utilities.Http.ResponseStatus.Success => ResponseStatus.Success,
                Utilities.Http.ResponseStatus.InvalidRequest => ResponseStatus.InvalidRequest,
                Utilities.Http.ResponseStatus.InternalError => ResponseStatus.InternalError,
                Utilities.Http.ResponseStatus.TooManyEntitiesRequested => ResponseStatus.TooManyEntitiesRequested,
                _ => throw new Exception($"Unknown ResponseStatus: {status}"),
            };
    }
}
