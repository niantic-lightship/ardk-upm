// Copyright 2022-2024 Niantic.
using System;
using System.IO;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Utilities.Textures;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.Simulation
{
    /// <summary>
    /// Based on Unity Simulation's SimulationCameraSubsystem and LightshipPlaybackCameraSubsystem.
    /// </summary>
    public sealed class LightshipSimulationCameraSubsystem : XRCameraSubsystem
    {
        private const string SubsystemId = "Lightship-XRSimulation-Camera";

        /// <summary>
        /// The name for the shader for rendering the camera texture.
        /// </summary>
        /// <value>
        /// The name for the shader for rendering the camera texture.
        /// </value>
        // const string k_BackgroundShaderName = "Unlit/Simulation Background Simple";
        private const string BackgroundShaderName = "Unlit/LightshipPlaybackBackground";

        /// <summary>
        /// The shader property name for the simple RGB component of the camera video frame.
        /// </summary>
        /// <value>
        /// The shader property name for the  simple RGB component of the camera video frame.
        /// </value>
        private const string TextureSinglePropertyName = "_CameraTex";

        /// <summary>
        /// The shader property name identifier for the simple RGB component of the camera video frame.
        /// </summary>
        /// <value>
        /// The shader property name identifier for the simple RGB component of the camera video frame.
        /// </value>
        internal static readonly int s_textureSinglePropertyNameId = Shader.PropertyToID(TextureSinglePropertyName);

        private class LightshipSimulationProvider : Provider
        {
            private LightshipCameraTextureFrameEventArgs _cameraTextureFrameEventArgs;
            private LightshipSimulationRgbCameraTextureProvider _simulationRgbCameraTextureProvider;
            private Camera _camera;
            private Material _cameraMaterial;
            private XRCameraConfiguration _xrCameraConfiguration;

            private XRSupportedCameraBackgroundRenderingMode _requestedBackgroundRenderingMode = XRSupportedCameraBackgroundRenderingMode.BeforeOpaques;

            public override XRCpuImage.Api cpuImageApi { get; } = LightshipCpuImageApi.Instance;

            public override Feature currentCamera => Feature.WorldFacingCamera;

            public override XRCameraConfiguration? currentConfiguration
            {
                get => _xrCameraConfiguration;
                set
                {
                    // Currently assuming any not null configuration is valid for simulation
                    if (value == null)
                        throw new ArgumentNullException("value", "cannot set the camera configuration to null");

                    _xrCameraConfiguration = (XRCameraConfiguration)value;
                }
            }

            public override Material cameraMaterial => _cameraMaterial;

            public override bool permissionGranted => true;

            public override XRSupportedCameraBackgroundRenderingMode requestedBackgroundRenderingMode
            {
                get => _requestedBackgroundRenderingMode;
                set => _requestedBackgroundRenderingMode = value;
            }

            public override XRCameraBackgroundRenderingMode currentBackgroundRenderingMode
            {
                get
                {
                    switch (requestedBackgroundRenderingMode)
                    {
                        case XRSupportedCameraBackgroundRenderingMode.AfterOpaques:
                            return XRCameraBackgroundRenderingMode.AfterOpaques;
                        case XRSupportedCameraBackgroundRenderingMode.BeforeOpaques:
                        case XRSupportedCameraBackgroundRenderingMode.Any:
                            return XRCameraBackgroundRenderingMode.BeforeOpaques;
                        default:
                            return XRCameraBackgroundRenderingMode.None;
                    }
                }
            }

            public override XRSupportedCameraBackgroundRenderingMode supportedBackgroundRenderingMode => XRSupportedCameraBackgroundRenderingMode.Any;

            public LightshipSimulationProvider()
            {
                var backgroundShader = Shader.Find(BackgroundShaderName);

                if (backgroundShader == null)
                {
                   Log.Error("Cannot create camera background material compatible with the render pipeline");
                }
                else
                {
                    _cameraMaterial = CreateCameraMaterial(BackgroundShaderName);
                }
            }

            public override void Start()
            {
                var xrOrigin = Object.FindObjectOfType<XROrigin>();
                if (xrOrigin == null)
                    throw new NullReferenceException("No XROrigin found.");

                var xrCamera = xrOrigin.Camera;
                if (xrCamera == null)
                    throw new NullReferenceException("No camera found under XROrigin.");

                _camera = LightshipSimulationDevice.GetOrCreateSimulationCamera().RgbCamera;

                _simulationRgbCameraTextureProvider = LightshipSimulationRgbCameraTextureProvider.AddTextureProviderToCamera(_camera, xrCamera);
                _simulationRgbCameraTextureProvider.FrameReceived += FrameReceived;
                if (_simulationRgbCameraTextureProvider != null && _simulationRgbCameraTextureProvider.CameraFrameEventArgs != null)
                    _cameraTextureFrameEventArgs = (LightshipCameraTextureFrameEventArgs)_simulationRgbCameraTextureProvider.CameraFrameEventArgs;

                _xrCameraConfiguration = new XRCameraConfiguration(IntPtr.Zero, new Vector2Int(_camera.pixelWidth, _camera.pixelHeight));
            }

            public override void Stop()
            {
                if (_simulationRgbCameraTextureProvider != null)
                    _simulationRgbCameraTextureProvider.FrameReceived -= FrameReceived;
            }

            public override void Destroy()
            {
                if (_simulationRgbCameraTextureProvider != null)
                {
                    Object.Destroy(_simulationRgbCameraTextureProvider.gameObject);
                    _simulationRgbCameraTextureProvider = null;
                }
            }

            public override NativeArray<XRCameraConfiguration> GetConfigurations(XRCameraConfiguration defaultCameraConfiguration, Allocator allocator)
            {
                var configs = new NativeArray<XRCameraConfiguration>(1, allocator);
                configs[0] = _xrCameraConfiguration;
                return configs;
            }

            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors(XRTextureDescriptor defaultDescriptor, Allocator allocator)
            {
                if (_simulationRgbCameraTextureProvider != null && _simulationRgbCameraTextureProvider.TryGetTextureDescriptors(out var descriptors, allocator))
                {
                    return descriptors;
                }

                return base.GetTextureDescriptors(defaultDescriptor, allocator);
            }

            public override bool TryAcquireLatestCpuImage(out XRCpuImage.Cinfo cameraImageCinfo)
            {
                if (_cameraTextureFrameEventArgs.Texture == null)
                {
                    cameraImageCinfo = default;
                    return false;
                }

                Texture2D temp = new Texture2D(_cameraTextureFrameEventArgs.Texture.width, _cameraTextureFrameEventArgs.Texture.height, _cameraTextureFrameEventArgs.Texture.format, mipChain: false);
                ExternalTextureUtils.ReadFromExternalTexture(_cameraTextureFrameEventArgs.Texture, temp);
                IntPtr dataPtr;
                unsafe
                {
                    dataPtr = (IntPtr) temp.GetRawTextureData<byte>().GetUnsafeReadOnlyPtr();
                }

                var lightshipCpuImageApi = (LightshipCpuImageApi)cpuImageApi;

                var gotCpuImage = lightshipCpuImageApi.TryAddManagedXRCpuImage
                (
                    dataPtr,
                    temp.width * temp.height * temp.format.BytesPerPixel(),
                    temp.width,
                    temp.height,
                    temp.format,
                    timestampMs: (ulong)_cameraTextureFrameEventArgs.TimestampNs,
                    out cameraImageCinfo
                );

                return gotCpuImage;
            }

            private void FrameReceived(LightshipCameraTextureFrameEventArgs args)
            {
                _cameraTextureFrameEventArgs = args;
            }

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame)
            {
                if (_simulationRgbCameraTextureProvider == null)
                {
                    cameraFrame = new XRCameraFrame();
                    return false;
                }

                XRCameraFrameProperties properties = 0;

                long timeStamp = default;
                float averageBrightness = default;
                float averageColorTemperature = default;
                Color colorCorrection = default;
                Matrix4x4 projectionMatrix = default;
                Matrix4x4 displayMatrix = default;
                TrackingState trackingState = TrackingState.Tracking;
                IntPtr nativePtr = default;
                float averageIntensityInLumens = default;
                double exposureDuration = default;
                float exposureOffset = default;
                float mainLightIntensityInLumens = default;
                Color mainLightColor = default;
                Vector3 mainLightDirection = default;
                SphericalHarmonicsL2 ambientSphericalHarmonics = default;
                XRTextureDescriptor cameraGrain = default;
                float noiseIntensity = default;

                timeStamp = (long)_cameraTextureFrameEventArgs.TimestampNs;
                properties |= XRCameraFrameProperties.Timestamp;

                projectionMatrix = (Matrix4x4)_cameraTextureFrameEventArgs.ProjectionMatrix;
                properties |= XRCameraFrameProperties.ProjectionMatrix;

                displayMatrix = (Matrix4x4)_cameraTextureFrameEventArgs.DisplayMatrix;
                properties |= XRCameraFrameProperties.DisplayMatrix;

                if (_simulationRgbCameraTextureProvider == null || !_simulationRgbCameraTextureProvider.TryGetLatestImagePtr(out nativePtr))
                {
                    cameraFrame = default;
                    return false;
                }

                cameraFrame = new XRCameraFrame(
                    timeStamp,
                    averageBrightness,
                    averageColorTemperature,
                    colorCorrection,
                    projectionMatrix,
                    displayMatrix,
                    trackingState,
                    nativePtr,
                    properties,
                    averageIntensityInLumens,
                    exposureDuration,
                    exposureOffset,
                    mainLightIntensityInLumens,
                    mainLightColor,
                    mainLightDirection,
                    ambientSphericalHarmonics,
                    cameraGrain,
                    noiseIntensity);

                return true;
            }

            public override bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics)
            {
                cameraIntrinsics = _cameraTextureFrameEventArgs.Intrinsics;
                return true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            var cInfo = new XRCameraSubsystemCinfo {
                id = SubsystemId,
                providerType = typeof(LightshipSimulationProvider),
                subsystemTypeOverride = typeof(LightshipSimulationCameraSubsystem),
                supportsCameraConfigurations = true,
                supportsCameraImage = true,
            };

            if (!XRCameraSubsystem.Register(cInfo))
            {
                Log.Error("Cannot register the camera subsystem");
            }
        }
    }
}
