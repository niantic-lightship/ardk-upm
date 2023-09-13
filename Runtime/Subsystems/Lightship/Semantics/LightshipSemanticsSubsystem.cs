// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.ARFoundation.Occlusion;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.SemanticsSubsystem;
using Niantic.Lightship.AR.Subsystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR
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
        static void Register()
        {
            Debug.Log(nameof(LightshipSemanticsSubsystem)+"."+nameof(Register));
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
                        Debug.LogError("Target frame rate value must be greater than zero.");
                        return;
                    }

                    if (_targetFrameRate != value)
                    {
                        _targetFrameRate = value;
                        ConfigureTargetFrameRate();
                    }
                }
            }

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipSemanticsProvider(IApi api)
            {
                Debug.Log("LightshipSemanticsSubsystem.LightshipSemanticsProvider construct");

                _api = api;
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                _nativeProviderHandle = _api.Construct(LightshipUnityContext.UnityContextHandle);
#endif
                Debug.Log("LightshipSemanticsSubsystem got nativeProviderHandle: " + _nativeProviderHandle);

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
            }

            /// <summary>
            /// Gets a semantics channel texture descriptor and a matrix used to fit the texture to the viewport.
            /// </summary>
            /// <param name="channelName">The string description of the semantics channel that is needed.</param>
            /// <param name="cameraParams">Describes the viewport.</param>
            /// <param name="semanticChannelDescriptor">The semantics channel texture descriptor to be populated, if
            /// available from the provider.</param>
            /// <param name="samplerMatrix">Converts from normalized viewport coordinates to normalized texture coordinates.</param>
            /// <returns>
            /// <c>true</c> if the semantics channel texture descriptor is available and is returned. Otherwise,
            /// <c>false</c>.
            /// </returns>
            public override bool TryGetSemanticChannel
            (
                string channelName,
                XRCameraParams? cameraParams,
                out XRTextureDescriptor semanticChannelDescriptor,
                out Matrix4x4 samplerMatrix
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
                    out semanticChannelDescriptor,
                    out samplerMatrix
                );
            }

            /// <summary>
            /// Tries to acquire the latest semantics channel CPU image.
            /// </summary>
            /// <param name="channelName">The string description of the semantics channel that is needed.</param>
            /// <param name="cpuBuffer">If this method returns `true`, an acquired <see cref="LightshipCpuBuffer"/>. The CPU buffer
            /// must be disposed by the caller.</param>
            /// <returns>Returns `true` if an <see cref="LightshipCpuBuffer"/> was successfully acquired.
            /// Returns `false` otherwise.</returns>
            public override bool TryAcquireSemanticChannelCPUImage(string channelName, out LightshipCpuBuffer cpuBuffer)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    cpuBuffer = default;
                    return false;
                }

                return _api.TryAcquireSemanticChannelCPUImage(_nativeProviderHandle, channelName, out cpuBuffer);
            }

            /// <summary>
            /// Calculates a transformation that aligns the pixels in the
            /// image as if it was taken from the latest camera pose.
            /// </summary>
            /// <param name="buffer">CPU buffer for a semantic image.</param>
            /// <param name="cameraParams">Describes the viewport.</param>
            /// <param name="result">The 4x4 transformation matrix that when applied to the image, translates its pixels
            /// such that the image will appear as if it was taken from the latest camera pose.</param>
            /// <returns>True if the transform could be calculated, false if the buffer was invalid or it does not
            /// represent a semantic image.</returns>
            /// <exception cref="NotSupportedException"></exception>
            public override bool TryCalculateSamplerMatrix(LightshipCpuBuffer buffer, XRCameraParams cameraParams, out Matrix4x4 result)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    result = Matrix4x4.identity;
                    return false;
                }

                if (PoseProvider.TryAcquireCurrentPose(out var currentPose))
                {
                    return _api.TryCalculateSamplerMatrix
                    (
                        _nativeProviderHandle,
                        buffer.nativeHandle,
                        cameraParams,
                        currentPose,
                        OcclusionContext.Shared.OccludeeEyeDepth,
                        out result
                    );
                }

                result = Matrix4x4.identity;
                return false;
            }

            /// <summary>
            /// Once a <see cref="LightshipCpuBuffer"/> is acquired by using one of the other methods, you have to dispose
            /// of it once you are done with it as it holds on to a native resource (the memory for the buffer).
            /// </summary>
            ///
            /// <param name="cpuBuffer"> The <see cref="LightshipCpuBuffer"/> you want to dispose of </param>
            public override void DisposeCPUImage(LightshipCpuBuffer cpuBuffer)
            {
                if (!_nativeProviderHandle.IsValidHandle() || !cpuBuffer.valid)
                {
                    return;
                }

                _api.DisposeCPUImage(_nativeProviderHandle, cpuBuffer.nativeHandle);
                cpuBuffer.Dispose();
            }

            /// <summary>
            /// Gets a packed semantics texture descriptor.
            /// </summary>
            /// <param name="cameraParams">Describes the viewport.</param>
            /// <param name="packedSemanticsDescriptor">The packed semantics texture descriptor to be populated, if
            /// available from the provider.</param>
            /// <param name="samplerMatrix">A matrix that converts from viewport to texture coordinates.</param>
            /// <returns>
            /// <c>true</c> if the packed semantics texture descriptor is available and is returned. Otherwise,
            /// <c>false</c>.
            /// </returns>
            public override bool TryGetPackedSemanticChannels
            (
                XRCameraParams? cameraParams,
                out XRTextureDescriptor packedSemanticsDescriptor,
                out Matrix4x4 samplerMatrix
            )
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    packedSemanticsDescriptor = default;
                    samplerMatrix = Matrix4x4.identity;
                    return false;
                }

                return _api.TryGetPackedSemanticChannels(_nativeProviderHandle, cameraParams, out packedSemanticsDescriptor, out samplerMatrix);
            }

            /// <summary>
            ///  Tries to acquire the latest packed semantic channels CPU image.
            /// </summary>
            /// <param name="cpuBuffer">If this method returns `true`, an acquired <see cref="LightshipCpuBuffer"/>. The CPU buffer
            /// must be disposed by the caller.</param>
            /// <returns>True if the CPU image is acquired. Otherwise, false</returns>
            public override bool TryAcquirePackedSemanticChannelsCPUImage(out LightshipCpuBuffer cpuBuffer)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    cpuBuffer = default;
                    return false;
                }

                return _api.TryAcquirePackedSemanticChannelsCPUImage(_nativeProviderHandle, out cpuBuffer);
            }

            /// <summary>
            /// Gets a list of the semantic channel names for the current semantic model.
            /// </summary>
            public override List<string> GetChannelNames()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return new List<string>();
                }

                if (_api.TryGetChannelNames(_nativeProviderHandle, out var channelNames))
                {
                    return channelNames;
                }

                return new List<string>();
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
            /// If the semantic segmentation model is ready, prepare the subsystem's data structures.
            /// </summary>
            /// <returns>
            /// <c>true</c> if the semantic segmentation model is ready and the subsystem has prepared its data structures. Otherwise,
            /// <c>false</c>.
            /// </returns>
            public override bool TryPrepareSubsystem()
            {
                // The model needs to be finished downloading before reading the channel names
                var channelNames = GetChannelNames();
                if (default == channelNames || channelNames.Count == 0)
                    return false;

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                if (!_api.TryPrepareSubsystem(_nativeProviderHandle))
                    return false;

                ConfigureTargetFrameRate();
                return true;
            }

            /// <summary>
            /// Destroy the provider
            /// </summary>
            public override void Destroy()
            {
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
            private void ConfigureTargetFrameRate()
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
                    IntPtr.Zero
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

                var channelNames = GetChannelNames();

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
                        Debug.LogError("Requested confidence " + confidenceThreshold + " is not between 0 and 1");
                        return false;
                    }

                    var index = channelNames.FindIndex(str => str == semanticChannelName);

                    if (index < 0)
                    {
                        Debug.LogError("Semantic channel " + semanticChannelName + " was not found in the " +
                            "list of semantic channels");
                        return false;
                    }

                    confidenceList[index] = confidenceThreshold;
                }

                // Set config values
                unsafe
                {
                    _api.Configure(_nativeProviderHandle, TargetFrameRate, (uint) confidenceList.Length, (System.IntPtr) confidenceList.GetUnsafePtr());
                }

                confidenceList.Dispose();

                return true;
            }
        }
    }
}
