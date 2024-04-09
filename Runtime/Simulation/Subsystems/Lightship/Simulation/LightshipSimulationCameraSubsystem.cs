// Copyright 2022-2024 Niantic.
using System;

using Niantic.Lightship.AR.Utilities.Logging;

using Unity.Collections;
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
        internal const string k_SubsystemId = "Lightship-XRSimulation-Camera";

        /// <summary>
        /// The name for the shader for rendering the camera texture.
        /// </summary>
        /// <value>
        /// The name for the shader for rendering the camera texture.
        /// </value>
        // const string k_BackgroundShaderName = "Unlit/Simulation Background Simple";
        private const string k_BackgroundShaderName = "Unlit/LightshipPlaybackBackground";

        /// <summary>
        /// The shader property name for the simple RGB component of the camera video frame.
        /// </summary>
        /// <value>
        /// The shader property name for the  simple RGB component of the camera video frame.
        /// </value>
        private const string k_TextureSinglePropertyName = "_CameraTex";

        /// <summary>
        /// The shader property name identifier for the simple RGB component of the camera video frame.
        /// </summary>
        /// <value>
        /// The shader property name identifier for the simple RGB component of the camera video frame.
        /// </value>
        internal static readonly int textureSinglePropertyNameId = Shader.PropertyToID(k_TextureSinglePropertyName);

        private class LightshipSimulationProvider : Provider
        {
            private LightshipCameraTextureFrameEventArgs m_CameraTextureFrameEventArgs;
            private LightshipSimulationRgbCameraTextureProvider _mSimulationRgbCameraTextureProvider;
            private Camera m_Camera;
            private Material m_CameraMaterial;
            private XRCameraConfiguration m_XRCameraConfiguration;

            private XRSupportedCameraBackgroundRenderingMode m_RequestedBackgroundRenderingMode = XRSupportedCameraBackgroundRenderingMode.BeforeOpaques;

            public override Feature currentCamera => Feature.WorldFacingCamera;

            public override XRCameraConfiguration? currentConfiguration
            {
                get => m_XRCameraConfiguration;
                set
                {
                    // Currently assuming any not null configuration is valid for simulation
                    if (value == null)
                        throw new ArgumentNullException("value", "cannot set the camera configuration to null");

                    m_XRCameraConfiguration = (XRCameraConfiguration)value;
                }
            }

            public override Material cameraMaterial => m_CameraMaterial;

            public override bool permissionGranted => true;

            public override XRSupportedCameraBackgroundRenderingMode requestedBackgroundRenderingMode
            {
                get => m_RequestedBackgroundRenderingMode;
                set => m_RequestedBackgroundRenderingMode = value;
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
                var backgroundShader = Shader.Find(k_BackgroundShaderName);

                if (backgroundShader == null)
                {
                   Log.Error("Cannot create camera background material compatible with the render pipeline");
                }
                else
                {
                    m_CameraMaterial = CreateCameraMaterial(k_BackgroundShaderName);
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

                m_Camera = LightshipSimulationDevice.GetOrCreateSimulationCamera().RgbCamera;

                _mSimulationRgbCameraTextureProvider = LightshipSimulationRgbCameraTextureProvider.AddTextureProviderToCamera(m_Camera, xrCamera);
                _mSimulationRgbCameraTextureProvider.frameReceived += FrameReceived;
                if (_mSimulationRgbCameraTextureProvider != null && _mSimulationRgbCameraTextureProvider.CameraFrameEventArgs != null)
                    m_CameraTextureFrameEventArgs = (LightshipCameraTextureFrameEventArgs)_mSimulationRgbCameraTextureProvider.CameraFrameEventArgs;

                m_XRCameraConfiguration = new XRCameraConfiguration(IntPtr.Zero, new Vector2Int(m_Camera.pixelWidth, m_Camera.pixelHeight));
            }

            public override void Stop()
            {
                if (_mSimulationRgbCameraTextureProvider != null)
                    _mSimulationRgbCameraTextureProvider.frameReceived -= FrameReceived;
            }

            public override void Destroy()
            {
                if (_mSimulationRgbCameraTextureProvider != null)
                {
                    Object.Destroy(_mSimulationRgbCameraTextureProvider.gameObject);
                    _mSimulationRgbCameraTextureProvider = null;
                }
            }

            public override NativeArray<XRCameraConfiguration> GetConfigurations(XRCameraConfiguration defaultCameraConfiguration, Allocator allocator)
            {
                var configs = new NativeArray<XRCameraConfiguration>(1, allocator);
                configs[0] = m_XRCameraConfiguration;
                return configs;
            }

            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors(XRTextureDescriptor defaultDescriptor, Allocator allocator)
            {
                if (_mSimulationRgbCameraTextureProvider != null && _mSimulationRgbCameraTextureProvider.TryGetTextureDescriptors(out var descriptors, allocator))
                {
                    return descriptors;
                }

                return base.GetTextureDescriptors(defaultDescriptor, allocator);
            }

            public override bool TryAcquireLatestCpuImage(out XRCpuImage.Cinfo cameraImageCinfo)
            {
                throw new NotImplementedException();
            }

            private void FrameReceived(LightshipCameraTextureFrameEventArgs args)
            {
                m_CameraTextureFrameEventArgs = args;
            }

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame)
            {
                if (_mSimulationRgbCameraTextureProvider == null)
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

                if (m_CameraTextureFrameEventArgs.timestampNs.HasValue)
                {
                    timeStamp = (long)m_CameraTextureFrameEventArgs.timestampNs;
                    properties |= XRCameraFrameProperties.Timestamp;
                }

                if (m_CameraTextureFrameEventArgs.projectionMatrix.HasValue)
                {
                    projectionMatrix = (Matrix4x4)m_CameraTextureFrameEventArgs.projectionMatrix;
                    properties |= XRCameraFrameProperties.ProjectionMatrix;
                }

                if (m_CameraTextureFrameEventArgs.displayMatrix.HasValue)
                {
                    displayMatrix = (Matrix4x4)m_CameraTextureFrameEventArgs.displayMatrix;
                    properties |= XRCameraFrameProperties.DisplayMatrix;
                }

                if (_mSimulationRgbCameraTextureProvider == null || !_mSimulationRgbCameraTextureProvider.TryGetLatestImagePtr(out nativePtr))
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
                cameraIntrinsics = m_CameraTextureFrameEventArgs.intrinsics;
                return true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            var cInfo = new XRCameraSubsystemCinfo {
                id = k_SubsystemId,
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
