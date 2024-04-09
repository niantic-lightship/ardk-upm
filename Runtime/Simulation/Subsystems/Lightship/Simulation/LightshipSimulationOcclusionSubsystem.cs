// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.Simulation
{
    public class LightshipSimulationOcclusionSubsystem : XROcclusionSubsystem
    {
        /// <summary>
        /// Register the Lightship Playback occlusion subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            const string id = "Lightship-Simulation-Occlusion";
            var xrOcclusionSubsystemCinfo = new XROcclusionSubsystemCinfo()
            {
                id = id,
                providerType = typeof(LightshipSimulationProvider),
                subsystemTypeOverride = typeof(LightshipSimulationOcclusionSubsystem),
                humanSegmentationStencilImageSupportedDelegate = () => Supported.Unsupported,
                humanSegmentationDepthImageSupportedDelegate = () => Supported.Unsupported,
                environmentDepthImageSupportedDelegate = () => Supported.Supported,
                environmentDepthConfidenceImageSupportedDelegate = () => Supported.Supported,
                environmentDepthTemporalSmoothingSupportedDelegate = () => Supported.Unsupported
            };

            XROcclusionSubsystem.Register(xrOcclusionSubsystemCinfo);
        }

        private class LightshipSimulationProvider : Provider
        {
            /// <summary>
            /// The shader keyword for enabling environment depth rendering for ARKit Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string k_EnvironmentDepthEnabledARKitMaterialKeyword = "ARKIT_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keyword for enabling environment depth rendering for ARCore Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string k_EnvironmentDepthEnabledARCoreMaterialKeyword = "ARCORE_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keyword for enabling environment depth rendering for Lightship Playback Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string k_EnvironmentDepthEnabledLightshipMaterialKeyword =
                "LIGHTSHIP_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keywords for enabling environment depth rendering.
            /// </summary>
            /// <value>
            /// The shader keywords for enabling environment depth rendering.
            /// </value>
            private static readonly List<string> s_EnvironmentDepthEnabledMaterialKeywords =
                new()
                {
                    k_EnvironmentDepthEnabledARKitMaterialKeyword,
                    k_EnvironmentDepthEnabledARCoreMaterialKeyword,
                    k_EnvironmentDepthEnabledLightshipMaterialKeyword
                };

            private Camera m_Camera;
            private LightshipCameraTextureFrameEventArgs m_DepthTextureFrameEventArgs;
            private LightshipSimulationDepthTextureProvider _mSimulationDepthTextureProvider;

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipSimulationProvider()
            {
                Debug.Log("LightshipSimulationProvider construct");
            }

            public override void Start()
            {
                var xrOrigin = Object.FindObjectOfType<XROrigin>();
                if (xrOrigin == null)
                    throw new NullReferenceException("No XROrigin found.");

                var xrCamera = xrOrigin.Camera;
                if (xrCamera == null)
                    throw new NullReferenceException("No camera found under XROrigin.");

                var simulationDevice = LightshipSimulationDevice.GetOrCreateSimulationCamera();

                var cameraGo = new GameObject("LightshipSimulationDepthCamera");
                cameraGo.transform.SetParent(simulationDevice.CameraParent, false);
                m_Camera = cameraGo.AddComponent<Camera>();
                m_Camera.enabled = false;

                _mSimulationDepthTextureProvider =
                    LightshipSimulationDepthTextureProvider.AddTextureProviderToCamera(m_Camera, xrCamera);
                _mSimulationDepthTextureProvider.frameReceived += CameraFrameReceived;
                if (_mSimulationDepthTextureProvider != null && _mSimulationDepthTextureProvider.CameraFrameEventArgs != null)
                    m_DepthTextureFrameEventArgs =
                        (LightshipCameraTextureFrameEventArgs)_mSimulationDepthTextureProvider.CameraFrameEventArgs;
            }

            public override void Stop()
            {

            }

            public override void Destroy()
            {

            }

            private void CameraFrameReceived(LightshipCameraTextureFrameEventArgs args)
            {
                m_DepthTextureFrameEventArgs = args;
            }

            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors(
                XRTextureDescriptor defaultDescriptor,
                Allocator allocator)
            {
                if (TryGetEnvironmentDepth(out var xrTextureDescriptor))
                {
                    var nativeArray = new NativeArray<XRTextureDescriptor>(1, allocator);
                    nativeArray[0] = xrTextureDescriptor;
                    return nativeArray;
                }

                return new NativeArray<XRTextureDescriptor>(0, allocator);
            }

            public override bool TryGetEnvironmentDepth(out XRTextureDescriptor xrTextureDescriptor)
            {
                if (_mSimulationDepthTextureProvider == null)
                {
                    xrTextureDescriptor = default;
                    return false;
                }

                _mSimulationDepthTextureProvider.TryGetTextureDescriptor(out var descriptor);
                xrTextureDescriptor = descriptor;
                return true;
            }

            public override bool TryGetEnvironmentDepthConfidence(
                out XRTextureDescriptor environmentDepthConfidenceDescriptor)
            {
                if (_mSimulationDepthTextureProvider == null)
                {
                    environmentDepthConfidenceDescriptor = default;
                    return false;
                }

                _mSimulationDepthTextureProvider.TryGetConfidenceTextureDescriptor(out var descriptor);
                environmentDepthConfidenceDescriptor = descriptor;
                return true;
            }

            /// <summary>
            /// Get the enabled and disabled shader keywords for the material.
            /// </summary>
            /// <param name="enabledKeywords">The keywords to enable for the material.</param>
            /// <param name="disabledKeywords">The keywords to disable for the material.</param>
            public override void GetMaterialKeywords(out List<string> enabledKeywords,
                out List<string> disabledKeywords)
            {
                enabledKeywords = s_EnvironmentDepthEnabledMaterialKeywords;
                disabledKeywords = null;
            }
        }
    }
}
