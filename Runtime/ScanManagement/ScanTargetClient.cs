using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Settings.User;
using UnityEngine;
using Input = UnityEngine.Input;


namespace Niantic.ARDK.AR.Scanning
{
    /// Client for requesting scan targets close to the user's location. A scan target is a location that can be scanned
    /// and activated for VPS.
    public class ScanTargetClient
    {
        private const string ScanTargetEndpoint =
            "https://vps-coverage-api.nianticlabs.com/api/json/v1/SEARCH_SCAN_TARGETS";

        private Dictionary<string, string> _encodedScanIds = new Dictionary<string, string>();

        private readonly LightshipSettings _lightshipSettings;

        public ScanTargetClient(LightshipSettings lightshipSettings) => _lightshipSettings = lightshipSettings;

        /// Request scan targets within a given radius of a location using the callback pattern.
        /// @param queryLocation Center of query.
        /// @param queryRadius Radius for query between 0m and 2000m. Negative radius will default to the maximum radius of 2000m.
        /// @param onScanTargetReceived Callback function to process the received ScanTargetResponse.
        public async void RequestScanTargets(LatLng queryLocation, int queryRadiusMeters,
            Action<ScanTargetResult> onScanTargetReceived)
        {
            ScanTargetResult result = await RequestScanTargetsAsync(queryLocation, queryRadiusMeters);
            onScanTargetReceived?.Invoke(result);
        }

        internal virtual Task<_HttpResponse<ScanTargetResponse>> SendNetworkRequest(
            string endpoint, ScanTargetRequest request, Dictionary<string, string> headers)
        {
            return _HttpClient.SendPostAsync<ScanTargetRequest, ScanTargetResponse>(endpoint, request, headers);
        }

        /// Requests scan targets within a given radius of a location using the async/await pattern.
        /// @param queryLocation Center of query.
        /// @param queryRadius Radius for query between 0m and 2000m. Negative radius will default to the maximum radius of 2000m.
        /// @returns Task with the received ScanTargetResponse as result.
        public async Task<ScanTargetResult> RequestScanTargetsAsync(LatLng queryLocation, int queryRadiusMeters)
        {
            ScanTargetRequest request;
            // Server side we use radius == 0 then use max radius, radius < 0 then set radius to 0.
            // Client side we want a to use radius == 0 then radius = 0, radius < 0 then use max radius.
            if (queryRadiusMeters == 0)
                queryRadiusMeters = -1;
            else if (queryRadiusMeters < 0)
                queryRadiusMeters = 0;

            string requestId = Guid.NewGuid().ToString();
            var metadata = _lightshipSettings.GetCommonDataEnvelopeWithRequestIdAsStruct(requestId);
            var requestHeaders = Metadata.GetApiGatewayHeaders(requestId);
            requestHeaders.Add("Authorization", _lightshipSettings.ApiKey);
            if (Input.location.status == LocationServiceStatus.Running)
            {
                int distanceToQuery = (int)queryLocation.Distance(new LatLng(Input.location.lastData));
                request = new ScanTargetRequest(queryLocation, queryRadiusMeters, distanceToQuery, metadata);
            }
            else
            {
                request = new ScanTargetRequest(queryLocation, queryRadiusMeters, metadata);
            }

            _HttpResponse<ScanTargetResponse> response =
                await SendNetworkRequest
                (
                    ScanTargetEndpoint,
                    request,
                    requestHeaders
                );
            ResponseStatus trueResponseStatus = response.Status == ResponseStatus.Success
                ? _ResponseStatusTranslator.FromString(response.Data.status)
                : response.Status;

            if (trueResponseStatus != ResponseStatus.Success)
            {
                return new ScanTargetResult(trueResponseStatus);
            }

            if (response.Data.scan_targets == null)
            {
                // Request is successful, but there are no results.
                return new ScanTargetResult(ResponseStatus.Success);
            }

            List<ScanTarget> result = response.Data.scan_targets.Select(scanTarget =>
            {
                ScanTarget target = new ScanTarget();
                target.Name = scanTarget.name;
                target.Shape = new LatLng[] { scanTarget.shape.point };
                target.ImageUrl = scanTarget.image_url;
                Enum.TryParse(scanTarget.vps_status, true, out ScanTarget.ScanTargetLocalizabilityStatus status);
                target.LocalizabilityStatus = status;
                string encodedScanTargetId;
                if (_encodedScanIds.ContainsKey(scanTarget.id))
                {
                    encodedScanTargetId = _encodedScanIds[scanTarget.id];
                }
                else
                {
                    // Introduce randomness to the ID here so they are not expected to be stable.

                    byte[] key = new byte[8];
                    byte[] iv = new byte[8];
                    RNGCryptoServiceProvider rngCryptoServiceProvider = new RNGCryptoServiceProvider();
                    rngCryptoServiceProvider.GetBytes(key);
                    rngCryptoServiceProvider.GetBytes(iv);
                    SymmetricAlgorithm algorithm = DES.Create();
                    ICryptoTransform transform = algorithm.CreateEncryptor(key, iv);
                    byte[] inputBuffer = Encoding.Unicode.GetBytes(scanTarget.id);
                    byte[] encodedId = transform.TransformFinalBlock(inputBuffer, 0, inputBuffer.Length);
                    byte[] outputWithKey = new byte[16 + 1 + encodedId.Length];
                    outputWithKey[0] = 0; // First byte is version.
                    Buffer.BlockCopy(key, 0, outputWithKey, 1, 8);
                    Buffer.BlockCopy(iv, 0, outputWithKey, 9, 8);
                    Buffer.BlockCopy(encodedId, 0, outputWithKey, 17, encodedId.Length);
                    rngCryptoServiceProvider.Dispose();
                    encodedScanTargetId = Convert.ToBase64String(outputWithKey);
                    _encodedScanIds.Add(scanTarget.id, encodedScanTargetId);
                }

                target.ScanTargetIdentifier = encodedScanTargetId;

                return target;
            }).ToList();

            result.Sort((a, b) => a.Centroid.Distance(queryLocation)
                .CompareTo(b.Centroid.Distance(queryLocation))
            );

            return new ScanTargetResult(result);
        }
    }
}
