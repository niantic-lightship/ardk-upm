// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Reports the status of a localization network request (single client -> server request).
    /// </summary>
    [PublicAPI]
    public enum RequestStatus : byte
    {
        Unknown = 0,
        Pending,
        Successful,
        Failed,
    }

    /// <summary>
    /// Type of localization network request.
    /// </summary>
    [PublicAPI]
    public enum RequestType : byte
    {
        Localize = 0,
        GetGraph,
        GetReplacedNodes,
        RegisterNode,
    }

    /// <summary>
    /// Reports the error code of a localization network request, if any
    /// </summary>
    [PublicAPI]
    public enum ErrorCode : uint
    {
        Unknown = 0,
        None,
        BadNetworkConnection,
        BadApiKey,
        PermissionDenied,
        RequestsLimitExceeded,
        InternalServer,
        InternalClient,
    }

    /// <summary>
    /// Diagnostic information about a persistent anchor network request
    /// </summary>
    [PublicAPI]
    public struct XRPersistentAnchorNetworkRequestStatus
    {
        /// <summary>
        /// Id of the request
        /// </summary>
        public Guid RequestId;
        
        /// <summary>
        /// Status of the request
        /// </summary>
        public RequestStatus Status;
        
        /// <summary>
        /// Type of request sent
        /// </summary>
        public RequestType Type;
        
        /// <summary>
        /// Error code of the request, if any
        /// </summary>
        public ErrorCode Error;
        
        /// <summary>
        /// Time in ms that the request was sent
        /// Only comparable to EndTimeMs
        /// </summary>
        public ulong StartTimeMs;
        
        /// <summary>
        /// Time in ms that the response was received
        /// Only comparable to StartTimeMs
        /// </summary>
        public ulong EndTimeMs;

        /// <summary>
        /// Frame Id corresponding to frame sent in NetworkRequest, if available
        /// </summary>
        public UInt64 FrameId;
    }
}
