// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Settings;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.VpsCoverage
{
    /// <summary>
    /// Client to request CoverageAreas and LocalizationTargets.
    /// </summary>
    [PublicAPI]
    public class CoverageClient
    {
        private const string BasePath = "api/json/v1/";

        private const string CoverageAreasMethodName = "GET_VPS_COVERAGE";
        private const string LocalizationTargetMethodName = "GET_VPS_LOCALIZATION_TARGETS";

        private readonly string _coverageAreasEndpoint;
        private readonly string _localizationTargetsEndpoint;

        [Obsolete("Construct a CoverageClient using default constructor instead.")]
        public CoverageClient(LightshipSettings lightshipSettings)
        {
            _coverageAreasEndpoint = lightshipSettings.VpsCoverageEndpoint + BasePath + CoverageAreasMethodName;
            _localizationTargetsEndpoint = lightshipSettings.VpsCoverageEndpoint + BasePath + LocalizationTargetMethodName;
        }

        public CoverageClient()
        {
            var endpointSettings = LightshipSettingsHelper.ActiveSettings.EndpointSettings;
            _coverageAreasEndpoint = endpointSettings.VpsCoverageEndpoint + BasePath + CoverageAreasMethodName;
            _localizationTargetsEndpoint = endpointSettings.VpsCoverageEndpoint + BasePath + LocalizationTargetMethodName;
        }

        private async Task<CoverageAreasResult> RequestCoverageAreasAsync(LatLng queryLocation, int queryRadius)
        {
            CoverageAreasRequest request;

            // Server side we use radius == 0 then use max radius, radius < 0 then set radius to 0.
            // Client side we want a to use radius == 0 then radius = 0, radius < 0 then use max radius.
            if (queryRadius == 0)
            {
                queryRadius = -1;
            }
            else if (queryRadius < 0)
            {
                queryRadius = 0;
            }

            string requestId = Guid.NewGuid().ToString();
            var metadata = LegacyMetadataHelper.GetCommonDataEnvelopeWithRequestIdAsStruct(requestId);
            var requestHeaders = Metadata.GetApiGatewayHeaders(requestId);

            if (Input.location.status == LocationServiceStatus.Running)
            {
                var distanceToQuery = (int)queryLocation.Distance(new LatLng(Input.location.lastData));
                request = new CoverageAreasRequest(queryLocation, queryRadius, distanceToQuery, metadata);
            }
            else
            {
                request = new CoverageAreasRequest(queryLocation, queryRadius, metadata);
            }

            var response =
                await HttpClient.SendPostAsync<CoverageAreasRequest, CoverageAreasResponse>
                (
                    _coverageAreasEndpoint,
                    request,
                    requestHeaders
                );

            if (response.Status == ResponseStatus.Success)
            {
                response.Status = ResponseStatusTranslator.FromString(response.Data.status);
            }

            var result = new CoverageAreasResult(response);

            return result;
        }

        private async Task<LocalizationTargetsResult> RequestLocalizationTargetsAsync(string[] targetIdentifiers)
        {
            string requestId = Guid.NewGuid().ToString();
            var metadata = LegacyMetadataHelper.GetCommonDataEnvelopeWithRequestIdAsStruct(requestId);
            var requestHeaders = Metadata.GetApiGatewayHeaders(requestId);

            var request = new LocalizationTargetsRequest(targetIdentifiers, metadata);

            var response =
                await HttpClient.SendPostAsync<LocalizationTargetsRequest, LocalizationTargetsResponse>
                (
                    _localizationTargetsEndpoint,
                    request,
                    requestHeaders
                );

            if (response.Status == ResponseStatus.Success)
            {
                response.Status = ResponseStatusTranslator.FromString(response.Data.status);
            }

            var result = new LocalizationTargetsResult(response);

            return result;
        }

        /// <summary>
        /// Request CoverageAreas at device location within a radius using the callback pattern.
        /// </summary>
        /// <param name="queryLocation">Center of query</param>
        /// <param name="queryRadius">
        ///     Radius for query between 0m and 2000m.
        ///     A negative radius will default to the maximum radius of 2000m.
        /// </param>
        /// <param name="onAreasReceived">Callback invoked when the requested CoverageAreas are ready.</param>
        public async void TryGetCoverageAreas
        (
            LatLng queryLocation,
            int queryRadius,
            Action<CoverageAreasResult> onAreasReceived
        )
        {
            var result = await RequestCoverageAreasAsync(queryLocation, queryRadius);
            onAreasReceived?.Invoke(result);
        }

        /// <summary>
        /// Request LocalizationTargets for a set of identifiers using the callback pattern.
        /// </summary>
        /// <param name="targetIdentifiers">Set of unique identifiers of the requested targets.</param>
        /// <param name="onTargetsReceived">Callback invoked when the requested LocalizationTargets are ready.</param>
        public async void TryGetLocalizationTargets
        (
            string[] targetIdentifiers,
            Action<LocalizationTargetsResult> onTargetsReceived
        )
        {
            var result = await RequestLocalizationTargetsAsync(targetIdentifiers);
            onTargetsReceived?.Invoke(result);
        }

        /// <summary>
        /// Request coupled CoverageAreas and LocalizationTargets within a radius, using the callback pattern.
        /// </summary>
        /// <param name="queryLocation">Center of query</param>
        /// <param name="queryRadius">
        ///     Radius for query between 0m and 2000m.
        ///     A negative radius will default to the maximum radius of 2000m.
        /// </param>
        /// <param name="onLocationsReceived">
        ///     Callback invoked when the requested CoverageAreas and LocalizationTargets are ready.
        /// </param>
        /// <param name="privateScanLocalizationTargets">
        ///     Optional. For any LocalizationTarget included in this array, a corresponding AreaTarget will be added to
        ///     the AreaTargets returned through the onLocationsReceived callback. Specify your private AR Locations via
        ///     the CoverageClientManager in order to utilize this parameter.
        /// </param>
        public async void TryGetCoverage
        (
            LatLng queryLocation,
            int queryRadius,
            Action<AreaTargetsResult> onLocationsReceived,
            LocalizationTarget[] privateScanLocalizationTargets = null
        )
        {
            var areasResult = await RequestCoverageAreasAsync(queryLocation, queryRadius);
            LocalizationTargetsResult targetsResult = null;

            if (areasResult.Status == ResponseStatus.Success)
            {
                var targetIds = new List<string>();
                foreach (var area in areasResult.Areas)
                {
                    foreach (string target in area.LocalizationTargetIdentifiers)
                    {
                        targetIds.Add(target);
                    }
                }

                targetsResult = await RequestLocalizationTargetsAsync(targetIds.ToArray());
            }

            var responseStatus = targetsResult?.Status ?? areasResult.Status;
            var locationsResult =
                new AreaTargetsResult
                (
                    queryLocation,
                    queryRadius,
                    responseStatus,
                    areasResult,
                    targetsResult,
                    privateScanLocalizationTargets
                );

            onLocationsReceived?.Invoke(locationsResult);
        }

        /// <summary>
        /// Downloads the image from the provided url as a texture, using the async await pattern.
        /// </summary>
        /// <param name="imageUrl">URL of the localization target's hint image</param>
        public async Task<Texture> TryGetImageFromUrl(string imageUrl)
        {
            var image = await HttpClient.DownloadImageAsync(imageUrl);
            return image;
        }

        /// <summary>
        /// Downloads the image from the provided url as a texture, using the callback pattern.
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <param name="onImageDownloaded">Callback for downloaded image as texture. When download fails,
        /// texture is returned as null.</param>
        public async void TryGetImageFromUrl(string imageUrl, Action<Texture> onImageDownloaded)
        {
            var image = await HttpClient.DownloadImageAsync(imageUrl);
            onImageDownloaded?.Invoke(image);
        }

        /// Downloads the image from the provided url as a texture cropped to a fixed size, using the
        /// async await pattern.
        /// The source image is first resampled so the image is fitting for the limiting dimension, then
        /// it gets cropped to the fixed size.
        /// <param name="imageUrl">URL of the localization target's hint image</param>
        /// <param name="width">Fixed width of cropped image</param>
        /// <param name="height">Fixed height of cropped image</param>
        public async Task<Texture> TryGetImageFromUrl(string imageUrl, int width, int height)
        {
            var image = await HttpClient.DownloadImageAsync(imageUrl + "=w" + width + "-h" + height + "-c");
            return image;
        }

        /// Downloads the image from the provided url as a texture cropped to a fixed size, using the
        /// callback pattern.
        /// The source image is first resampled so the image is fitting for the limiting dimension, then
        /// it gets cropped to the fixed size.
        /// <param name="imageUrl">URL of the localization target's hint image</param>
        /// <param name="width">Fixed width of cropped image</param>
        /// <param name="height">Fixed height Fixed height of cropped image</param>
        /// <param name="onImageDownloaded">Callback for downloaded image as texture. When download fails
        /// texture is returned as null.</param>
        public async void TryGetImageFromUrl(string imageUrl, int width, int height, Action<Texture> onImageDownloaded)
        {
            var image = await HttpClient.DownloadImageAsync(imageUrl + "=w" + width + "-h" + height + "-c");
            onImageDownloaded?.Invoke(image);
        }
    }
}
