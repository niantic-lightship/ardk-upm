// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.XRSubsystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;
using Utilities;

namespace Niantic.Lightship.AR.Subsystems.Semantics
{
    /// <summary>
    /// The Lightship implementation of the <c>XRSemanticsSubsystem</c>. Do not create this directly.
    /// Use the <c>SubsystemManager</c> instead.
    /// </summary>
    [Preserve]
    public sealed class LightshipSemanticsSubsystem : XRSemanticsSubsystem, ISubsystemWithMutableApi<IApi>
    {
        internal const uint MaxRecommendedFrameRate = 20;

        /// <summary>
        /// Register the Lightship semantics subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            Log.Info(nameof(LightshipSemanticsSubsystem)+"."+nameof(Register));
            const string id = "Lightship-Semantics";
            var xrSemanticsSubsystemCinfo = new XRSemanticsSubsystemCinfo()
            {
                id = id,
                providerType = typeof(LightshipSemanticsProvider),
                subsystemTypeOverride = typeof(LightshipSemanticsSubsystem),
                semanticSegmentationImageSupportedDelegate = () => Supported.Supported,
            };

            XRSemanticsSubsystem.Register(xrSemanticsSubsystemCinfo);
        }

        /// <summary>
        /// Returns the intrinsics matrix of the most recent semantic segmentation prediction. Contains values
        /// for the camera's focal length and principal point. Converts from world coordinates relative to the
        /// camera to image space, with the x- and y-coordinates expressed in pixels, scaled by the z-value.
        /// </summary>
        /// <value>
        /// The intrinsics matrix.
        /// </value>
        /// <exception cref="System.NotSupportedException">Thrown if getting intrinsics matrix is not supported.
        /// </exception>
        public Matrix4x4? LatestIntrinsicsMatrix
        {
            get
            {
                if (provider is LightshipSemanticsProvider lightshipProvider)
                {
                    return lightshipProvider.LatestIntrinsicsMatrix;
                }

                throw new NotSupportedException();
            }
        }

        void ISubsystemWithMutableApi<IApi>.SwitchApiImplementation(IApi api)
        {
            ((LightshipSemanticsProvider) provider).SwitchApiImplementation(api);
        }

        void ISubsystemWithMutableApi<IApi>.SwitchToInternalMockImplementation()
        {
            ((LightshipSemanticsProvider) provider).SwitchApiImplementation(new MockApi());
        }

        /// <summary>
        /// The implementation provider class.
        /// </summary>
        internal class LightshipSemanticsProvider : Provider
        {
            /// <summary>
            /// The handle to the native version of the provider
            /// </summary>
            private IntPtr _nativeProviderHandle;
            private IApi _api;

            private uint _targetFrameRate = MaxRecommendedFrameRate;
            private const float _useDefaultConfidenceThreshold = -1.0f;

            private List<string> _channelNames = new();
            private List<string> _suppressionMaskChannels = new();

            public LightshipSemanticsProvider() : this(new NativeApi()) { }

            /// <summary>
            /// Property to get or set the target frame rate for the semantic segmentation feature.
            /// </summary>
            /// <value>
            /// The requested target frame rate in frames per second.
            /// </value>
            public override uint TargetFrameRate
            {
                get => _targetFrameRate;
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

            public override List<string> SuppressionMaskChannels
            {
                get => _suppressionMaskChannels;
                set
                {
                    if (_suppressionMaskChannels != value)
                    {
                        _suppressionMaskChannels = value;
                        ConfigureProvider();
                    }
                }
            }

            public override uint? LatestFrameId
            {
                get
                {
                    if (_nativeProviderHandle.IsValidHandle())
                    {
                        if (_api.TryGetLatestFrameId(_nativeProviderHandle, out uint id))
                        {
                            return id;
                        }
                    }

                    return null;
                }
            }

            /// <summary>
            /// Returns the intrinsics matrix of the most recent semantic segmentation prediction. Contains values
            /// for the camera's focal length and principal point. Converts from world coordinates relative to the
            /// camera to image space, with the x- and y-coordinates expressed in pixels, scaled by the z-value.
            /// </summary>
            /// <value>
            /// The intrinsics matrix.
            /// </value>
            /// <exception cref="System.NotSupportedException">Thrown if getting intrinsics matrix is not supported.
            /// </exception>
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

            public override bool IsMetadataAvailable
            {
                get
                {
                    if (_nativeProviderHandle.IsValidHandle())
                    {
                        return _api.HasMetadata(_nativeProviderHandle);
                    }

                    return false;
                }
            }

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipSemanticsProvider(IApi api)
            {
                Log.Info("LightshipSemanticsSubsystem.LightshipSemanticsProvider construct");

                _api = api;
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                _nativeProviderHandle = _api.Construct(LightshipUnityContext.UnityContextHandle);
#endif
                Log.Info("LightshipSemanticsSubsystem got nativeProviderHandle: " + _nativeProviderHandle);

            }

            // Destruct the native provider and replace it with the provided (or default mock) provider
            // Used for testing and mocking
            internal void SwitchApiImplementation(IApi api)
            {
                if (_nativeProviderHandle != IntPtr.Zero)
                {
                    _api.Stop(_nativeProviderHandle);
                    _api.Destruct(_nativeProviderHandle);
                }

                _api = api;
                _nativeProviderHandle = api.Construct(LightshipUnityContext.UnityContextHandle);

                _channelNames = new List<string>();
            }

            /// <summary>
            /// Gets a semantics channel texture descriptor and a matrix used to fit the texture to the viewport.
            /// </summary>
            /// <param name="channelName">The string description of the semantics channel that is needed.</param>
            /// <param name="semanticChannelDescriptor">The semantics channel texture descriptor to be populated, if
            /// available from the provider.</param>
            /// <param name="samplerMatrix">Converts from normalized viewport coordinates to normalized texture coordinates.</param>
            /// <param name="cameraParams">Describes the viewport.</param>
            /// <returns>
            /// <c>true</c> if the semantics channel texture descriptor is available and is returned. Otherwise,
            /// <c>false</c>.
            /// </returns>
            public override bool TryGetSemanticChannel
            (
                string channelName,
                out XRTextureDescriptor semanticChannelDescriptor,
                out Matrix4x4 samplerMatrix,
                XRCameraParams? cameraParams = null
            )
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    semanticChannelDescriptor = default;
                    samplerMatrix = Matrix4x4.identity;
                    return false;
                }

                return _api.TryGetSemanticChannel
                (
                    _nativeProviderHandle,
                    channelName,
                    cameraParams,
                    InputReader.CurrentPose,
                    out semanticChannelDescriptor,
                    out samplerMatrix
                );
            }

            /// <summary>
            /// Tries to acquire the latest semantics channel XRCpuImage.
            /// </summary>
            /// <param name="channelName">The string description of the semantics channel that is needed.</param>
            /// <param name="cpuImage">If this method returns `true`, an acquired <see cref="XRCpuImage"/>. The XRCpuImage
            /// must be disposed by the caller.</param>
            /// <param name="samplerMatrix">A matrix that converts from viewport to texture coordinates.</param>
            /// <param name="cameraParams">Describes the viewport.</param>
            /// <returns>Returns `true` if an <see cref="XRCpuImage"/> was successfully acquired.
            /// Returns `false` otherwise.</returns>
            public override bool TryAcquireSemanticChannelCpuImage
            (
                string channelName,
                out XRCpuImage cpuImage,
                out Matrix4x4 samplerMatrix,
                XRCameraParams? cameraParams = null
            )
            {
                cpuImage = default;
                samplerMatrix = Matrix4x4.identity;
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                return
                    _api.TryAcquireSemanticChannelCpuImage
                    (
                        _nativeProviderHandle,
                        channelName,
                        cameraParams,
                        InputReader.CurrentPose,
                        out cpuImage,
                        out samplerMatrix
                    );
            }

            /// <summary>
            /// Gets a packed semantics texture descriptor.
            /// </summary>
            /// <param name="packedSemanticsDescriptor">The packed semantics texture descriptor to be populated, if
            /// available from the provider.</param>
            /// <param name="samplerMatrix">A matrix that converts from viewport to texture coordinates.</param>
            /// <param name="cameraParams">Describes the viewport.</param>
            /// <returns>
            /// <c>true</c> if the packed semantics texture descriptor is available and is returned. Otherwise,
            /// <c>false</c>.
            /// </returns>
            public override bool TryGetPackedSemanticChannels
            (
                out XRTextureDescriptor packedSemanticsDescriptor,
                out Matrix4x4 samplerMatrix,
                XRCameraParams? cameraParams = null
            )
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    packedSemanticsDescriptor = default;
                    samplerMatrix = Matrix4x4.identity;
                    return false;
                }

                return
                    _api.TryGetPackedSemanticChannels
                    (
                        _nativeProviderHandle,
                        cameraParams,
                        InputReader.CurrentPose,
                        out packedSemanticsDescriptor,
                        out samplerMatrix
                    );
            }

            /// <summary>
            ///  Tries to acquire the latest packed semantic channels XRCpuImage.
            /// </summary>
            /// <param name="cpuImage">If this method returns `true`, an acquired <see cref="XRCpuImage"/>. The XRCpuImage
            /// must be disposed by the caller.</param>
            /// <param name="samplerMatrix">A matrix that converts from viewport to texture coordinates.</param>
            /// <param name="cameraParams">Describes the viewport.</param>
            /// <returns>True if the XRCpuImage is acquired. Otherwise, false</returns>
            public override bool TryAcquirePackedSemanticChannelsCpuImage
            (
                out XRCpuImage cpuImage,
                out Matrix4x4 samplerMatrix,
                XRCameraParams? cameraParams = null
            )
            {
                cpuImage = default;
                samplerMatrix = default;

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                return
                    _api.TryAcquirePackedSemanticChannelsCpuImage
                    (
                        _nativeProviderHandle,
                        cameraParams,
                        InputReader.CurrentPose,
                        out cpuImage,
                        out samplerMatrix
                    );
            }


            /// <summary>
            /// Tries to get the suppression mask texture already computed from the latest semantics.
            /// </summary>
            /// <param name="suppressionMaskDescriptor">The suppression mask texture descriptor to be populated, if available from the provider.</param>
            /// <param name="samplerMatrix">A matrix that converts from viewport to texture coordinates</param>
            /// <param name="cameraParams">Describes the viewport</param>
            /// <returns>
            /// <c>true</c> if the suppression mask texture descriptor is available and is returned. Otherwise, <c>false</c>.
            /// </returns>
            public override bool TryGetSuppressionMaskTexture
            (
                out XRTextureDescriptor suppressionMaskDescriptor,
                out Matrix4x4 samplerMatrix,
                XRCameraParams? cameraParams = null
            )
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    suppressionMaskDescriptor = default;
                    samplerMatrix = Matrix4x4.identity;
                    return false;
                }

                return _api.TryGetSuppressionMaskTexture
                (
                    _nativeProviderHandle,
                    cameraParams,
                    InputReader.CurrentPose,
                    out suppressionMaskDescriptor,
                    out samplerMatrix
                );
            }

            /// <summary>
            /// Try to acquire the suppression mask XRCpuImage.
            /// </summary>
            /// <param name="cpuImage">If the method returns 'true', an acquired <see cref="XRCpuImage"/>. The XRCpuImage must be disposed by the caller.</param>
            /// <param name="samplerMatrix">A matrix that converts from viewport to texture coordinates.</param>
            /// <param name="cameraParams">Describes the viewport.</param>
            /// <returns>True if the XRCpuImage is acquired. Otherwise, false</returns>
            public override bool TryAcquireSuppressionMaskCpuImage
            (
                out XRCpuImage cpuImage,
                out Matrix4x4 samplerMatrix,
                XRCameraParams? cameraParams = null
            )
            {
                cpuImage = default;
                samplerMatrix = default;

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                return
                    _api.TryAcquireSuppressionMaskCpuImage
                    (
                        _nativeProviderHandle,
                        cameraParams,
                        InputReader.CurrentPose,
                        out cpuImage,
                        out samplerMatrix
                    );
            }

            /// <summary>
            /// Tries to get a list of the semantic channel names for the current semantic model.
            /// </summary>
            public override bool TryGetChannelNames(out IReadOnlyList<string> names)
            {
                if (_nativeProviderHandle.IsValidHandle() && _api.TryGetChannelNames(_nativeProviderHandle, out _channelNames))
                {
                    names = _channelNames.AsReadOnly();
                    return true;
                }

                names = new List<string>().AsReadOnly();
                return false;
            }

            /// <summary>
            /// Start the provider
            /// </summary>
            public override void Start()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }
                // This should be changed to ConfigureProvider() when TODO [ARDK-737] is completed
                ConfigureProvider();

                _api.Start(_nativeProviderHandle);
            }

            /// <summary>
            /// Stop the provider
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
            /// Destroy the provider
            /// </summary>
            public override void Destroy()
            {
                _channelNames = new List<string>();

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Destruct(_nativeProviderHandle);
                _nativeProviderHandle = IntPtr.Zero;
            }

            // TODO [ARDK-737]: Once it's possible to set channel thresholds outside of calling the
            // TrySetChannelConfidenceThresholds method, this method should be turned into a "ConfigureProvider"
            // method that sets both the requested target frame rate and the requested thresholds.
            // For now, passing on a thresholds list of size = 0 doesn't matter because of the
            // bug [ARDK-653] where thresholds are never configured.
            // Additionally, bug [ARDK-986] means Configure calls are ignored if model initialization is in progress.
            private void ConfigureProvider()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Configure
                (
                    _nativeProviderHandle,
                    TargetFrameRate,
                    0,
                    IntPtr.Zero,
                    SuppressionMaskChannels
                );
            }

            /// <summary>
            /// Sets the confidence threshold for including the specified semantic channel in the packed semantic
            /// channel buffer.
            /// </summary>
            /// <remarks>
            /// Each semantic channel will use its default threshold value chosen by the model until a new value is set
            /// by this function during the AR session.
            /// </remarks>
            /// <param name="channelConfidenceThresholds">
            /// A dictionary consisting of keys specifying the name of the semantics channel that is needed and values
            /// between 0 and 1, inclusive, that set the threshold above which the platform will include the specified
            /// channel in the packed semantics buffer. The key must be a semantic channel name present in the list
            /// returned by <c>TryGetChannelNames</c>.
            /// </param>
            /// <exception cref="System.NotSupportedException">Thrown when setting confidence thresholds is not
            /// supported by the implementation.</exception>
            /// <returns>True if the threshold was set. Otherwise, false.</returns>
            public override bool TrySetChannelConfidenceThresholds(Dictionary<string,float> channelConfidenceThresholds)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                if (!TryGetChannelNames(out var channelNames))
                {
                    return false;
                }

                // Keep all the thresholds the same except for the channels that we want to set
                var confidenceList = new NativeArray<float>(channelNames.Count, Allocator.Temp);
                for (int i = 0; i < confidenceList.Length; i++)
                    confidenceList[i] = _useDefaultConfidenceThreshold;

                foreach (var channelThresholdPair in channelConfidenceThresholds)
                {
                    var confidenceThreshold = channelThresholdPair.Value;
                    var semanticChannelName = channelThresholdPair.Key;

                    if (confidenceThreshold is < 0 or > 1)
                    {
                        Log.Error("Requested confidence " + confidenceThreshold + " is not between 0 and 1");
                        return false;
                    }

                    var index = channelNames.IndexOf(semanticChannelName);

                    if (index < 0)
                    {
                        Log.Error
                        (
                            $"Semantic channel {semanticChannelName} was not found in the list of semantic channels"
                        );

                        return false;
                    }

                    confidenceList[index] = confidenceThreshold;
                }

                // Set config values
                unsafe
                {
                    _api.Configure(_nativeProviderHandle, TargetFrameRate, (uint) confidenceList.Length, (IntPtr) confidenceList.GetUnsafePtr(), SuppressionMaskChannels);
                }

                confidenceList.Dispose();

                return true;
            }

            /// <summary>
            /// Resets the confidence thresholds for all semantic channels to the default values from the current model.
            /// </summary>
            /// <remarks>
            /// This reverts any changes made with <see cref="TrySetChannelConfidenceThresholds"/>.
            /// </remarks>
            /// <exception cref="System.NotSupportedException">Thrown when resetting confidence thresholds is not
            /// supported by the implementation.</exception>
            /// <returns>True if the thresholds were reset. Otherwise, false.</returns>
            public override bool TryResetChannelConfidenceThresholds()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                if (!TryGetChannelNames(out var channelNames))
                {
                    return false;
                }

                var confidenceList = new NativeArray<float>(channelNames.Count, Allocator.Temp);

                // To reset the thresholds list, set all values to a negative number
                for (int i = 0; i < confidenceList.Length; i++)
                    confidenceList[i] = _useDefaultConfidenceThreshold;

                unsafe
                {
                    _api.Configure(_nativeProviderHandle, TargetFrameRate, (uint) confidenceList.Length, (IntPtr) confidenceList.GetUnsafePtr(), SuppressionMaskChannels);
                }

                confidenceList.Dispose();
                return true;
            }
        }
    }
}
