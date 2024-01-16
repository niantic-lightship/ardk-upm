// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// Received result from server request for LocalizationTargets.
    [PublicAPI]
    public class LocalizationTargetsResult
    {
        internal LocalizationTargetsResult(HttpResponse<LocalizationTargetsResponse> response)
        {
            Status = response.Status;

            if (Status == ResponseStatus.Success)
            {
                var activationTargets = new Dictionary<string, LocalizationTarget>();
                if (response.Data.vps_localization_target != null)
                {
                    activationTargets = new Dictionary<string, LocalizationTarget>();
                    foreach (var target in response.Data.vps_localization_target)
                    {
                        activationTargets[target.id] = new LocalizationTarget(target);
                    }
                }

                ActivationTargets = activationTargets;
            }
        }

        /// Response status of server request.
        public ResponseStatus Status { get; }

        /// Found LocalizationTargets found for the request as a dictionary with their identifier as keys.
        public IReadOnlyDictionary<string, LocalizationTarget> ActivationTargets { get; }
    }
}
