// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Linq;

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// Received result from server request for CoverageAreas.
    public class CoverageAreasResult
    {
        internal CoverageAreasResult(HttpResponse<CoverageAreasResponse> response)
        {
            Status = response.Status;

            if (Status == ResponseStatus.Success)
            {
                if (response.Data.vps_coverage_area != null)
                {
                    Areas = response.Data.vps_coverage_area.Select(t => new CoverageArea(t)).ToArray();
                }
                else
                {
                    Areas = Array.Empty<CoverageArea>();
                }
            }
        }

        /// Response status of server request.
        public ResponseStatus Status { get; }

        /// CoverageAreas found from the request.
        public CoverageArea[] Areas { get; }
    }
}
