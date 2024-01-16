// Copyright 2022-2024 Niantic.

using System;
using System.Globalization;
using UnityEngine.Networking;

namespace Niantic.Lightship.AR.VpsCoverage
{
    internal static class ResponseStatusTranslator
    {
        public static ResponseStatus FromString(string status)
        {
            ResponseStatus result;
            if (Enum.TryParse(status, out result))
            {
                return result;
            }

            status = status.ToLower().Replace("_", " ");
            TextInfo info = CultureInfo.CurrentCulture.TextInfo;
            status = info.ToTitleCase(status).Replace(" ", string.Empty);
            Enum.TryParse(status, out result);
            return result;
        }

        public static ResponseStatus FromHttpStatus(UnityWebRequest.Result httpRequestProgressStatus,
            long httpStatusCode)
        {
            Enum.TryParse(httpRequestProgressStatus.ToString(), out ResponseStatus responseStatus);

            if (responseStatus == ResponseStatus.ProtocolError)
            {
                Enum.TryParse(httpStatusCode.ToString(), out responseStatus);
            }

            return responseStatus;
        }
    }
}
