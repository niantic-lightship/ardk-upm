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
            private const string EnvironmentDepthEnabledARKitMaterialKeyword = "ARKIT_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keyword for enabling environment depth rendering for ARCore Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string EnvironmentDepthEnabledARCoreMaterialKeyword = "ARCORE_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keyword for enabling environment depth rendering for Lightship Playback Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string EnvironmentDepthEnabledLightshipMaterialKeyword =
                "LIGHTSHIP_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keywords for enabling environment depth rendering.
            /// </summary>
            /// <value>
            /// The shader keywords for enabling environment depth rendering.
            /// </value>
            private static readonly List<string> s_environmentDepthEnabledMaterialKeywords =
                new()
                {
                    EnvironmentDepthEnabledARKitMaterialKeyword,
                    EnvironmentDepthEnabledARCoreMaterialKeyword,
                    EnvironmentDepthEnabledLightshipMaterialKeyword
                };

            private Camera _camera;
            private LightshipSimulationDepthTextureProvider _simulationDepthTextureProvider;

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
                _camera = cameraGo.AddComponent<Camera>();
                _camera.enabled = false;

                _simulationDepthTextureProvider =
                    LightshipSimulationDepthTextureProvider.AddTextureProviderToCamera(_camera, xrCamera);
                _simulationDepthTextureProvider.FrameReceived += CameraFrameReceived;
            }

            public override void Stop()
            {
                if (_simulationDepthTextureProvider != null)
                    _simulationDepthTextureProvider.FrameReceived -= CameraFrameReceived;
            }

            public override void Destroy()
            { }

            private void CameraFrameReceived(LightshipCameraTextureFrameEventArgs args)
            { }

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
                if (_simulationDepthTextureProvider == null)
                {
                    xrTextureDescriptor = default;
                    return false;
                }

                if (!_simulationDepthTextureProvider.TryGetTextureDescriptor(out var descriptor))
                {
                    xrTextureDescriptor = default;
                    return false;
                }

                xrTextureDescriptor = descriptor;
                return true;
            }

            public override bool TryGetEnvironmentDepthConfidence(
                out XRTextureDescriptor environmentDepthConfidenceDescriptor)
            {
                if (_simulationDepthTextureProvider == null)
                {
                    environmentDepthConfidenceDescriptor = default;
                    return false;
                }

                _simulationDepthTextureProvider.TryGetConfidenceTextureDescriptor(out var descriptor);
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
                enabledKeywords = s_environmentDepthEnabledMaterialKeywords;
                disabledKeywords = null;
            }
        }
    }
}
