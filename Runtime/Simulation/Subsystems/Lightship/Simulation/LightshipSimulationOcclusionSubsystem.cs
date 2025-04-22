// Copyright 2022-2025 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

#if UNITY_6000_0_OR_NEWER
            var xrOcclusionSubsystemCinfo = new XROcclusionSubsystemDescriptor.Cinfo()
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

            XROcclusionSubsystemDescriptor.Register(xrOcclusionSubsystemCinfo);
#else
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
#endif
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

            /// <summary>
            /// The occlusion preference mode for when rendering the background.
            /// </summary>
            private OcclusionPreferenceMode _occlusionPreferenceMode;

            /// <summary>
            /// Specifies the requested occlusion preference mode.
            /// </summary>
            /// <value>
            /// The requested occlusion preference mode.
            /// </value>
            public override OcclusionPreferenceMode requestedOcclusionPreferenceMode
            {
                get => _occlusionPreferenceMode;
                set => _occlusionPreferenceMode = value;
            }

            /// <summary>
            /// Get the occlusion preference mode currently in use by the provider.
            /// </summary>
            public override OcclusionPreferenceMode currentOcclusionPreferenceMode => _occlusionPreferenceMode;

            /// <summary>
            /// The CPU image API for interacting with the environment depth image.
            /// </summary>
            public override XRCpuImage.Api environmentDepthCpuImageApi => LightshipCpuImageApi.Instance;

            /// <summary>
            /// The CPU image API for interacting with the environment depth confidence image.
            /// </summary>
            public override XRCpuImage.Api environmentDepthConfidenceCpuImageApi => LightshipCpuImageApi.Instance;

            private Camera _camera;
            private LightshipSimulationDepthTextureProvider _simulationDepthTextureProvider;
            private long _lastFrameTimestamp;

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
                    LightshipSimulationTextureProvider.AddToCamera<LightshipSimulationDepthTextureProvider>(_camera, xrCamera);
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
            {
                _lastFrameTimestamp = args.TimestampNs;
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

            public override bool TryAcquireRawEnvironmentDepthCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                return TryAcquireEnvironmentDepthCpuImage(out cinfo);
            }

            public override bool TryAcquireEnvironmentDepthCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                if (_simulationDepthTextureProvider == null)
                {
                    cinfo = default;
                    return false;
                }

                var gotCpuData =
                    _simulationDepthTextureProvider.TryGetCpuData
                    (
                        out var data,
                        out var dimensions,
                        out var format
                    );

                if (!gotCpuData)
                {
                    cinfo = default;
                    return false;
                }

                // Get ptr to the data on cpu memory
                IntPtr dataPtr;
                unsafe
                {
                    var ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data);
                    dataPtr = (IntPtr)ptr;
                }

                return ((LightshipCpuImageApi)environmentDepthCpuImageApi).TryAddManagedXRCpuImage
                (
                    dataPtr,
                    dimensions.x * dimensions.y * format.BytesPerPixel(),
                    width: dimensions.x,
                    height: dimensions.y,
                    format,
                    (ulong)_lastFrameTimestamp,
                    out cinfo
                );
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

            public override bool TryAcquireEnvironmentDepthConfidenceCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                if (_simulationDepthTextureProvider == null)
                {
                    cinfo = default;
                    return false;
                }

                var gotCpuData =
                    _simulationDepthTextureProvider.TryGetConfidenceCpuData
                    (
                        out var data,
                        out var dimensions,
                        out var format
                    );

                if (!gotCpuData)
                {
                    cinfo = default;
                    return false;
                }

                // Get ptr to the data on cpu memory
                IntPtr dataPtr;
                unsafe
                {
                    var ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data);
                    dataPtr = (IntPtr)ptr;
                }

                return ((LightshipCpuImageApi)environmentDepthConfidenceCpuImageApi).TryAddManagedXRCpuImage
                (
                    dataPtr,
                    dimensions.x * dimensions.y * format.BytesPerPixel(),
                    width: dimensions.x,
                    height: dimensions.y,
                    format,
                    (ulong)_lastFrameTimestamp,
                    out cinfo
                );
            }

            /// <summary>
            /// Get the enabled and disabled shader keywords for the material.
            /// </summary>
            /// <param name="enabledKeywords">The keywords to enable for the material.</param>
            /// <param name="disabledKeywords">The keywords to disable for the material.</param>
            public override void GetMaterialKeywords(out List<string> enabledKeywords,
                out List<string> disabledKeywords)
            {
                if ((_occlusionPreferenceMode == OcclusionPreferenceMode.NoOcclusion))
                {
                    enabledKeywords = null;
                    disabledKeywords = s_environmentDepthEnabledMaterialKeywords;
                }
                else
                {
                    enabledKeywords = s_environmentDepthEnabledMaterialKeywords;
                    disabledKeywords = null;
                }
            }

#if UNITY_6000_0_OR_NEWER
            public override ShaderKeywords GetShaderKeywords() =>
                _occlusionPreferenceMode == OcclusionPreferenceMode.NoOcclusion
                    ? new ShaderKeywords(disabledKeywords: s_environmentDepthEnabledMaterialKeywords.AsReadOnly())
                    : new ShaderKeywords(enabledKeywords: s_environmentDepthEnabledMaterialKeywords.AsReadOnly());
#endif
        }
    }
}
