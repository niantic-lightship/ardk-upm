// Copyright 2023 Niantic, Inc. All Rights Reserved.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Log;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Occlusion;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Preloading;
using Niantic.Lightship.AR.Utilities.Textures;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Occlusion
{
    /// <summary>
    /// This subsystem provides implementing functionality for the <c>XROcclusionSubsystem</c> class.s
    /// </summary>
    [Preserve]
    public sealed class LightshipOcclusionSubsystem : XROcclusionSubsystem
    {
        internal const uint MaxRecommendedFrameRate = 20;

        public uint TargetFrameRate
        {
            get
            {
                if (provider is LightshipOcclusionProvider lightshipProvider)
                {
                    return lightshipProvider.TargetFrameRate;
                }

                throw new NotSupportedException();
            }
            set
            {
                if (provider is LightshipOcclusionProvider lightshipProvider)
                {
                    lightshipProvider.TargetFrameRate = value;
                }
            }
        }

        /// <summary>
        /// Register the Lightship occlusion subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Log.Info("LightshipOcclusionSubsystem.Register");
            const string id = "Lightship-Occlusion";
            var xrOcclusionSubsystemCinfo = new XROcclusionSubsystemCinfo()
            {
                id = id,
                providerType = typeof(LightshipOcclusionProvider),
                subsystemTypeOverride = typeof(LightshipOcclusionSubsystem),
                humanSegmentationStencilImageSupportedDelegate = () => Supported.Unsupported,
                humanSegmentationDepthImageSupportedDelegate = () => Supported.Unsupported,
                environmentDepthImageSupportedDelegate = () => Supported.Supported,
                environmentDepthConfidenceImageSupportedDelegate = () => Supported.Unsupported,
                environmentDepthTemporalSmoothingSupportedDelegate = () => Supported.Unsupported
            };

            XROcclusionSubsystem.Register(xrOcclusionSubsystemCinfo);
        }

        /// <summary>
        /// The implementation provider class.
        /// </summary>
        internal class LightshipOcclusionProvider : Provider
        {
            /// <summary>
            /// The shader property name for the environment depth texture.
            /// </summary>
            /// <value>
            /// The shader property name for the environment depth texture.
            /// </value>
            private const string _TextureEnvironmentDepthPropertyName = "_EnvironmentDepth";

            /// <summary>
            /// The shader property name identifier for the environment depth texture.
            /// </summary>
            /// <value>
            /// The shader property name identifier for the environment depth texture.
            /// </value>
            private static readonly int _TextureEnvironmentDepthPropertyId =
                Shader.PropertyToID(_TextureEnvironmentDepthPropertyName);

            /// <summary>
            /// The shader keyword for enabling environment depth rendering for ARKit Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string _EnvironmentDepthEnabledARKitMaterialKeyword = "ARKIT_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keyword for enabling environment depth rendering for ARCore Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string _EnvironmentDepthEnabledARCoreMaterialKeyword = "ARCORE_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keyword for enabling environment depth rendering for Lightship Playback Background shader.
            /// </summary>
            /// <value>
            /// The shader keyword for enabling environment depth rendering.
            /// </value>
            private const string _EnvironmentDepthEnabledLightshipMaterialKeyword =
                "LIGHTSHIP_ENVIRONMENT_DEPTH_ENABLED";

            /// <summary>
            /// The shader keywords for enabling environment depth rendering.
            /// </summary>
            /// <value>
            /// The shader keywords for enabling environment depth rendering.
            /// </value>
            private static readonly List<string> _environmentDepthEnabledMaterialKeywords =
                new()
                {
                    _EnvironmentDepthEnabledARKitMaterialKeyword,
                    _EnvironmentDepthEnabledARCoreMaterialKeyword,
                    _EnvironmentDepthEnabledLightshipMaterialKeyword
                };

            // TODO [ARDK-685]: Value will be held and validated in native layer
            private uint _targetFrameRate = MaxRecommendedFrameRate;
            private ulong _latestTimestampMs = 0;

            public uint TargetFrameRate
            {
                get
                {
                    return _targetFrameRate;
                }
                set
                {
                    if (value <= 0)
                    {
                        Log.Error("Target frame rate value must be greater than zero.");
                        return;
                    }

                    if (_targetFrameRate != value)
                    {
                        _targetFrameRate = value;
                        ConfigureProvider();
                    }
                }
            }

            /// <summary>
            /// The occlusion preference mode for when rendering the background.
            /// </summary>
            private OcclusionPreferenceMode _occlusionPreferenceMode;

            /// <summary>
            /// The handle to the native version of the provider
            /// </summary>
            private IntPtr _nativeProviderHandle;

            // This value will strongly affect memory usage.  It can also be set by the user in configuration.
            // The value represents the number of frames in memory before the user must make a copy of the data
            private const int FramesInMemoryCount = 2;
            private readonly BufferedTextureCache _environmentDepthTextures;

            // The index of the frame where the depth buffer was last updated
            private int _frameIndexOfLastUpdate = 0;

            private EnvironmentDepthMode _requestedEnvironmentDepthMode;

            private IApi _api;

            public LightshipOcclusionProvider(): this(new NativeApi()) {}

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipOcclusionProvider(IApi api)
            {
                _api = api;

                _nativeProviderHandle = _api.Construct(LightshipUnityContext.UnityContextHandle);

                // Default depth mode
                _requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;

                _environmentDepthTextures = new BufferedTextureCache(FramesInMemoryCount);

                // Reset settings possibly inherited from a previous session
                OcclusionContext.ResetOccludee();
            }

            /// <summary>
            /// Start the provider.
            /// </summary>
            public override void Start()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                ConfigureProvider();
            }

            private byte UnityModeToLightshipMode(EnvironmentDepthMode mode)
            {
                switch (mode)
                {
                    case EnvironmentDepthMode.Best:
                        return (byte)DepthMode.Smooth;
                    case EnvironmentDepthMode.Fastest:
                        return (byte)DepthMode.Fast;
                    case EnvironmentDepthMode.Medium:
                        return (byte)DepthMode.Medium;
                    default:
                        return (byte)DepthMode.Unspecified;
                }
            }

            private void ConfigureProvider()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Stop(_nativeProviderHandle);

                switch (requestedEnvironmentDepthMode)
                {
                    // Don't start if depth is disabled
                    case EnvironmentDepthMode.Disabled:
                        return;
                }

                // Don't start if occlusion is disabled
                if (_occlusionPreferenceMode == OcclusionPreferenceMode.NoOcclusion)
                    return;

                _api.Configure
                (
                    _nativeProviderHandle,
                    UnityModeToLightshipMode(requestedEnvironmentDepthMode),
                    TargetFrameRate
                );

                _api.Start(_nativeProviderHandle);
            }

            /// <summary>
            /// Stop the provider.
            /// </summary>
            public override void Stop()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Stop(_nativeProviderHandle);
            }

            /// <summary>
            /// Destroy the provider.
            /// </summary>
            public override void Destroy()
            {
                _environmentDepthTextures.Dispose();

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Destruct(_nativeProviderHandle);
                _nativeProviderHandle = IntPtr.Zero;
            }

            /// <summary>
            /// Property to get or set the requested environment depth mode.
            /// </summary>
            /// <value>
            /// The requested environment depth mode.
            /// </value>
            public override EnvironmentDepthMode requestedEnvironmentDepthMode
            {
                get => _requestedEnvironmentDepthMode;
                set
                {
                    if (_requestedEnvironmentDepthMode != value)
                    {
                        _requestedEnvironmentDepthMode = value;
                        ConfigureProvider();
                    }
                }
            }

            /// <summary>
            /// Property to get the current environment depth mode.
            /// </summary>
            public override EnvironmentDepthMode currentEnvironmentDepthMode => _requestedEnvironmentDepthMode;

            /// <summary>
            /// Specifies the requested occlusion preference mode.
            /// </summary>
            /// <value>
            /// The requested occlusion preference mode.
            /// </value>
            public override OcclusionPreferenceMode requestedOcclusionPreferenceMode
            {
                get => _occlusionPreferenceMode;
                set
                {
                    if (_occlusionPreferenceMode != value)
                    {
                        _occlusionPreferenceMode = value;
                        ConfigureProvider();
                    }
                }
            }

            /// <summary>
            /// Get the occlusion preference mode currently in use by the provider.
            /// </summary>
            public override OcclusionPreferenceMode currentOcclusionPreferenceMode => _occlusionPreferenceMode;

            /// <summary>
            /// Get the environment texture descriptor.
            /// </summary>
            /// <param name="xrTextureDescriptor">The environment depth texture descriptor to be populated, if
            /// available.</param>
            /// <returns>
            /// <c>true</c> if the environment depth texture descriptor is available and is returned. Otherwise,
            /// <c>false</c>.
            /// </returns>
            public override bool TryGetEnvironmentDepth(out XRTextureDescriptor xrTextureDescriptor)
            {
                xrTextureDescriptor = default;

                Texture2D texture = null;
                var needsToUpdate = _frameIndexOfLastUpdate != Time.frameCount;
                if (needsToUpdate)
                {
                    var gotEnvDepth =
                        TryAcquireEnvironmentDepth
                        (
                            requestPostProcessing: true,
                            out IntPtr resourceHandle,
                            out IntPtr memoryBuffer,
                            out int size,
                            out int width,
                            out int height,
                            out TextureFormat format,
                            out _latestTimestampMs
                        ); // Need to save _latestTimestamp for different calls to TryAcquireEnvironmentDepth

                    if (gotEnvDepth)
                    {
                        _frameIndexOfLastUpdate = Time.frameCount;
                        texture = _environmentDepthTextures.GetUpdatedTextureFromBuffer
                        (
                            memoryBuffer,
                            size,
                            width,
                            height,
                            format,
                            (uint)_frameIndexOfLastUpdate
                        );

                        _api.DisposeResource(_nativeProviderHandle, resourceHandle);
                    }
                }
                else
                {
                    texture = _environmentDepthTextures.GetActiveTexture();
                }

                if (texture == null)
                    return false;

                xrTextureDescriptor =
                    new XRTextureDescriptor
                    (
                        texture.GetNativeTexturePtr(),
                        texture.width,
                        texture.height,
                        0,
                        texture.format,
                        _TextureEnvironmentDepthPropertyId,
                        0,
                        TextureDimension.Tex2D
                    );

                return true;
            }

            /// <summary>
            /// Gets the CPU construction information for a environment depth image.
            /// </summary>
            /// <param name="cinfo">The CPU image construction information, on success.</param>
            /// <returns>
            /// <c>true</c> if the environment depth texture is available and its CPU image construction information is
            /// returned. Otherwise, <c>false</c>.
            /// </returns>
            public override bool TryAcquireEnvironmentDepthCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                var gotEnvDepth =
                    TryAcquireEnvironmentDepth
                    (
                        requestPostProcessing: true,
                        out IntPtr resourceHandle,
                        out IntPtr memoryBuffer,
                        out int size,
                        out int width,
                        out int height,
                        out TextureFormat format,
                        out _latestTimestampMs
                    ); // Need to save _latestTimestamp for different calls to TryAcquireEnvironmentDepth


                if (!gotEnvDepth)
                {
                    cinfo = default;
                    return false;
                }

                var cpuImageApi = (LightshipCpuImageApi)environmentDepthCpuImageApi;
                var gotCpuImage = cpuImageApi.TryAddManagedXRCpuImage(memoryBuffer, size, width, height, format, _latestTimestampMs, out cinfo);
                _api.DisposeResource(_nativeProviderHandle, resourceHandle);

                return gotCpuImage;
            }

            public override bool TryAcquireRawEnvironmentDepthCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                return TryAcquireEnvironmentDepthCpuImage(out cinfo);
            }

            public override bool TryAcquireSmoothedEnvironmentDepthCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                return TryAcquireEnvironmentDepthCpuImage(out cinfo);
            }

            /// <summary>
            /// Acquires the latest environment depth resource.
            /// </summary>
            /// <param name="requestPostProcessing">Whether to perform post-processing to align the image with
            ///     the current camera pose. Requesting this feature does not guarantee it will actually occur.</param>
            /// <param name="resourceHandle">Handle to the native resource.</param>
            /// <param name="memoryBuffer">Handle to the data buffer.</param>
            /// <param name="size">The size of the data buffer.</param>
            /// <param name="width">The width if the image.</param>
            /// <param name="height">The height of the image.</param>
            /// <param name="format">The texture format that should be used to represent the image.</param>
            /// <param name="frameTimestamp">The timestamp of the frame.</param>
            /// <returns>Whether the resource has been acquired.</returns>
            private bool TryAcquireEnvironmentDepth
            (
                bool requestPostProcessing,
                out IntPtr resourceHandle,
                out IntPtr memoryBuffer,
                out int size,
                out int width,
                out int height,
                out TextureFormat format,
                out ulong frameTimestamp
            )
            {
                // Verify the aspect ratio we need to comply with
                var cameraImageAspectRatio = OcclusionContext.Shared.CameraImageAspectRatio;
                var isCameraAspectRatioValid =
                    cameraImageAspectRatio.HasValue && !cameraImageAspectRatio.Value.IsUndefined();

                // Cannot acquire an environment depth image in an appropriate image container
                if (!_nativeProviderHandle.IsValidHandle() || !isCameraAspectRatioValid)
                {
                    resourceHandle = IntPtr.Zero;
                    memoryBuffer = IntPtr.Zero;
                    size = default;
                    width = default;
                    height = default;
                    format = default;
                    frameTimestamp = default;
                    return false;
                }

                // Acquire the inference result
                resourceHandle =
                    _api.GetEnvironmentDepth
                    (
                        _nativeProviderHandle,
                        out memoryBuffer,
                        out size,
                        out width,
                        out height,
                        out format,
                        out _,
                        out frameTimestamp
                    );

                if (resourceHandle == IntPtr.Zero)
                {
                    return false;
                }

                // Calculate the aligned image container
                height = Mathf.FloorToInt(width / cameraImageAspectRatio.Value);

                // Acquire the most recent device pose
                var didAcquirePose = PoseProvider.TryAcquireCurrentPose(out var poseMatrix);

                // Successfully acquired the native resource, but no post-processing
                // was requested or we have insufficient information to perform it
                IntPtr processedResourceHandle;
                if (!requestPostProcessing || !didAcquirePose)
                {
                    // Blit the original image to a container that matches the camera image aspect ratio
                    processedResourceHandle =
                        _api.Blit
                        (
                            _nativeProviderHandle,
                            resourceHandle,
                            width,
                            height,
                            out memoryBuffer,
                            out size
                        );
                }
                else
                {
                    // Convert the pose to native ARDK format
                    var pose = MatrixConversionHelper.Matrix4x4ToInternalArray(poseMatrix.FromUnityToArdk());

                    // Warp the original depth image to align it with the current pose
                    processedResourceHandle =
                        _api.Warp
                        (
                            _nativeProviderHandle,
                            resourceHandle,
                            pose,
                            width,
                            height,
                            OcclusionContext.Shared.OccludeeEyeDepth,
                            out memoryBuffer,
                            out size
                        );
                }

                // Release the original buffer
                _api.DisposeResource(_nativeProviderHandle, resourceHandle);

                // Deliver the image
                resourceHandle = processedResourceHandle;
                return resourceHandle != IntPtr.Zero;
            }

            /// <summary>
            /// The CPU image API for interacting with the environment depth image.
            /// </summary>
            public override XRCpuImage.Api environmentDepthCpuImageApi => LightshipCpuImageApi.instance;


            /// <summary>
            /// Gets the occlusion texture descriptors associated with the current AR frame.
            /// </summary>
            /// <param name="defaultDescriptor">The default descriptor value.</param>
            /// <param name="allocator">The allocator to use when creating the returned <c>NativeArray</c>.</param>
            /// <returns>The occlusion texture descriptors.</returns>
            public override unsafe NativeArray<XRTextureDescriptor> GetTextureDescriptors
            (
                XRTextureDescriptor defaultDescriptor,
                Allocator allocator
            )
            {
                if (TryGetEnvironmentDepth(out var xrTextureDescriptor))
                {
                    var nativeArray = new NativeArray<XRTextureDescriptor>(1, allocator);
                    nativeArray[0] = xrTextureDescriptor;
                    return nativeArray;
                }

                return new NativeArray<XRTextureDescriptor>(0, allocator);
            }

            /// <summary>
            /// Get the enabled and disabled shader keywords for the material.
            /// </summary>
            /// <param name="enabledKeywords">The keywords to enable for the material.</param>
            /// <param name="disabledKeywords">The keywords to disable for the material.</param>
            public override void GetMaterialKeywords
            (
                out List<string> enabledKeywords,
                out List<string> disabledKeywords
            )
            {
                enabledKeywords = _environmentDepthEnabledMaterialKeywords;
                disabledKeywords = null;
            }
        }
    }
}
