// Copyright 2022-2024 Niantic.

using System;
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

        private bool _usingLightshipOcclusion;
        private bool _locationServiceNeedsToStart = false;

        // Textures
        private Texture2D _gpuImageTex;
        private Texture2D _gpuDepthImageTex;
        private Texture2D _gpuDepthConfidenceTex;

        // Descriptors
        private XRTextureDescriptor _gpuImageDescriptor;

        private const float DefaultAccuracyMeters = 0.01f;
        private const float DefaultDistanceMeters = 0.01f;

        private bool _autoEnabledLocationServices;
        private bool _autoEnabledCompass;

        private XRCpuImage _cpuImage;
        private XRCpuImage _depthImage;
        private XRCpuImage _depthConfidenceImage;

        public override bool TryToBeReady()
        {
            if (_sessionSubsystem == null)
            {
                SetupSubsystemReferences();
            }

            return _sessionSubsystem != null;
        }

        public SubsystemsDataAcquirer()
        {
            SetupSubsystemReferences();
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
        protected void SetupSubsystemReferences()
        {
            // Query the currently active loader for the created subsystem, if one exists.
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                var loader = XRGeneralSettings.Instance.Manager.activeLoader;
                if (loader != null)
                {
                    _cameraSubsystem = loader.GetLoadedSubsystem<XRCameraSubsystem>();
                    _sessionSubsystem = loader.GetLoadedSubsystem<XRSessionSubsystem>();
                    _occlusionSubsystem = loader.GetLoadedSubsystem<XROcclusionSubsystem>();
                    _usingLightshipOcclusion = _occlusionSubsystem is LightshipOcclusionSubsystem;
                }
            }
        }

        public override bool TryGetCameraIntrinsicsDeprecated(out XRCameraIntrinsics intrinsics)
        {
            return _cameraSubsystem.TryGetIntrinsics(out intrinsics);
        }

        public override bool TryGetCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct intrinsics)
        {
            intrinsics = default;
            if (_cameraSubsystem.TryGetIntrinsics(out var xrCameraIntrinsics))
            {
                intrinsics.SetIntrinsics(xrCameraIntrinsics.focalLength, xrCameraIntrinsics.principalPoint);
                return true;
            }

            return false;
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

        public override bool TryGetCameraFrameDeprecated(out XRCameraFrame frame)
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

            return _cameraSubsystem.TryGetLatestFrame(emptyParams, out frame);
        }

        public override bool TryGetCameraTimestampMs(out ulong timestampMs)
        {
            if (TryGetCameraFrameDeprecated(out var frame))
            {
                timestampMs = (ulong)frame.timestampNs / 1_000_000;
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

        /// Will return the latest XRCpuImage acquired through the XRCameraSubsystem. The returned image can be invalid,
        /// for example because the session startup was not completed. XRCpuImages must be disposed by the consumer.
        public override bool TryGetCpuImageDeprecated(out XRCpuImage cpuImage)
        {
            return _cameraSubsystem.TryAcquireLatestCpuImage(out cpuImage);
        }

        public override bool TryGetCpuDepthImageDeprecated(out XRCpuImage cpuDepthImage, out XRCpuImage cpuDepthConfidenceImage)
        {
            if (_usingLightshipOcclusion)
            {
                cpuDepthImage = default;
                cpuDepthConfidenceImage = default;
                return false;
            }

            var gotDepth = _occlusionSubsystem.TryAcquireRawEnvironmentDepthCpuImage(out cpuDepthImage);

            bool gotConfidence = false;
            if (gotDepth)
            {
                gotConfidence =
                    _occlusionSubsystem.TryAcquireEnvironmentDepthConfidenceCpuImage(out cpuDepthConfidenceImage);
            }
            else
            {
                cpuDepthConfidenceImage = default;
            }

            return gotDepth && gotConfidence;
        }
        public override bool TryGetLightshipCpuImage(out LightshipCpuImage cpuImage)
        {
            cpuImage = new LightshipCpuImage();
            _cpuImage.Dispose();
            return TryGetCpuImageDeprecated(out _cpuImage) || cpuImage.FromXRCpuImage(_cpuImage);
        }

        public override bool TryGetLightshipCpuDepthImage(out LightshipCpuImage cpuDepthImage,
            out LightshipCpuImage cpuDepthConfidenceImage)
        {
            cpuDepthImage = new LightshipCpuImage();
            cpuDepthConfidenceImage = new LightshipCpuImage();

            _depthImage.Dispose();
            _depthConfidenceImage.Dispose();
            return TryGetCpuDepthImageDeprecated(out _depthImage, out _depthConfidenceImage) ||
                cpuDepthImage.FromXRCpuImage(_depthImage) ||
                cpuDepthConfidenceImage.FromXRCpuImage(_depthConfidenceImage);
        }

        public override bool TryGetGpsLocation(out GpsLocationCStruct gps)
        {
            if (_locationServiceNeedsToStart)
                TryStartLocationService();

            if (Input.location.status != LocationServiceStatus.Running)
            {
                gps = default;
                return false;
            }

            gps.TimestampMs = (UInt64)(Input.location.lastData.timestamp * 1000);
            gps.Latitude = Input.location.lastData.latitude;
            gps.Longitude = Input.location.lastData.longitude;
            gps.Altitude = Input.location.lastData.altitude;
            gps.HorizontalAccuracy = Input.location.lastData.horizontalAccuracy;
            gps.VerticalAccuracy = Input.location.lastData.verticalAccuracy;
            return true;
        }

        public override bool TryGetCompass(out CompassDataCStruct compass)
        {
            if (_locationServiceNeedsToStart)
                TryStartLocationService();

            if (Input.location.status != LocationServiceStatus.Running)
            {
                compass = default;
                return false;
            }

            compass.TimestampMs = (UInt64)(Input.compass.timestamp * 1000);
            compass.HeadingAccuracy = Input.compass.headingAccuracy;
            compass.MagneticHeading = Input.compass.magneticHeading;
            compass.RawDataX = Input.compass.rawVector.x;
            compass.RawDataY = Input.compass.rawVector.y;
            compass.RawDataZ = Input.compass.rawVector.z;
            compass.TrueHeading = Input.compass.trueHeading;
            return true;
        }

        public override void OnFormatAdded(DataFormat formatAdded)
        {
            switch (formatAdded)
            {
                case DataFormat.kGpsLocation:
                    if (Input.location.status == LocationServiceStatus.Stopped)
                    {
                        RequestLocationPermissions();
                    }
                    break;
                case DataFormat.kCompass:
                    if (Input.location.status == LocationServiceStatus.Stopped)
                    {
                        // Lazy start of location services with compass.
                        EnableCompass();
                        RequestLocationPermissions();
                    }

                    if (Input.compass.enabled == false)
                    {
                        // We shouldn't need to restart the location service, but simply
                        // enable the compass.
                        EnableCompass();
                    }
                    break;
            }
        }

        public override void OnFormatRemoved(DataFormat formatRemoved)
        {
            switch (formatRemoved)
            {
                case DataFormat.kGpsLocation:
                    // We don't want to stop the location service in case the developer needs it for other non-lightship functionality
                    break;

                case DataFormat.kCompass:
                    // We don't want to stop the location service in case the developer needs it for other non-lightship functionality
                    break;
            }
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
