// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;

using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems
{
    public enum RequestStatus : byte
    {
        Unknown = 0,
        Pending,
        Successful,
        Failed,
    }

    public enum RequestType : byte
    {
        Localize = 0,
        GetGraph,
        GetReplacedNodes,
        RegisterNode,
    }

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

    // Gives diagnostic information about a persistent anchor network request
    public struct XRPersistentAnchorNetworkRequestStatus
    {
        // Id of the request
        public Guid RequestId;
        public RequestStatus Status;
        public RequestType Type;
        public ErrorCode Error;
        public ulong StartTimeMs;
        public ulong EndTimeMs;
    }
}
