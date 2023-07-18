using System;
using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR;
using UnityEngine;

namespace Niantic.ARDK.AR.Scanning
{
    /// Response from the server to requests from <see cref="IScanTargetClient"/>.
    public class ScanTargetResult
    {
        /// List of targets returned by the server. This may be empty if there were no scan targets within the
        /// radius of the query.
        public readonly List<ScanTarget> ScanTargets;

        /// Status code returned by the server.
        public ResponseStatus Status;

        public ScanTargetResult(List<ScanTarget> scanTargets)
        {
            this.Status = ResponseStatus.Success;
            this.ScanTargets = scanTargets;
        }

        public ScanTargetResult(ResponseStatus status)
        {
            this.Status = status;
            this.ScanTargets = new List<ScanTarget>();
        }
    }
}
