// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.IO;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Playback
{
    [Preserve]
    public class LightshipPlaybackOcclusionSubsystem : XROcclusionSubsystem, IPlaybackDatasetUser
    {
        /// <summary>
        /// Register the Lightship Playback occlusion subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            Log.Info("LightshipPlaybackOcclusionSubsystem.Register");
            const string id = "Lightship-Playback-Occlusion";
            var xrOcclusionSubsystemCinfo = new XROcclusionSubsystemCinfo()
            {
                id = id,
                providerType = typeof(LightshipPlaybackProvider),
                subsystemTypeOverride = typeof(LightshipPlaybackOcclusionSubsystem),
                humanSegmentationStencilImageSupportedDelegate = () => Supported.Unsupported,
                humanSegmentationDepthImageSupportedDelegate = () => Supported.Unsupported,
                environmentDepthImageSupportedDelegate = () => Supported.Supported,
                environmentDepthConfidenceImageSupportedDelegate = () => Supported.Supported,
                environmentDepthTemporalSmoothingSupportedDelegate = () => Supported.Unsupported
            };

            XROcclusionSubsystem.Register(xrOcclusionSubsystemCinfo);
        }

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            ((IPlaybackDatasetUser)provider).SetPlaybackDatasetReader(reader);
        }

        internal Matrix4x4? _LatestIntrinsicsMatrix
        {
            get
            {
                if (provider is LightshipPlaybackProvider lightshipProvider)
                {
                    return lightshipProvider.LatestIntrinsicsMatrix;
                }

                throw new NotSupportedException();
            }
        }

        internal Matrix4x4? _LatestExtrinsicsMatrix
        {
            get
            {
                if (provider is LightshipPlaybackProvider lightshipProvider)
                {
                    return lightshipProvider.LatestExtrinsicsMatrix;
                }

                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// The implementation provider class.
        /// </summary>
        private class LightshipPlaybackProvider : Provider, IPlaybackDatasetUser
        {
            /// <summary>
            /// The shader property name for the environment depth texture.
            /// </summary>
            /// <value>
            /// The shader property name for the environment depth texture.
            /// </value>
            private const string TextureEnvironmentDepthPropertyName = "_EnvironmentDepth";

            /// <summary>
            /// The shader property name for the environment depth confidence texture.
            /// </summary>
            /// <value>
            /// The shader property name for the environment depth confidence texture.
            /// </value>
            private const string TextureEnvironmentDepthConfidencePropertyName = "_EnvironmentDepthConfidence";

            /// <summary>
            /// The shader property name identifier for the environment depth texture.
            /// </summary>
            /// <value>
            /// The shader property name identifier for the environment depth texture.
            /// </value>
            private static readonly int s_textureEnvironmentDepthPropertyId =
                Shader.PropertyToID(TextureEnvironmentDepthPropertyName);

            /// <summary>
            /// The shader property name identifier for the environment depth texture.
            /// </summary>
            /// <value>
            /// The shader property name identifier for the environment depth texture.
            /// </value>
            private static readonly int s_textureEnvironmentDepthConfidencePropertyId =
                Shader.PropertyToID(TextureEnvironmentDepthConfidencePropertyName);

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
            /// The CPU image API for interacting with the environment depth image.
            /// </summary>
            public override XRCpuImage.Api environmentDepthCpuImageApi => LightshipCpuImageApi.Instance;

            /// <summary>
            /// The CPU image API for interacting with the environment depth confidence image.
            /// </summary>
            public override XRCpuImage.Api environmentDepthConfidenceCpuImageApi => LightshipCpuImageApi.Instance;

            /// <summary>
            /// The handle to the native version of the provider
            /// </summary>
            private IntPtr _nativeProviderHandle;

            private PlaybackDatasetReader _datasetReader;

            // This value will strongly affect memory usage.  It can also be set by the user in configuration.
            // The value represents the number of frames in memory before the user must make a copy of the data
            private const int FramesInMemoryCount = 2;
            private int _textureWidth;
            private int _textureHeight;
            private (int Id, Texture2D Frame) _currentDepthTexture;
            private (int Id, Texture2D Frame) _currentConfidenceTexture;

            private TextureFormat _depthImageFormat => TextureFormat.RFloat;
            private TextureFormat _depthConfidenceImageFormat => TextureFormat.R8;

            private EnvironmentDepthMode _requestedEnvironmentDepthMode;
            private readonly int _dummyStartingValue = -99;

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipPlaybackProvider()
            {
                Log.Info("LightshipPlaybackOcclusionSubsystem.LightshipPlaybackProvider construct");

                // Default depth mode
                _requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
            }

            public override void Start() { }

            public override void Stop() { }

            /// <summary>
            /// Destroy the provider.
            /// </summary>
            public override void Destroy()
            {
                _datasetReader = null;
            }

            public void SetPlaybackDatasetReader(PlaybackDatasetReader reader)
            {
                _datasetReader = reader;

                var depthRes = _datasetReader.GetDepthResolution();
                _textureWidth = depthRes.x;
                _textureHeight = depthRes.y;

                _currentDepthTexture = new ValueTuple<int, Texture2D>
                    (_dummyStartingValue, new Texture2D(_textureWidth, _textureHeight, _depthImageFormat, false));
                _currentConfidenceTexture = new ValueTuple<int, Texture2D>
                    (_dummyStartingValue, new Texture2D(_textureWidth, _textureHeight, _depthConfidenceImageFormat, false));
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
                set { _requestedEnvironmentDepthMode = value; }
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
                    if (_datasetReader == null)
                    {
                        return null;
                    }

                    var frame = _datasetReader.CurrFrame;
                    if (frame != null)
                    {
                        var res = _datasetReader.GetDepthResolution();
                        var intrinsics = frame.Intrinsics;
                        var result = Matrix4x4.identity;
                        result[0, 0] = intrinsics.focalLength.x / intrinsics.resolution.x * res.x;
                        result[1, 1] = intrinsics.focalLength.y / intrinsics.resolution.y * res.y;
                        result[0, 2] = intrinsics.principalPoint.x / intrinsics.resolution.x * res.x;
                        result[1, 2] = intrinsics.principalPoint.y / intrinsics.resolution.y * res.y;
                        return result;
                    }

                    return null;
                }
            }

            public Matrix4x4? LatestExtrinsicsMatrix
            {
                get
                {
                    return _datasetReader?.CurrFrame?.Pose;
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
                // TODO (kcho): Check if LiDAR frames are fetched after subsystem is stopped by ARKit
                if (_datasetReader == null)
                {
                    xrTextureDescriptor = default;
                    return false;
                }

                var frame = _datasetReader.CurrFrame;
                if (frame == null || !frame.HasDepth)
                {
                    xrTextureDescriptor = default;
                    return false;
                }

                UpdateCacheWithCurrentDepthImage(frame);

                var depthResolution = _datasetReader.GetDepthResolution();
                xrTextureDescriptor =
                    new XRTextureDescriptor
                    (
                        _currentDepthTexture.Frame.GetNativeTexturePtr(),
                        width: depthResolution.x,
                        height: depthResolution.y,
                        mipmapCount: 0,
                        _currentDepthTexture.Frame.format,
                        propertyNameId: s_textureEnvironmentDepthPropertyId,
                        depth: 0,
                        TextureDimension.Tex2D
                    );

                return true;
            }

            public override bool TryAcquireRawEnvironmentDepthCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                return TryAcquireEnvironmentDepthCpuImage(out cinfo);
            }

            /// <summary>
            /// Gets the CPU construction information for a environment depth image.
            /// Only really used for FPSMetricsUtility to collect timestamp
            /// </summary>
            /// <param name="cinfo">The CPU image construction information, on success.</param>
            /// <returns>
            /// <c>true</c> if the environment depth texture is available and its CPU image construction information is
            /// returned. Otherwise, <c>false</c>.
            /// </returns>
            public override bool TryAcquireEnvironmentDepthCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                if (_datasetReader == null)
                {
                    cinfo = default;
                    return false;
                }

                var frame = _datasetReader.CurrFrame;
                if (frame == null || !frame.HasDepth)
                {
                    cinfo = default;
                    return false;
                }

                // Playback depth frames are being used
                UpdateCacheWithCurrentDepthImage(frame);

                var cpuImageApi = (LightshipCpuImageApi)environmentDepthCpuImageApi;

                IntPtr dataPtr;
                unsafe
                {
                    dataPtr = (IntPtr) _currentDepthTexture.Frame.GetRawTextureData<byte>().GetUnsafeReadOnlyPtr();
                }

                var depthResolution = _datasetReader.GetDepthResolution();
                var gotCpuImage = cpuImageApi.TryAddManagedXRCpuImage
                (
                    dataPtr,
                    depthResolution.x * depthResolution.y * _currentDepthTexture.Frame.format.BytesPerPixel(),
                    width: depthResolution.x,
                    height: depthResolution.y,
                    _currentDepthTexture.Frame.format,
                    (ulong)(frame.TimestampInSeconds * 1000.0),
                    out cinfo
                );

                return gotCpuImage;
            }

            public override bool TryAcquireEnvironmentDepthConfidenceCpuImage(out XRCpuImage.Cinfo cinfo)
            {
                if (_datasetReader == null)
                {
                    cinfo = default;
                    return false;
                }

                var frame = _datasetReader.CurrFrame;
                if (frame == null || !frame.HasDepth)
                {
                    cinfo = default;
                    return false;
                }

                UpdateCacheWithCurrentConfidenceImage(frame);

                var cpuImageApi = (LightshipCpuImageApi)environmentDepthCpuImageApi;

                var currentFrame = _currentConfidenceTexture.Frame;
                IntPtr dataPtr;
                unsafe
                {
                    dataPtr = (IntPtr) currentFrame.GetRawTextureData<byte>().GetUnsafeReadOnlyPtr();
                }

                var resolution = _datasetReader.GetDepthResolution();
                var gotCpuImage = cpuImageApi.TryAddManagedXRCpuImage
                (
                    dataPtr,
                    currentFrame.width * currentFrame.height * currentFrame.format.BytesPerPixel(),
                    width: resolution.x,
                    height: resolution.y,
                    currentFrame.format,
                    (ulong)(frame.TimestampInSeconds * 1000.0),
                    out cinfo
                );

                return gotCpuImage;
            }

            //  Values are 0, 1, and 2 for low, medium, and high, respectively.
            public override bool TryGetEnvironmentDepthConfidence
            (
                out XRTextureDescriptor environmentDepthConfidenceDescriptor
            )
            {
                environmentDepthConfidenceDescriptor = default;

                // TODO (kcho): Check if LiDAR frames are fetched after subsystem is stopped by ARKit
                if (_datasetReader == null)
                {
                    return false;
                }

                var frame = _datasetReader.CurrFrame;
                if (frame == null || !frame.HasDepth)
                {
                    return false;
                }

                UpdateCacheWithCurrentConfidenceImage(frame);

                environmentDepthConfidenceDescriptor =
                    new XRTextureDescriptor
                    (
                        _currentConfidenceTexture.Frame.GetNativeTexturePtr(),
                        _textureWidth,
                        _textureHeight,
                        mipmapCount: 0,
                        _currentConfidenceTexture.Frame.format,
                        propertyNameId: s_textureEnvironmentDepthConfidencePropertyId,
                        depth: 0,
                        TextureDimension.Tex2D
                    );

                return true;
            }

            private void UpdateCacheWithCurrentDepthImage(PlaybackDataset.FrameMetadata frame)
            {
                if (_currentDepthTexture.Id != frame.Sequence)
                {
                    Texture2D tex = _currentDepthTexture.Frame;
                    var wasReinitialisationSuccessful = tex.Reinitialize(_textureWidth, _textureHeight,
                        _depthImageFormat, false);

                    if (!wasReinitialisationSuccessful)
                    {
                        tex = new Texture2D(_textureWidth, _textureHeight, _depthImageFormat, false);
                    }

                    var path = Path.Combine(_datasetReader.GetDatasetPath(), frame.DepthPath);
                    byte[] buffer = File.ReadAllBytes(path);
                    tex.LoadRawTextureData(buffer);
                    tex.Apply();

                    _currentDepthTexture = (frame.Sequence, tex);
                }
            }


            private void UpdateCacheWithCurrentConfidenceImage(PlaybackDataset.FrameMetadata frame)
            {
                if (_currentConfidenceTexture.Id != frame.Sequence)
                {
                    Texture2D tex = _currentConfidenceTexture.Frame;
                    var reinitStatus = tex.Reinitialize(_textureWidth, _textureHeight,
                        _depthConfidenceImageFormat, false);

                    if (!reinitStatus)
                    {
                        tex = new Texture2D(_textureWidth, _textureHeight, _depthConfidenceImageFormat, false);
                    }

                    var path = Path.Combine(_datasetReader.GetDatasetPath(), frame.DepthConfidencePath);
                    byte[] buffer = File.ReadAllBytes(path);
                    tex.LoadRawTextureData(buffer);
                    tex.Apply();

                    _currentConfidenceTexture = (frame.Sequence, tex);
                }
            }


            /// <summary>
            /// Gets the occlusion texture descriptors associated with the current AR frame.
            /// </summary>
            /// <param name="defaultDescriptor">The default descriptor value.</param>
            /// <param name="allocator">The allocator to use when creating the returned <c>NativeArray</c>.</param>
            /// <returns>The occlusion texture descriptors.</returns>
            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors
            (
                XRTextureDescriptor defaultDescriptor,
                Allocator allocator
            )
            {
                // TODO: verify if confidence descriptor is included
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
        }
    }
}
