// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Textures;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

using Input = Niantic.Lightship.AR.Input;

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

        private Texture2D _gpuImageTex;
        private Texture2D _gpuDepthImageTex;
        private Texture2D _gpuDepthConfidenceTex;

        private const float DefaultAccuracyMeters = 0.01f;
        private const float DefaultDistanceMeters = 0.01f;

        private bool _autoEnabledLocationServices;
        private bool _autoEnabledCompass;

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
            DestroyTexture(_gpuImageTex);
            DestroyTexture(_gpuDepthImageTex);
            DestroyTexture(_gpuDepthConfidenceTex);

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
        }

        private void DestroyTexture(Texture2D tex)
        {
            if (tex != null)
            {
                if (Application.isPlaying)
                {
                    GameObject.Destroy(tex);
                }
                else
                {
                    GameObject.DestroyImmediate(tex);
                }
            }
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

        public override bool TryGetCameraIntrinsics(out XRCameraIntrinsics intrinsics)
        {
            return _cameraSubsystem.TryGetIntrinsics(out intrinsics);
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

        public override bool TryGetCameraFrame(out XRCameraFrame frame)
        {
            // Create params with dummy screen data (doesn't impact the texture descriptors or timestamp, just the
            // projection and display matrices).
            XRCameraParams emptyParams = new XRCameraParams
            {
                zNear = 0,
                zFar = 1,
                screenWidth = 1,
                screenHeight = 1,
                screenOrientation = Screen.orientation
            };

            return _cameraSubsystem.TryGetLatestFrame(emptyParams, out frame);
        }

        /// Returns the camera pose
        public override bool TryGetCameraPose(out Matrix4x4 pose)
        {
            return InputReader.TryGetPose(out pose);
        }

        /// Will return the latest XRCpuImage acquired through the XRCameraSubsystem. The returned image can be invalid,
        /// for example because the session startup was not completed. XRCpuImages must be disposed by the consumer.
        public override bool TryGetCpuImage(out XRCpuImage cpuImage)
        {
            return _cameraSubsystem.TryAcquireLatestCpuImage(out cpuImage);
        }

        public override bool TryGetGpuImage(out Texture2D gpuImage)
        {
            var descriptors = _cameraSubsystem.GetTextureDescriptors(Allocator.Temp);

            if (descriptors.Length == 0)
            {
                gpuImage = null;
                return false;
            }

            // TODO: ARKit returns two textures (Y and CbCr) while ARCore and Playback returns just one
            var descriptor = descriptors[0];

            if (_gpuImageTex != null)
            {
                _gpuImageTex.UpdateExternalTexture(descriptor.nativeTexture);
            }
            else
            {
                _gpuImageTex = ExternalTextureUtils.CreateExternalTexture2D(descriptor);
            }

            gpuImage = _gpuImageTex;
            return true;
        }

        public override bool TryGetCpuDepthImage(out XRCpuImage cpuDepthImage, out XRCpuImage cpuDepthConfidenceImage)
        {
            if (_usingLightshipOcclusion)
            {
                cpuDepthImage = default ;
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

        public override bool TryGetGpuDepthImage(out Texture2D gpuDepthImage, out Texture2D gpuDepthConfidenceImage)
        {
            if (_usingLightshipOcclusion)
            {
                gpuDepthImage = null;
                gpuDepthConfidenceImage = null;
                return false;
            }

            var descriptors = _occlusionSubsystem.GetTextureDescriptors(Allocator.Temp);
            var confidenceDescriptor = new XRTextureDescriptor();

            // Have to add a try catch here, otherwise the code will crash when underlined
            // subsystem does not support the TryGetEnvironmentDepthConfidence().
            try
            {
              _occlusionSubsystem.TryGetEnvironmentDepthConfidence(out confidenceDescriptor);
            }
            catch (Exception)
            {
                gpuDepthImage = null;
                gpuDepthConfidenceImage = null;
                return false;
            }

            if (descriptors.Length == 0 || !descriptors[0].valid || !confidenceDescriptor.valid)
            {
                gpuDepthImage = null;
                gpuDepthConfidenceImage = null;
                return false;
            }

            var descriptor = descriptors[0];

            if (_gpuDepthImageTex != null)
            {
                _gpuDepthImageTex.UpdateExternalTexture(descriptor.nativeTexture);
            }
            else
            {
                _gpuDepthImageTex = ExternalTextureUtils.CreateExternalTexture2D(descriptor);
            }

            if (_gpuDepthConfidenceTex != null)
            {
                _gpuDepthConfidenceTex.UpdateExternalTexture(confidenceDescriptor.nativeTexture);
            }
            else
            {
                _gpuDepthConfidenceTex = ExternalTextureUtils.CreateExternalTexture2D(confidenceDescriptor);
            }

            gpuDepthImage = _gpuDepthImageTex;
            gpuDepthConfidenceImage = _gpuDepthConfidenceTex;

            return true;
        }

        public override bool TryGetGpsLocation(out GpsLocation gps)
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
            gps.padding = 0;
            return true;
        }

        public override bool TryGetCompass(out CompassData compass)
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
        private void TryStartLocationService()
        {
            if (!Input.location.isEnabledByUser)
            {
                // Cannot Start if Location Permissions have not been granted
                return;
            }

            if (Input.location.status == LocationServiceStatus.Initializing || Input.location.status == LocationServiceStatus.Running)
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
