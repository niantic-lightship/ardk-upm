// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Core;
using UnityEngine.XR.ARSubsystems;
using Niantic.Lightship.AR.Utilities.Textures;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

namespace Niantic.Lightship.AR.Subsystems.Scanning
{
    /// <summary>
    /// The Lightship implementation of the <c>XRScanningSubsystem</c>. Do not create this directly.
    /// Use the <c>SubsystemManager</c> instead.
    /// </summary>
    [Preserve]
    public sealed class LightshipScanningSubsystem : XRScanningSubsystem, ISubsystemWithMutableApi<IApi>
    {
        internal class LightshipProvider : Provider
        {
            private const int DefaultRaycastImageWidth = 256;
            private const int DefaultRaycastImageHeight = 256;
            private const TextureFormat DefaultRaycastColorImageFormat = TextureFormat.RGBA32;
            private const TextureFormat DefaultRaycastNormalImageFormat = TextureFormat.RGBA32;
            private const TextureFormat DefaultRaycastPositionImageFormat = TextureFormat.RGBAHalf;

            private IApi _api;
            private XRScanningConfiguration _currentConfiguration = new XRScanningConfiguration();
            private XRScanningState _state;
            private const string TextureRaycastColorPropertyName = "_RaycastColorTexture";
            private const string TextureRaycastNormalPropertyName = "_RaycastNormalTexture";
            private const string TextureRaycastPositionPropertyName = "_RaycastPositionTexture";

            private int _raycastColorTexturePropertyNameID;
            private int _raycastNormalTexturePropertyNameID;
            private int _raycastPositionTexturePropertyNameID;

            private const int FramesInMemory = 1;
            private BufferedTextureCache _colorBufferedTextureCache;
            private BufferedTextureCache _normalBufferedTextureCache;
            private BufferedTextureCache _positionBufferedTextureCache;

            private UInt32 _frameId = 1;

            /// <summary>
            /// The handle to the native version of the provider
            /// </summary>
            private IntPtr _nativeProviderHandle = IntPtr.Zero;

            public LightshipProvider() : this(new NativeApi()) { }

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipProvider(IApi api)
            {
                _api = api;
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                _nativeProviderHandle = _api.Construct(LightshipUnityContext.UnityContextHandle);
#endif
                _state = XRScanningState.Ready;
                _raycastColorTexturePropertyNameID = Shader.PropertyToID(TextureRaycastColorPropertyName);
                _raycastNormalTexturePropertyNameID = Shader.PropertyToID(TextureRaycastNormalPropertyName);
                _raycastPositionTexturePropertyNameID = Shader.PropertyToID(TextureRaycastPositionPropertyName);
                _colorBufferedTextureCache = new BufferedTextureCache(FramesInMemory);
                _normalBufferedTextureCache = new BufferedTextureCache(FramesInMemory);
                _positionBufferedTextureCache = new BufferedTextureCache(FramesInMemory);
            }

            internal void SwitchApiImplementation(IApi api)
            {
                if (_nativeProviderHandle != IntPtr.Zero)
                {
                    _api.Destruct(_nativeProviderHandle);
                }

                _api = api;
                _nativeProviderHandle = api.Construct(LightshipUnityContext.UnityContextHandle);
            }

            public override string GetScanId()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return null;
                }

                bool hasResult = _api.TryGetRecordingInfo(_nativeProviderHandle, out string scanId, out RecordingStatus status);
                return hasResult ? scanId : null;
            }

            public override void SaveCurrentScan()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                if (_state != XRScanningState.Started)
                {
                    Log.Error("Can only save from started state");
                    return;
                }

                _api.SaveCurrentScan(_nativeProviderHandle);
                _state = XRScanningState.Saving;
            }

            public override void DiscardCurrentScan()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                if (_state != XRScanningState.Started)
                {
                    Log.Error("Can only discard from started state");
                    return;
                }

                _api.DiscardCurrentScan(_nativeProviderHandle);
                _state = XRScanningState.Discarding;
            }

            public override void Start()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                if (_state != XRScanningState.Ready && _state != XRScanningState.Stopped)
                {
                    Log.Error($"Can't call Start when current state is {_state}");
                    return;
                }

                _api.Start(_nativeProviderHandle);
                _state = XRScanningState.Started;
            }

            public override void Stop()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Stop(_nativeProviderHandle);
                _state = XRScanningState.Stopped;
            }

            public override void Destroy()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                if (_state != XRScanningState.Ready || _state != XRScanningState.Stopped)
                {
                    Stop();
                }

                _api.Destruct(_nativeProviderHandle);
                _nativeProviderHandle = IntPtr.Zero;
            }

            public override XRScanningState GetState()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return XRScanningState.Error;
                }

                if (_state == XRScanningState.Discarding || _state == XRScanningState.Saving)
                {
                    // Check if these are done
                    if (_api.TryGetRecordingInfo(_nativeProviderHandle, out string scanId, out RecordingStatus status))
                    {
                        if (status == RecordingStatus.Saved)
                        {
                            _state = XRScanningState.Saved;
                        }

                        if (status == RecordingStatus.Discarded)
                        {
                            _state = XRScanningState.Discarded;
                        }

                        if (status == RecordingStatus.Error)
                        {
                            _state = XRScanningState.Error;
                        }
                    }
                }
                return _state;
            }

            public override XRScanningConfiguration CurrentConfiguration
            {
                get => _currentConfiguration;
                set
                {
                    if (!_nativeProviderHandle.IsValidHandle())
                    {
                        return;
                    }

                    if (value.RaycasterVisualizationEnabled || value.VoxelVisualizationEnabled)
                    {
                        if (!value.UseEstimatedDepth)
                        {
                            Log.Error("Disabling depth estimation but " +
                                "enabling visualization is not supported.");
                            value.UseEstimatedDepth = true;
                        }
                    }

                    _currentConfiguration = value;

                    var configurationCStruct = new ScannerConfigurationCStruct();
                    configurationCStruct.Framerate = _currentConfiguration.Framerate;
                    configurationCStruct.EnableRaycastVisualization = _currentConfiguration.RaycasterVisualizationEnabled;
                    configurationCStruct.RaycastWidth = (int)_currentConfiguration.RaycasterVisualizationResolution.x;
                    configurationCStruct.RaycastHeight = (int)_currentConfiguration.RaycasterVisualizationResolution.y;
                    configurationCStruct.EnableVoxelVisualization = _currentConfiguration.VoxelVisualizationEnabled;
                    configurationCStruct.BasePath = _currentConfiguration.ScanBasePath;
                    configurationCStruct.BasePathLen = _currentConfiguration.ScanBasePath.Length;
                    configurationCStruct.ScanTargetId = _currentConfiguration.RawScanTargetId;
                    configurationCStruct.ScanTargetIdLen = _currentConfiguration.RawScanTargetId.Length;
                    configurationCStruct.UseMultidepth = _currentConfiguration.UseEstimatedDepth;
                    configurationCStruct.EnableFullResolution = _currentConfiguration.FullResolutionEnabled;
                    _api.Configure(_nativeProviderHandle, configurationCStruct);
                }
            }

            /// <summary>
            /// Gets the GPU texture description for a raycast image buffer.
            /// </summary>
            /// <param name="raycastBufferDescriptor"></param>
            /// <param name="raycastNormalBufferDescriptor"></param>
            /// <param name="raycastPositionAndConfidenceDescriptor"></param>
            /// <returns></returns>
            public override bool TryGetRaycastBuffer
            (
                out XRTextureDescriptor raycastBufferDescriptor,
                out XRTextureDescriptor raycastNormalBufferDescriptor,
                out XRTextureDescriptor raycastPositionAndConfidenceDescriptor
            )
            {
                raycastBufferDescriptor = default;
                raycastNormalBufferDescriptor = default;
                raycastPositionAndConfidenceDescriptor = default;

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                IntPtr resultPtr = _api.TryGetRaycastBuffer
                (
                    _nativeProviderHandle,
                    out var colorBuffer,
                    out var normalBuffer,
                    out var positionBuffer,
                    out int colorSize,
                    out int normalSize,
                    out int positionSize,
                    out int width,
                    out int height
                );

                if (resultPtr == IntPtr.Zero ||
                    colorBuffer == IntPtr.Zero ||
                    normalBuffer == IntPtr.Zero ||
                    positionBuffer == IntPtr.Zero ||
                    colorSize == 0 ||
                    normalSize == 0 ||
                    positionSize == 0 ||
                    width == 0 ||
                    height == 0)
                {
                    if (resultPtr != IntPtr.Zero)
                    {
                        _api.ReleaseResource(_nativeProviderHandle, resultPtr);
                    }
                    return false;
                }

                // TODO(sxian): Don't use the hardcoded value but get the dimention, format and
                // frameId from GetRaycasterImage().
                _frameId = _frameId + 1;
                var raycastTex =
                    _colorBufferedTextureCache.GetUpdatedTextureFromBuffer
                    (
                        colorBuffer,
                        colorSize,
                        width,
                        height,
                        DefaultRaycastColorImageFormat,
                        _frameId
                    );

                raycastBufferDescriptor =
                    new XRTextureDescriptor
                    (
                        raycastTex.GetNativeTexturePtr(),
                        width,
                        height,
                        0,
                        DefaultRaycastColorImageFormat,
                        _raycastColorTexturePropertyNameID,
                        0,
                        TextureDimension.Tex2D
                    );

                var normalsTex =
                    _normalBufferedTextureCache.GetUpdatedTextureFromBuffer
                    (
                        normalBuffer,
                        normalSize,
                        width,
                        height,
                        DefaultRaycastColorImageFormat,
                        _frameId
                    );

                raycastNormalBufferDescriptor =
                    new XRTextureDescriptor
                    (
                        normalsTex.GetNativeTexturePtr(),
                        width,
                        height,
                        0,
                        DefaultRaycastNormalImageFormat,
                        _raycastNormalTexturePropertyNameID,
                        0,
                        TextureDimension.Tex2D
                    );

                var positionsTex =
                    _positionBufferedTextureCache.GetUpdatedTextureFromBuffer
                    (
                        positionBuffer,
                        positionSize,
                        width,
                        height,
                        DefaultRaycastPositionImageFormat,
                        _frameId
                    );

                raycastPositionAndConfidenceDescriptor =
                    new XRTextureDescriptor
                    (
                        positionsTex.GetNativeTexturePtr(),
                        width,
                        height,
                        0,
                        DefaultRaycastPositionImageFormat,
                        _raycastPositionTexturePropertyNameID,
                        0,
                        TextureDimension.Tex2D
                    );

                _api.ReleaseResource(_nativeProviderHandle, resultPtr);
                return true;
            }

            /// <summary>
            /// Gets the last computed version of the voxel data.
            /// </summary>
            /// <param name="voxelData">The output voxel data.</param>
            /// <returns></returns>
            public override bool TryGetVoxelBuffer(out XRScanningVoxelData voxelData)
            {
                voxelData = default;
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                IntPtr handlePtr = _api.TryGetVoxelBuffer(_nativeProviderHandle, out var positionBuffer, out var colorBuffer, out int pointCount);
                if (handlePtr == IntPtr.Zero)
                {
                    return false;
                }

                voxelData = new XRScanningVoxelData(positionBuffer, colorBuffer, pointCount, handlePtr);
                return true;
            }

            public override void ComputeVoxels()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.ComputeVoxels(_nativeProviderHandle);
            }

            public override void DisposeVoxelBuffer(XRScanningVoxelData voxelData)
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.ReleaseResource(_nativeProviderHandle, voxelData.nativeHandle);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDescriptor()
        {
            var cinfo = new XRScanningSubsystemDescriptor.Cinfo
            {
                id = "Lightship-Scanning",
                providerType = typeof(LightshipProvider),
                subsystemTypeOverride = typeof(LightshipScanningSubsystem),
            };

            XRScanningSubsystemDescriptor.Create(cinfo);
        }

        void ISubsystemWithMutableApi<IApi>.SwitchApiImplementation(IApi api)
        {
            ((LightshipProvider) provider).SwitchApiImplementation(api);
        }

        void ISubsystemWithMutableApi<IApi>.SwitchToInternalMockImplementation()
        {
            ((LightshipProvider) provider).SwitchApiImplementation(new MockApi());
        }
    }
}
