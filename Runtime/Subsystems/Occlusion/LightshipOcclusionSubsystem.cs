// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
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
        /// Request to bypass automatically fetching the texture descriptors.
        /// </summary>
        internal bool RequestDisableFetchTextureDescriptors
        {
            get
            {
                if (provider is LightshipOcclusionProvider lightshipProvider)
                {
                    return lightshipProvider.RequestDisableFetchTextureDescriptors;
                }

                return false;
            }

            set
            {
                if (provider is LightshipOcclusionProvider lightshipProvider)
                {
                    lightshipProvider.RequestDisableFetchTextureDescriptors = value;
                }
            }
        }

        /// <summary>
        /// Returns the intrinsics matrix of the most recent depth prediction. Contains values
        /// for the camera's focal length and principal point. Since the depth texture is guaranteed to be the same
        /// aspect ratio as the camera image, these intrinsics will be the same as those from the <c>ARCameraManager</c>
        /// but scaled by the factor between their resolutions. Converts from world coordinates relative to the
        /// camera to image space, with the x- and y-coordinates expressed in pixels, scaled by the z-value.
        /// </summary>
        /// <remarks>This matrix assumes the image in its original (un-padded) aspect ratio.</remarks>
        /// <exception cref="System.NotSupportedException">Thrown if getting intrinsics matrix is not supported.
        /// </exception>
        [Obsolete("Use OcclusionExtension.LatestIntrinsicsMatrix instead.")]
        public Matrix4x4? LatestIntrinsicsMatrix
        {
            get
            {
                if (provider is LightshipOcclusionProvider lightshipProvider)
                {
                    // Get the aspect ratio of the camera image
                    var isCameraAspectRatioValid = XRDisplayContext.TryGetCameraImageAspectRatio(out var aspectRatio);

                    // Get the original resolution of the depth image
                    var sourceResolution = lightshipProvider.LatestEnvironmentDepthResolution;

                    // Get the original intrinsics matrix for the depth image
                    var narrowIntrinsics = lightshipProvider.LatestIntrinsicsMatrix;

                    if (narrowIntrinsics.HasValue && sourceResolution.HasValue && isCameraAspectRatioValid)
                    {
                        // Calculate the padded intrinsics matrix
                        var targetWidth = sourceResolution.Value.x;
                        var targetHeight = Mathf.FloorToInt(targetWidth / aspectRatio);
                        var result = narrowIntrinsics.Value;

                        // Transform principal point to the padded resolution
                        result[0, 2] += (targetWidth - sourceResolution.Value.x) / 2.0f;
                        result[1, 2] += (targetHeight - sourceResolution.Value.y) / 2.0f;
                        return result;
                    }

                    return null;
                }

                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Returns the intrinsics matrix of the most recent depth prediction. Contains values
        /// for the camera's focal length and principal point. Converts between 2D image pixel
        /// coordinates and 3D world coordinates relative to the camera.
        /// </summary>
        /// <remarks>This matrix assumes the image in its original (un-padded) aspect ratio.</remarks>
        /// <exception cref="System.NotSupportedException">Thrown if getting intrinsics matrix is not supported.
        /// </exception>
        internal Matrix4x4? _LatestIntrinsicsMatrix
        {
            get
            {
                if (provider is LightshipOcclusionProvider lightshipProvider)
                {
                    return lightshipProvider.LatestIntrinsicsMatrix;
                }

                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Returns the extrinsics matrix of the most recent depth prediction. This matrix
        /// represents the transformation from the camera to the world space for the image
        /// that was used to create the latest depth image.
        /// </summary>
        /// <exception cref="System.NotSupportedException">Thrown if getting extrinsics matrix is not supported.
        /// </exception>
        internal Matrix4x4? _LatestExtrinsicsMatrix
        {
            get
            {
                if (provider is LightshipOcclusionProvider lightshipProvider)
                {
                    return lightshipProvider.LatestExtrinsicsMatrix;
                }

                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Tries to acquire the latest environment depth CPU image. Using this
        /// override, the aspect ratio of the resulting image may differ from
        /// that of the camera image. Use the provided sampler matrix to display
        /// the image on the specified viewport.
        /// </summary>
        /// <param name="viewport">The viewport that samplerMatrix should map the image onto.</param>
        /// <param name="cpuImage">If this method returns `true`, an acquired <see cref="XRCpuImage"/>. The CPU image
        /// must be disposed by the caller.</param>
        /// <param name="samplerMatrix">The transformation matrix used when displaying the image on the viewport.</param>
        /// <returns>Returns `true` if an <see cref="XRCpuImage"/> was successfully acquired.
        /// Returns `false` otherwise.</returns>
        internal bool TryAcquireEnvironmentDepthCpuImage(XRCameraParams viewport, out XRCpuImage cpuImage, out Matrix4x4 samplerMatrix)
        {
            if (provider is LightshipOcclusionProvider lsProvider)
            {
                if (lsProvider.environmentDepthCpuImageApi != null &&
                    lsProvider.TryAcquireEnvironmentDepthCpuImage(viewport, out var cinfo, out samplerMatrix))
                {
                    cpuImage = new XRCpuImage(provider.environmentDepthCpuImageApi, cinfo);
                    return true;
                }
            }

            cpuImage = default;
            samplerMatrix = Matrix4x4.identity;
            return false;
        }

        /// <summary>
        /// Register the Lightship occlusion subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
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

            public bool RequestDisableFetchTextureDescriptors { get; set; }

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
            private bool _nativeProviderIsRunning;

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
                XRDisplayContext.ResetOccludee();
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

                _nativeProviderIsRunning = true;
                ConfigureProvider();
                _api.Start(_nativeProviderHandle);
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

                _api.Configure
                (
                    _nativeProviderHandle,
                    UnityModeToLightshipMode(requestedEnvironmentDepthMode),
                    TargetFrameRate
                );

                if (requestedEnvironmentDepthMode == EnvironmentDepthMode.Disabled)
                {
                    if (_nativeProviderIsRunning)
                    {
                        _nativeProviderIsRunning = false;
                        _api.Stop(_nativeProviderHandle);
                    }
                }
                else if (!_nativeProviderIsRunning && running)
                {
                    _nativeProviderIsRunning = true;
                    _api.Start(_nativeProviderHandle);
                }
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

                _nativeProviderIsRunning = false;
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
                set => _occlusionPreferenceMode = value;
            }

            /// <summary>
            /// Get the occlusion preference mode currently in use by the provider.
            /// </summary>
            public override OcclusionPreferenceMode currentOcclusionPreferenceMode => _occlusionPreferenceMode;


            public Matrix4x4? LatestIntrinsicsMatrix
            {
                get
                {
                    if (_nativeProviderHandle.IsValidHandle())
                    {
                        if (_api.TryGetLatestIntrinsicsMatrix(_nativeProviderHandle, out Matrix4x4 intrinsicsMatrix))
                        {
                            return intrinsicsMatrix;
                        }
                    }

                    return null;
                }
            }

            public Matrix4x4? LatestExtrinsicsMatrix
            {
                get
                {
                    if (_nativeProviderHandle.IsValidHandle())
                    {
                        if (_api.TryGetLatestExtrinsicsMatrix(_nativeProviderHandle, out Matrix4x4 extrinsicsMatrix))
                        {
                            return extrinsicsMatrix;
                        }
                    }

                    return null;
                }
            }

            public Vector2Int? LatestEnvironmentDepthResolution
            {
                get
                {
                    if (_nativeProviderHandle.IsValidHandle())
                    {
                        if (_api.TryGetLatestEnvironmentDepthResolution(_nativeProviderHandle, out Vector2Int resolution))
                        {
                            return resolution;
                        }
                    }

                    return null;
                }
            }

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

            /// <summary>
            /// Gets the CPU construction information for a environment depth image.
            /// Using this override, the aspect ratio of the resulting image may differ
            /// from that of the camera image. Use the provided sampler matrix to display
            /// the image on the specified viewport.
            /// </summary>
            /// <param name="viewport">The viewport description the image should map onto.</param>
            /// <param name="cinfo">The CPU image construction information, on success.</param>
            /// <param name="samplerMatrix"></param>
            /// <returns>
            /// <c>true</c> if the environment depth texture is available and its CPU image construction information is
            /// returned. Otherwise, <c>false</c>.
            /// </returns>
            internal bool TryAcquireEnvironmentDepthCpuImage
            (
                XRCameraParams viewport,
                out XRCpuImage.Cinfo cinfo,
                out Matrix4x4 samplerMatrix
            )
            {
                var gotEnvDepth = TryAcquireEnvironmentDepth
                (
                    out IntPtr resourceHandle,
                    out IntPtr memoryBuffer,
                    out samplerMatrix,
                    out int size,
                    out int width,
                    out int height,
                    out TextureFormat format,
                    out _latestTimestampMs,
                    viewport
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
            /// Acquires the latest environment depth image.
            /// The resulting image will have the same aspect ratio as the camera image.
            /// </summary>
            /// <param name="resourceHandle">Handle to the native resource.</param>
            /// <param name="memoryBuffer">Handle to the data buffer.</param>
            /// <param name="size">The size of the data buffer.</param>
            /// <param name="width">The width of the image.</param>
            /// <param name="height">The height of the image.</param>
            /// <param name="format">The texture format that should be used to represent the image.</param>
            /// <param name="frameTimestamp">The timestamp of the frame.</param>
            /// <returns>Whether the resource has been acquired.</returns>
            private bool TryAcquireEnvironmentDepth
            (
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
                var isCameraAspectRatioValid = XRDisplayContext.TryGetCameraImageAspectRatio(out var aspectRatio);

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
                height = Mathf.FloorToInt(width / aspectRatio);

                // Blit the original image to a container that matches the camera image aspect ratio
                IntPtr processedResourceHandle =
                    _api.Blit
                    (
                        _nativeProviderHandle,
                        resourceHandle,
                        width,
                        height,
                        out memoryBuffer,
                        out size
                    );

                // Release the original buffer
                _api.DisposeResource(_nativeProviderHandle, resourceHandle);

                // Deliver the image
                resourceHandle = processedResourceHandle;
                return resourceHandle != IntPtr.Zero;
            }

            /// <summary>
            /// Acquires the latest environment depth image.
            /// The resulting image has the aspect ratio as the inference result.
            /// </summary>
            /// <param name="resourceHandle">Handle to the native resource.</param>
            /// <param name="memoryBuffer">Handle to the data buffer.</param>
            /// <param name="samplerMatrix">The matrix to fit the image to the viewport.</param>
            /// <param name="size">The size of the data buffer.</param>
            /// <param name="width">The width of the image.</param>
            /// <param name="height">The height of the image.</param>
            /// <param name="format">The texture format that should be used to represent the image.</param>
            /// <param name="frameTimestamp">The timestamp of the frame.</param>
            /// <param name="viewport"></param>
            /// <returns>Whether the resource has been acquired.</returns>
            private bool TryAcquireEnvironmentDepth
            (
                out IntPtr resourceHandle,
                out IntPtr memoryBuffer,
                out Matrix4x4 samplerMatrix,
                out int size,
                out int width,
                out int height,
                out TextureFormat format,
                out ulong frameTimestamp,
                XRCameraParams viewport
            )
            {
                // Cannot acquire an environment depth image in an appropriate image container
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    resourceHandle = IntPtr.Zero;
                    memoryBuffer = IntPtr.Zero;
                    samplerMatrix = Matrix4x4.identity;
                    size = default;
                    width = default;
                    height = default;
                    format = default;
                    frameTimestamp = default;
                    return false;
                }

                // Acquire the inference result
                resourceHandle = _api.GetEnvironmentDepth
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

                if (resourceHandle != IntPtr.Zero)
                {
                    samplerMatrix =
                        _api.AcquireSamplerMatrix
                        (
                            _nativeProviderHandle,
                            resourceHandle,
                            viewport,
                            InputReader.CurrentPose,
                            width,
                            height
                        );

                    return true;
                }

                samplerMatrix = Matrix4x4.identity;
                return false;
            }

            /// <summary>
            /// The CPU image API for interacting with the environment depth image.
            /// </summary>
            public override XRCpuImage.Api environmentDepthCpuImageApi => LightshipCpuImageApi.Instance;

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
                if (!RequestDisableFetchTextureDescriptors)
                {
                    if (TryGetEnvironmentDepth(out var xrTextureDescriptor))
                    {
                        var nativeArray = new NativeArray<XRTextureDescriptor>(1, allocator);
                        nativeArray[0] = xrTextureDescriptor;
                        return nativeArray;
                    }
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
                if ((_occlusionPreferenceMode == OcclusionPreferenceMode.NoOcclusion))
                {
                    enabledKeywords = null;
                    disabledKeywords = _environmentDepthEnabledMaterialKeywords;
                }
                else
                {
                    enabledKeywords = _environmentDepthEnabledMaterialKeywords;
                    disabledKeywords = null;
                }
            }
        }
    }
}
