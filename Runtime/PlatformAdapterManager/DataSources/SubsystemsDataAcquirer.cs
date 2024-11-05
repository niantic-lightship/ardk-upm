// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using Niantic.Lightship.AR.Utilities;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Niantic.Lightship.AR.PAM
{
    // This implementation connects to data sources on an actual device running an ARSession.
    internal class SubsystemsDataAcquirer : PlatformDataAcquirer
    {
        private XRSessionSubsystem _sessionSubsystem;
        private XRCameraSubsystem _cameraSubsystem;
        private XROcclusionSubsystem _occlusionSubsystem;

        // Textures
        private Texture2D _gpuImageTex;
        private Texture2D _gpuDepthImageTex;
        private Texture2D _gpuDepthConfidenceTex;

        // CPU images
        private XRCpuImage _cpuImage;
        private XRCpuImage _depthImage;
        private XRCpuImage _depthConfidenceImage;

        // Descriptors
        private XRTextureDescriptor _gpuImageDescriptor;

        private const float DefaultAccuracyMeters = 0.01f;
        private const float DefaultDistanceMeters = 0.01f;

        private bool _autoEnabledLocationServices;
        private bool _autoEnabledCompass;
        private bool _usingLightshipOcclusion;
        private bool _locationServiceNeedsToStart;

        /// <summary>
        /// Indicates whether all required subsystems have been loaded.
        /// </summary>
        protected virtual bool DidLoadSubsystems
        {
            get { return _sessionSubsystem != null; }
        }

        public override bool TryToBeReady()
        {
            if (!DidLoadSubsystems)
            {
                AcquireSubsystemReferences();
            }

            return DidLoadSubsystems;
        }

        public SubsystemsDataAcquirer()
        {
            AcquireSubsystemReferences();
        }

        public override void Dispose()
        {
            // Release textures
            UnityObjectUtils.Destroy(_gpuImageTex);
            UnityObjectUtils.Destroy(_gpuDepthImageTex);
            UnityObjectUtils.Destroy(_gpuDepthConfidenceTex);

            // Reset the descriptors
            _gpuImageDescriptor.Reset();

            if (_autoEnabledLocationServices)
            {
                Log.Info
                (
                    "ARDK enabled location services because they were required by an ARDK feature. " +
                    "ARDK is now shutting down. If location services are no longer required, they must be " +
                    "separately disabled."
                );
            }

            if (_autoEnabledCompass)
            {
                Log.Info
                (
                    "ARDK enabled the compass because it was required by an ARDK feature. " +
                    "ARDK is now shutting down. If the compass data is no longer required, it must be " +
                    "separately disabled."
                );
            }

            _cpuImage.Dispose();
            _depthImage.Dispose();
            _depthConfidenceImage.Dispose();
        }

        // Uses the XRGeneralSettings.instance singleton to connect to all subsystem references.
        private void AcquireSubsystemReferences()
        {
            // Query the currently active loader for the created subsystem, if one exists.
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                var loader = XRGeneralSettings.Instance.Manager.activeLoader;
                if (loader != null)
                {
                    OnAcquireSubsystems(loader);
                }
            }
        }

        /// <summary>
        /// Invoked when it is time to cache the subsystem references from the XRLoader.
        /// </summary>
        /// <param name="loader"></param>
        protected virtual void OnAcquireSubsystems(XRLoader loader)
        {
            _sessionSubsystem = loader.GetLoadedSubsystem<XRSessionSubsystem>();
            _cameraSubsystem = loader.GetLoadedSubsystem<XRCameraSubsystem>();
            _occlusionSubsystem = loader.GetLoadedSubsystem<XROcclusionSubsystem>();
            _usingLightshipOcclusion = _occlusionSubsystem is LightshipOcclusionSubsystem;
        }

        public override bool TryGetCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct intrinsics)
        {
            intrinsics = default;
            if (_cameraSubsystem.TryGetIntrinsics(out var xrCameraIntrinsics))
            {
                intrinsics.SetIntrinsics
                (
                    xrCameraIntrinsics.focalLength,
                    xrCameraIntrinsics.principalPoint,
                    xrCameraIntrinsics.resolution
                );

                return true;
            }

            return false;
        }

        public override bool TryGetDepthCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct depthIntrinsics)
        {
            // When both camera and depth images are obtained via ARFoundation's subsystems,
            // the depth intrinsics are the same as the camera intrinsics, scaled to the depth resolution.
            return TryGetCameraIntrinsicsCStruct(out depthIntrinsics);
        }

        /// Returns the XRSessionSubsystem's reported tracking state.
        /// Note:
        ///     In both ARKit and ARCore, tracking state is None for just the first frame and then changes to Limited,
        ///     even if the phone is lying face down. Once SLAM achieves localization, the state becomes Tracking.
        ///     Afterwards, tracking state is never None again, but turns to Limited in situations like if the
        ///     camera can see no feature points, moves too quickly, etc.
        public override TrackingState GetTrackingState()
        {
            return _sessionSubsystem.trackingState;
        }

        public override ScreenOrientation GetScreenOrientation()
        {
            return XRDisplayContext.GetScreenOrientation();
        }

        public override bool TryGetCameraTimestampMs(out double timestampMs)
        {
            // Create params with dummy screen data (doesn't impact the texture descriptors or timestamp, just the
            // projection and display matrices).
            XRCameraParams emptyParams = new XRCameraParams
            {
                zNear = 0,
                zFar = 1,
                screenWidth = 1,
                screenHeight = 1,
                screenOrientation = GetScreenOrientation()
            };

            if (_cameraSubsystem.TryGetLatestFrame(emptyParams, out var frame))
            {
                timestampMs = (double)frame.timestampNs / 1_000_000;
                return true;
            }

            timestampMs = 0;
            return false;
        }

        /// Returns the camera pose
        public override bool TryGetCameraPose(out Matrix4x4 pose)
        {
            return InputReader.TryGetPose(out pose);
        }

        public override bool TryGetCpuImage(out LightshipCpuImage cpuImage)
        {
            _cpuImage.Dispose(); // TODO(bevangelista) Avoid silently releasing resources on TryGets
            cpuImage = default;

            return _cameraSubsystem.TryAcquireLatestCpuImage(out _cpuImage) &&
                LightshipCpuImage.TryGetFromXRCpuImage(_cpuImage, out cpuImage);
        }

        public override bool TryGetDepthCpuImage
        (
            out LightshipCpuImage depthCpuImage,
            out LightshipCpuImage confidenceCpuImage
        )
        {
            _depthImage.Dispose();              // TODO(bevangelista) Avoid silently releasing resources on TryGets
            _depthConfidenceImage.Dispose();    // TODO(bevangelista) Avoid silently releasing resources on TryGets
            depthCpuImage = default;
            confidenceCpuImage = default;

            if (_usingLightshipOcclusion)
            {
                return false;
            }

            bool hasDepthImage = _occlusionSubsystem.TryAcquireRawEnvironmentDepthCpuImage(out _depthImage);
            if (hasDepthImage)
            {
                hasDepthImage = LightshipCpuImage.TryGetFromXRCpuImage(_depthImage, out depthCpuImage);
                if (hasDepthImage &&
                    _occlusionSubsystem.TryAcquireEnvironmentDepthConfidenceCpuImage(out _depthConfidenceImage))
                {
                    LightshipCpuImage.TryGetFromXRCpuImage(_depthConfidenceImage, out confidenceCpuImage);
                }
            }

            return hasDepthImage;
        }

        public override bool TryGetGpsLocation(out GpsLocationCStruct gps)
        {
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                RequestLocationPermissions();
            }

            if (_locationServiceNeedsToStart)
                TryStartLocationService();

            if (Input.location.status != LocationServiceStatus.Running)
            {
                gps = default;
                return false;
            }

            gps.TimestampMs = (ulong)(Input.location.lastData.timestamp * 1000);
            gps.Latitude = Input.location.lastData.latitude;
            gps.Longitude = Input.location.lastData.longitude;
            gps.Altitude = Input.location.lastData.altitude;
            gps.HorizontalAccuracy = Input.location.lastData.horizontalAccuracy;
            gps.VerticalAccuracy = Input.location.lastData.verticalAccuracy;

            return true;
        }

        public override bool TryGetCompass(out CompassDataCStruct compass)
        {
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                RequestLocationPermissions();
            }

            if (Input.compass.enabled == false)
            {
                EnableCompass();
            }

            if (_locationServiceNeedsToStart)
                TryStartLocationService();

            if (Input.location.status != LocationServiceStatus.Running)
            {
                compass = default;
                return false;
            }

            // The compass.timestamp value on Android will monotonic time since the device was last turned on
            // instead of posix time. We've observed it being in nanoseconds on a wide array of devices
            // even though Unity documentation says seconds. But we've been sending it multiplied by x1000
            // since ARDK 2.x, so we will continue to do that, even on Android, for continuity's sake.
            // On iOS we lose millisecond precision converting naively Input.compass.timestamp to a ulong,
            // so we want the x1000 anyway.
            compass.TimestampMs = (ulong)(Input.compass.timestamp * 1000);

            compass.HeadingAccuracy = Input.compass.headingAccuracy;
            compass.MagneticHeading = Input.compass.magneticHeading;
            compass.RawDataX = Input.compass.rawVector.x;
            compass.RawDataY = Input.compass.rawVector.y;
            compass.RawDataZ = Input.compass.rawVector.z;
            compass.TrueHeading = Input.compass.trueHeading;
            return true;
        }

        /// <summary>
        /// How permissions are requested differs between iOS and Android
        /// </summary>
        private void RequestLocationPermissions()
        {
            Log.Info("Location services are required by an enabled ARDK feature, so ARDK will attempt to enable them.");
            // Android devices require permissions to be granted before starting the Location Service.
            bool startedWithLocationPermission = Input.location.isEnabledByUser;
            if (!startedWithLocationPermission && Application.platform == RuntimePlatform.Android)
            {
#if !UNITY_EDITOR && UNITY_ANDROID
                Permission.RequestUserPermission(Permission.FineLocation);
#endif
                _locationServiceNeedsToStart = true;
                // We will try to start Location Service in TryStartLocationService()
                return;
            }

            // This will trigger Permissions on iOS
            Input.location.Start(DefaultAccuracyMeters, DefaultDistanceMeters);
            _autoEnabledLocationServices = true;
        }

        private void EnableCompass()
        {
            Log.Info("The device's compass is required by an enabled ARDK feature, so it is being turned on.");
            Input.compass.enabled = true;
            _autoEnabledCompass = true;
        }

        /// <summary>
        /// For Android devices, the Location Service can only be stared after Permissions are granted
        /// </summary>
        protected void TryStartLocationService()
        {
            if (!Input.location.isEnabledByUser)
            {
                // Cannot Start if Location Permissions have not been granted
                return;
            }

            if (Input.location.status == LocationServiceStatus.Initializing ||
                Input.location.status == LocationServiceStatus.Running)
            {
                // Start was already called
                _locationServiceNeedsToStart = false;
                return;
            }

            // We can only start the Location Service when it is not running
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                Input.location.Start(DefaultAccuracyMeters, DefaultDistanceMeters);
                _locationServiceNeedsToStart = false;
                return;
            }
        }
    }
}
