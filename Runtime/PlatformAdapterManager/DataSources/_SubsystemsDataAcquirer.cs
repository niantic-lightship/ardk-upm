using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Playback;
using Niantic.Lightship.AR.Utilities.CTrace;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    // This implementation connects to data sources on an actual device running an ARSession.
    internal class _SubsystemsDataAcquirer : _PlatformDataAcquirer
    {
        private XRSessionSubsystem _sessionSubsystem;
        private XRCameraSubsystem _cameraSubsystem;
        private XROcclusionSubsystem _occlusionSubsystem;
        private XRInputSubsystem _inputSubsystem;

        private bool _usingLightshipOcclusion;

        protected InputDevice? _inputDevice;
        private Resolution? _cachedImageResolution;

        private Texture2D _gpuImageTex;
        private Texture2D _gpuDepthImageTex;
        private Texture2D _gpuDepthConfidenceTex;

        internal const float _DefaultAccuracyMeters = 0.01f;
        internal const float _DefaultDistanceMeters = 0.01f;

        ~_SubsystemsDataAcquirer()
        {
            Input.compass.enabled = false;
            Input.location.Stop();
        }

        public override bool TryToBeReady()
        {
            if (_sessionSubsystem == null)
                SetupSubsystemReferences();

            return _sessionSubsystem != null;
        }

        public _SubsystemsDataAcquirer()
        {
            SetupSubsystemReferences();
            InputDevices.deviceConnected += OnInputDeviceConnected;
        }

        public override void Dispose()
        {
            InputDevices.deviceConnected -= OnInputDeviceConnected;
            DestroyTexture(_gpuImageTex);
            DestroyTexture(_gpuDepthImageTex);
            DestroyTexture(_gpuDepthConfidenceTex);
        }



        private void DestroyTexture(Texture2D tex)
        {
            if (tex != null)
            {
                if (Application.isPlaying)
                    GameObject.Destroy(tex);
                else
                    GameObject.DestroyImmediate(tex);
            }
        }

        private void OnInputDeviceConnected(InputDevice device)
        {
            Debug.Log($"Input device detected with name {device.name}");
            CheckConnectedDevice(device);
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
                    _usingLightshipOcclusion = _occlusionSubsystem is Niantic.Lightship.AR.LightshipOcclusionSubsystem;

                    List<InputDevice> devices = new List<InputDevice>();
                    InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.TrackedDevice, devices);
                    foreach (var device in devices)
                    {
                        CheckConnectedDevice(device, false);
                    }

                    if (null != loader.GetLoadedSubsystem<XRPersistentAnchorSubsystem>())
                    {
                        Input.location.Start(_DefaultAccuracyMeters, _DefaultDistanceMeters);
                    }
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

        public override DeviceOrientation GetDeviceOrientation()
        {
            return Input.deviceOrientation;
        }

        protected virtual ScreenOrientation GetScreenOrientation()
        {
            return Screen.orientation;
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

            if (!_cameraSubsystem.TryGetLatestFrame(emptyParams, out frame))
            {
                Debug.LogWarning("Failed to get camera frame. Is an XRCameraSubsystem present?");
                return false;
            }

            return true;
        }

        /// Returns the camera pose
        /// Note:
        ///     ARKit returns (presumably) best guess poses even when tracking state is Limited (i.e. sliding the
        ///     phone with the camera face down will change the translation value). However, ARCore "freezes"
        ///     the device pose while the tracking state is Limited.
        public override bool TryGetCameraPose(out Matrix4x4 pose)
        {
            if (_inputDevice != null &&
                GetPositionAndRotation(_inputDevice.Value, out Vector3 position, out Quaternion rotation))
            {
                var displayToLocal = Matrix4x4.TRS(position, rotation, Vector3.one);
                var screenOrientation = GetScreenOrientation();
                var cameraToDisplay = Matrix4x4.Rotate(_CameraMath.CameraToDisplayRotation(screenOrientation));

                var cameraToLocal = displayToLocal * cameraToDisplay;
                pose = cameraToLocal;
                return true;
            }

            pose = Matrix4x4.zero;
            return false;
        }

        public override bool TryGetImageResolution(out Resolution resolution)
        {
            // Check cache
            if (_cachedImageResolution.HasValue)
            {
                resolution = _cachedImageResolution.Value;
                return true;
            }

            int width = 0;
            int height = 0;
            var descriptors = _cameraSubsystem.GetTextureDescriptors(Allocator.Temp);
            if (descriptors.Length > 0)
            {
                // Use the size of the largest image plane
                var size = 0;
                for (var i = 0; i < descriptors.Length; i++)
                {
                    var plane = descriptors[i];
                    var planeSize = plane.width * plane.height;
                    if (planeSize > size)
                    {
                        size = planeSize;
                        width = plane.width;
                        height = plane.height;
                    }
                }
            }
            descriptors.Dispose();

            if (width > 0 && height > 0)
            {
                // Extract and cache resolution
                resolution = new Resolution {width = width, height = height};
                _cachedImageResolution = resolution;
                return true;
            }

            resolution = default;
            return false;
        }

        private void CheckConnectedDevice(InputDevice device, bool displayWarning = true)
        {
            if (GetPositionAndRotation(device, out Vector3 p, out Quaternion r))
            {
                if (_inputDevice == null)
                {
                    _inputDevice = device;
                }
                else if (displayWarning)
                {
                    Debug.LogWarning
                    (
                        $"An input device {device.name} with the TrackedDevice characteristic was registered but " +
                        $"the {nameof(_SubsystemsDataAcquirer)} is already consuming data from {_inputDevice.Value.name}."
                    );
                }
            }
        }

        private bool GetPositionAndRotation(InputDevice device, out Vector3 position, out Quaternion rotation)
        {
            var positionSuccess = false;
            var rotationSuccess = false;
            if (!(positionSuccess = device.TryGetFeatureValue(CommonUsages.centerEyePosition, out position)))
                positionSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraPosition, out position);
            if (!(rotationSuccess = device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rotation)))
                rotationSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraRotation, out rotation);

            return positionSuccess && rotationSuccess;
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

            // Cache the image resolution
            if (!_cachedImageResolution.HasValue && descriptor.width > 0 && descriptor.height > 0)
                _cachedImageResolution = new Resolution {width = descriptor.width, height = descriptor.height};

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
            try {
              _occlusionSubsystem.TryGetEnvironmentDepthConfidence(out confidenceDescriptor);
            } catch (Exception e) {
                gpuDepthImage = null;
                gpuDepthConfidenceImage = null;
                return false;
            }
            if (descriptors.Length == 0 || !confidenceDescriptor.valid)
            {
                gpuDepthImage = null;
                gpuDepthConfidenceImage = null;
                return false;
            }

            var descriptor = descriptors[0];

            if (_gpuDepthImageTex != null)
                _gpuDepthImageTex.UpdateExternalTexture(descriptor.nativeTexture);
            else
                _gpuDepthImageTex = ExternalTextureUtils.CreateExternalTexture2D(descriptor);

            if (_gpuDepthConfidenceTex != null)
                _gpuDepthConfidenceTex.UpdateExternalTexture(confidenceDescriptor.nativeTexture);
            else
                _gpuDepthConfidenceTex = ExternalTextureUtils.CreateExternalTexture2D(confidenceDescriptor);

            gpuDepthImage = _gpuDepthImageTex;
            gpuDepthConfidenceImage = _gpuDepthConfidenceTex;

            return true;
        }

        public override bool TryGetGpsLocation(out GpsLocation gps)
        {
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                // Lazy start of location services
                Input.location.Start(_DefaultAccuracyMeters, _DefaultDistanceMeters);
                gps = default;
                return false;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                gps = default;
                return false;
            }

            gps.TimestampMs = (UInt64)Input.location.lastData.timestamp * 1000;
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
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                // Lazy start of location services with compass.
                Input.compass.enabled = true;
                Input.location.Start(_DefaultAccuracyMeters, _DefaultDistanceMeters);
                compass = default;
                return false;
            }

            if (Input.compass.enabled == false)
            {
                // We shouldn't need to restart the location service, but simply
                // enable the compass.
                Input.compass.enabled = true;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                compass = default;
                return false;
            }

            compass.TimestampMs = (UInt64)Input.compass.timestamp * 1000;
            compass.HeadingAccuracy = Input.compass.headingAccuracy;
            compass.MagneticHeading = Input.compass.magneticHeading;
            compass.RawDataX = Input.compass.rawVector.x;
            compass.RawDataY = Input.compass.rawVector.y;
            compass.RawDataZ = Input.compass.rawVector.z;
            compass.TrueHeading = Input.compass.trueHeading;
            return true;
        }
    }
}
