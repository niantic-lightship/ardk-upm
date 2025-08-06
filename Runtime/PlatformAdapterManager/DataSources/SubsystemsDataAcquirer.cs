// Copyright 2022-2025 Niantic.

using System;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using Niantic.Lightship.AR.Utilities;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Niantic.Lightship.AR.PAM
{
    // This implementation connects to data sources on an actual device running an ARSession.
    internal class SubsystemsDataAcquirer : PlatformDataAcquirer
    {
        // Subsystem references
        private XRSessionSubsystem _sessionSubsystem;
        private XRCameraSubsystem _cameraSubsystem;
        private XROcclusionSubsystem _occlusionSubsystem;

        /// <summary>
        /// Gets the currently loaded XRSessionSubsystem.
        /// </summary>
        protected XRSessionSubsystem SessionSubsystem => _sessionSubsystem;

        /// <summary>
        /// Gets the currently loaded XRSessionSubsystem.
        /// </summary>
        protected XRCameraSubsystem CameraSubsystem => _cameraSubsystem;

        /// <summary>
        /// Gets the currently loaded XROcclusionSubsystem.
        /// </summary>
        protected XROcclusionSubsystem OcclusionSubsystem => _occlusionSubsystem;

        // Textures
        private Texture2D _gpuImageTex;
        private Texture2D _gpuDepthImageTex;
        private Texture2D _gpuDepthConfidenceTex;

        // CPU images
        private XRCpuImage _cameraCpuImage;
        private XRCpuImage _depthCpuImage;
        private XRCpuImage _depthConfidenceCpuImage;
        private ARSessionState _lastSessionState = ARSessionState.None;

        // Descriptors
        private XRTextureDescriptor _gpuImageDescriptor;

        private const float DefaultAccuracyMeters = 0.01f;
        private const float DefaultDistanceMeters = 0.01f;

        private bool _requestedLocationPermissions;
        private bool _autoEnabledLocationServices;
        private bool _autoEnabledCompass;
        private bool _usingLightshipOcclusion;

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
                TryAcquireSubsystemReferences();
            }

            return DidLoadSubsystems;
        }

        public SubsystemsDataAcquirer()
        {
            TryAcquireSubsystemReferences();
            ARSession.stateChanged += OnSessionStateChanged;
        }

        public override void Dispose()
        {
            ARSession.stateChanged -= OnSessionStateChanged;

            // Release textures
            UnityObjectUtils.Destroy(_gpuImageTex);
            UnityObjectUtils.Destroy(_gpuDepthImageTex);
            UnityObjectUtils.Destroy(_gpuDepthConfidenceTex);

            // Reset the descriptors
            _gpuImageDescriptor.Reset();

            _requestedLocationPermissions = false;
            MonoBehaviourEventDispatcher.LateUpdating.RemoveListener(IOSPermissionRequestCheck);

            if (_autoEnabledLocationServices && Input.location.status == LocationServiceStatus.Running)
            {
                Log.Info
                (
                    "ARDK enabled location services because they were required by an ARDK feature. " +
                    "ARDK is now shutting down. If location services are no longer required, they must be " +
                    "separately disabled."
                );
            }

            if (_autoEnabledCompass && Input.compass.enabled)
            {
                Log.Info
                (
                    "ARDK enabled the compass because it was required by an ARDK feature. " +
                    "ARDK is now shutting down. If the compass data is no longer required, it must be " +
                    "separately disabled."
                );
            }

            _cameraCpuImage.Dispose();
            _depthCpuImage.Dispose();
            _depthConfidenceCpuImage.Dispose();
        }

        // Uses the XRGeneralSettings.instance singleton to connect to all subsystem references.
        private void TryAcquireSubsystemReferences()
        {
            // Query the currently active loader for the created subsystem, if one exists.
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                var loader = XRGeneralSettings.Instance.Manager.activeLoader;
                if (loader != null)
                {
                    OnSubsystemsLoaded(loader);
                }
            }
        }

        /// <summary>
        /// Invoked when it is time to cache the subsystem references from the XRLoader.
        /// </summary>
        /// <param name="loader"></param>
        protected virtual void OnSubsystemsLoaded(XRLoader loader)
        {
            _sessionSubsystem = loader.GetLoadedSubsystem<XRSessionSubsystem>();
            _cameraSubsystem = loader.GetLoadedSubsystem<XRCameraSubsystem>();
            _occlusionSubsystem = loader.GetLoadedSubsystem<XROcclusionSubsystem>();
            _usingLightshipOcclusion = _occlusionSubsystem is LightshipOcclusionSubsystem;
        }

        // See LightshipARCoreLoader.UpgradeCameraConfigurationIfNeeded Note #2 for why we need this logic
        private void OnSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            if (_lastSessionState == ARSessionState.SessionTracking && args.state == ARSessionState.SessionInitializing)
            {
                Log.Info("ARSession was reset. ARDK functionality will be limited until tracking is re-established.");

                _cameraCpuImage.Dispose();
            }

            _lastSessionState = args.state;
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

        // Default depth extrinsics equals camera pose for platforms that do not provide separate depth sensor
        public override bool TryGetDepthPose(out Matrix4x4 extrinsics)
        {
            return TryGetCameraPose(out extrinsics);
        }

        public override bool TryGetCpuImage(out LightshipCpuImage cpuImage)
        {
            _cameraCpuImage.Dispose(); // TODO(bevangelista) Avoid silently releasing resources on TryGets
            cpuImage = default;

            return _cameraSubsystem.TryAcquireLatestCpuImage(out _cameraCpuImage) &&
                LightshipCpuImage.TryGetFromXRCpuImage(_cameraCpuImage, out cpuImage);
        }

        public override bool TryGetDepthCpuImage
        (
            out LightshipCpuImage depthCpuImage,
            out LightshipCpuImage confidenceCpuImage
        )
        {
            _depthCpuImage.Dispose();              // TODO(bevangelista) Avoid silently releasing resources on TryGets
            _depthConfidenceCpuImage.Dispose();    // TODO(bevangelista) Avoid silently releasing resources on TryGets
            depthCpuImage = default;
            confidenceCpuImage = default;

            if (_usingLightshipOcclusion)
            {
                return false;
            }

            bool hasDepthImage = _occlusionSubsystem.TryAcquireRawEnvironmentDepthCpuImage(out _depthCpuImage);
            if (hasDepthImage)
            {
                hasDepthImage = LightshipCpuImage.TryGetFromXRCpuImage(_depthCpuImage, out depthCpuImage);
                if (hasDepthImage &&
                    _occlusionSubsystem.TryAcquireEnvironmentDepthConfidenceCpuImage(out _depthConfidenceCpuImage))
                {
                    LightshipCpuImage.TryGetFromXRCpuImage(_depthConfidenceCpuImage, out confidenceCpuImage);
                }
            }

            return hasDepthImage;
        }

        private int _noGpsWarningFramerate = 120;
        public override bool TryGetGpsLocation(out GpsLocationCStruct gps)
        {
            if (Input.location.status == LocationServiceStatus.Stopped ||
                Input.location.status == LocationServiceStatus.Failed)
            {
                if (_requestedLocationPermissions && Time.frameCount % _noGpsWarningFramerate == 0)
                {
                    MissingLocationPermissionLog();    
                }
                else
                {
                    TryStartLocation();
                }
            }

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
            StartCompassIfNeeded();

            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                TryStartLocation();
            }

            // Compass values are invalid if the location service is not running
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

        // Protected because it's invoked from ML2SubsystemsDataAcquirer
        // This method won't ever fail, per se.
        // But enabling the compass won't contain true heading data if the location service is not running.
        protected void StartCompassIfNeeded()
        {
            if (Input.compass.enabled)
            {
                return;
            }

            Log.Info("The device's compass is required by an enabled ARDK feature, so it is being turned on.");
            Input.compass.enabled = true;
            _autoEnabledCompass = true;
        }

        // Protected because it's invoked from ML2SubsystemsDataAcquirer
        protected void TryStartLocation()
        {
            // If the user has previously denied permissions and then enabled them in their device's settings,
            // the app must be restarted for the permissions to take effect.
            if (_requestedLocationPermissions)
            {
                return;
            }

            if (Input.location.status == LocationServiceStatus.Initializing ||
                Input.location.status == LocationServiceStatus.Running)
            {
                return;
            }

            // We can only start the Location Service when it is not running
            if (Input.location.status == LocationServiceStatus.Stopped ||
                Input.location.status == LocationServiceStatus.Failed)
            {
                Log.Info("Location services are required by an enabled ARDK feature, so ARDK will attempt to enable them.");
                _requestedLocationPermissions = true;

                // Starting in Unity 2021, simply starting the location service will prompt the user for permissions
                // on Android devices (same as iOS devices). However, this flow also is bugged when multiple permissions
                // are requested at the same time (see comment below). So we're stuck with this non-elegant solution
                // where we request Android and iOS permission separately.
#if !UNITY_EDITOR && UNITY_ANDROID
                if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                {
                    Permission.RequestUserPermission(Permission.FineLocation);

                    // Ideally, we are using the PermissionCallbacks for everything. However...
                    // We've observed that when multiple permissions are requested at the same time
                    // (i.e. the location permission request here overlaps with the camera permission
                    // request from the ARSession), those callbacks are not invoked.

                    // There's no way to determine if the permission request was denied if the callback
                    // set up above does not return, because the Permission.HasUserAuthorizedPermission
                    // method returns false for both "denied" and "hasn't responded yet." So we'll
                    // print the warning periodically (see TryGetGpsLocation).
                    MonoBehaviourEventDispatcher.Updating.AddListener(AndroidPermissionRequestGrantedCheck);
                }
                else
                {
                    Log.Info("Location permissions were already granted. Starting location services...");
                    StartLocation();
                }
#else
                StartLocation();

                // The iOS permission request doesn't block the thread, and the app doesn't lose focus to the
                // permission request popup right away, so we need to poll for the status of the location service
                // in a future frame
                MonoBehaviourEventDispatcher.Updating.AddListener(IOSPermissionRequestCheck);
#endif
            }
        }

#if UNITY_ANDROID
        private void OnAndroidPermissionDenied(string permissionName)
        {
            MissingLocationPermissionLog();
        }

        private void AndroidPermissionRequestGrantedCheck()
        {
            // As mentioned in TryStartLocation above, because this is requesting multiple permissions at once
            // Input.location.status will return Failed initially. So we need to check for both Stopped and Failed 
            // here once we've confirmed that permission has been granted.
            if ( (Input.location.status == LocationServiceStatus.Stopped || 
                Input.location.status == LocationServiceStatus.Failed) && 
                Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Log.Info("Location permissions granted. Starting location services...");
                StartLocation();

                MonoBehaviourEventDispatcher.Updating.RemoveListener(AndroidPermissionRequestGrantedCheck);
            }            
        }
#endif
        private void IOSPermissionRequestCheck()
        {
            switch (Input.location.status)
            {
                case LocationServiceStatus.Initializing:
                    // Continue the checking
                    return;
                case LocationServiceStatus.Failed:
                    MissingLocationPermissionLog();
                    break;
                case LocationServiceStatus.Running:
                    Log.Info("Location permissions found. Location services are now running.");
                    break;
            }

            MonoBehaviourEventDispatcher.Updating.RemoveListener(IOSPermissionRequestCheck);
        }

        private void MissingLocationPermissionLog()
        {
            Log.Warning
            (
                "Fine location permissions are not currently granted. " +
                "Some ARDK features are limited without location data."
            );
        }

        private void StartLocation()
        {
            _autoEnabledLocationServices = true;
            Input.location.Start(DefaultAccuracyMeters, DefaultDistanceMeters);
        }
    }
}
