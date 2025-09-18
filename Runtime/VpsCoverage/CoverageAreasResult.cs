// Copyright 2022-2025 Niantic.

using System;
using System.Linq;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Http;

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// Received result from server request for CoverageAreas.
    [PublicAPI]
    public class CoverageAreasResult
    {
        internal CoverageAreasResult(HttpResponse<CoverageAreasResponse> response)
        {
            Status = ResponseStatusExtensions.Convert(response.Status);

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
