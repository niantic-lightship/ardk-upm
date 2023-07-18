// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Niantic.Lightship.AR.ScanningSubsystem;
using UnityEngine.XR.ARSubsystems;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

namespace Niantic.Lightship.AR
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
            private const int DEFAULT_RAYCAST_IMAGE_WIDTH = 256;
            private const int DEFAULT_RAYCAST_IMAGE_HEIGHT = 256;
            private const TextureFormat DEFAULT_RAYCAST_COLOR_IMAGE_FORMAT = TextureFormat.RGBA32;
            private const TextureFormat DEFAULT_RAYCAST_NORMAL_IMAGE_FORMAT = TextureFormat.RGBA32;
            private const TextureFormat DEFAULT_RAYCAST_POSITION_IMAGE_FORMAT = TextureFormat.RGBAHalf;

            private IApi _api;
            private XRScanningConfiguration _currentConfiguration = new XRScanningConfiguration();
            private XRScanningState _state;
            private const string _kTextureRaycastColorPropertyName = "_RaycastColorTexture";
            private const string _kTextureRaycastNormalPropertyName = "_RaycastNormalTexture";
            private const string _kTextureRaycastPositionPropertyName = "_RaycastPositionTexture";

            private int _raycastColorTexturePropertyNameID;
            private int _raycastNormalTexturePropertyNameID;
            private int _raycastPositionTexturePropertyNameID;

            private const int _kFramesInMemory = 1;
            private BufferedTextureCache _colorBufferedTextureCache;
            private BufferedTextureCache _normalBufferedTextureCache;
            private BufferedTextureCache _positionBufferedTextureCache;

            private UInt32 _frameId = 1;

            /// <summary>
            /// The handle to the native version of the provider
            /// </summary>
            private IntPtr _nativeProviderHandle = IntPtr.Zero;

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipProvider()
            {
                _api = new NativeApi();
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                _nativeProviderHandle = _api.Construct(LightshipUnityContext.UnityContextHandle);
#endif
                _state = XRScanningState.Ready;
                _raycastColorTexturePropertyNameID = Shader.PropertyToID(_kTextureRaycastColorPropertyName);
                _raycastNormalTexturePropertyNameID = Shader.PropertyToID(_kTextureRaycastNormalPropertyName);
                _raycastPositionTexturePropertyNameID = Shader.PropertyToID(_kTextureRaycastPositionPropertyName);
                _colorBufferedTextureCache = new BufferedTextureCache(_kFramesInMemory);
                _normalBufferedTextureCache = new BufferedTextureCache(_kFramesInMemory);
                _positionBufferedTextureCache = new BufferedTextureCache(_kFramesInMemory);
            }

            internal void SwitchApiImplementation(IApi api)
            {
                if (_nativeProviderHandle != IntPtr.Zero)
                {
                    _api.Destruct(_nativeProviderHandle);
                    _nativeProviderHandle = IntPtr.Zero;
                }

                _api = api;
            }

            public override string GetScanId()
            {
                bool hasResult = _api.TryGetRecordingInfo(_nativeProviderHandle, out string scanId, out RecordingStatus status);
                return hasResult ? scanId : null;
            }

            public override void SaveCurrentScan()
            {
                if (_state != XRScanningState.Started)
                {
                    Debug.LogError("Can only save from started state");
                    return;
                }
                _api.SaveCurrentScan(_nativeProviderHandle);
                _state = XRScanningState.Saving;
            }

            public override void DiscardCurrentScan()
            {
                if (_state != XRScanningState.Started)
                {
                    Debug.LogError("Can only discard from started state");
                    return;
                }
                _api.DiscardCurrentScan(_nativeProviderHandle);
                _state = XRScanningState.Discarding;
            }

            public override void Start()
            {
                if (_state != XRScanningState.Ready && _state != XRScanningState.Stopped)
                {
                    Debug.LogError($"Can't call Start when current state is {_state}");
                    return;
                }

                _api.Start(_nativeProviderHandle);
                _state = XRScanningState.Started;
            }

            public override void Stop()
            {
                _api.Stop(_nativeProviderHandle);
                _state = XRScanningState.Stopped;
            }

            public override void Destroy()
            {
                if (_state != XRScanningState.Ready || _state != XRScanningState.Stopped)
                {
                    Stop();
                }

                _api.Destruct(_nativeProviderHandle);
                _nativeProviderHandle = IntPtr.Zero;
                _api = null;
            }

            public override XRScanningState GetState()
            {
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
                    _currentConfiguration = value;
                    _api.Configure
                    (
                        _nativeProviderHandle,
                        _currentConfiguration.Framerate,
                        _currentConfiguration.RaycasterVisualizationEnabled,
                        (int)_currentConfiguration.RaycasterVisualizationResolution.x,
                        (int)_currentConfiguration.RaycasterVisualizationResolution.y,
                        _currentConfiguration.VoxelVisualizationEnabled,
                        _currentConfiguration.ScanBasePath
                    );
                }
            }

            /// <summary>
            /// Gets the GPU texture description for a raycast image buffer.
            /// </summary>
            /// <param name="raycastBufferDescriptor"></param>
            /// <param name="raycastNormalBufferDescriptor"></param>
            /// <param name="raycastPositionAndConfidenceDescriptor"></param>
            /// <returns></returns>
            public override bool TryGetRaycastBuffer(out XRTextureDescriptor raycastBufferDescriptor,
                out XRTextureDescriptor raycastNormalBufferDescriptor,
                out XRTextureDescriptor raycastPositionAndConfidenceDescriptor)
            {
                raycastBufferDescriptor = default;
                raycastNormalBufferDescriptor = default;
                raycastPositionAndConfidenceDescriptor = default;
                if (_api == null)
                {
                    return false;
                }
                IntPtr resultPtr = _api.TryGetRaycastBuffer(_nativeProviderHandle, out var colorBuffer,out var normalBuffer, out var positionBuffer, out int colorSize, out int normalSize, out int positionSize,
                    out int width, out int height);
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
                _colorBufferedTextureCache.GetUpdatedTextureFromBuffer(
                    colorBuffer, colorSize, width, height,
                    DEFAULT_RAYCAST_COLOR_IMAGE_FORMAT, _frameId, out IntPtr nativeColorTexturePtr);

                raycastBufferDescriptor = new XRTextureDescriptor(nativeColorTexturePtr, width,
                    height, 0, DEFAULT_RAYCAST_COLOR_IMAGE_FORMAT,
                    _raycastColorTexturePropertyNameID, 0, TextureDimension.Tex2D);

                _normalBufferedTextureCache.GetUpdatedTextureFromBuffer(
                    normalBuffer, normalSize, width, height,
                    DEFAULT_RAYCAST_COLOR_IMAGE_FORMAT, _frameId, out IntPtr nativeNormalTexturePtr);

                raycastNormalBufferDescriptor = new XRTextureDescriptor(nativeNormalTexturePtr, width,
                    height, 0, DEFAULT_RAYCAST_NORMAL_IMAGE_FORMAT,
                    _raycastNormalTexturePropertyNameID, 0, TextureDimension.Tex2D);

                _positionBufferedTextureCache.GetUpdatedTextureFromBuffer(
                    positionBuffer, positionSize, width, height,
                    DEFAULT_RAYCAST_POSITION_IMAGE_FORMAT, _frameId, out IntPtr nativePositionTexturePtr);

                raycastPositionAndConfidenceDescriptor = new XRTextureDescriptor(nativePositionTexturePtr, width,
                    height, 0, DEFAULT_RAYCAST_POSITION_IMAGE_FORMAT,
                    _raycastPositionTexturePropertyNameID, 0, TextureDimension.Tex2D);

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
                if (_api == null)
                {
                    return false;
                }

                IntPtr handlePtr = _api.TryGetVoxelBuffer(_nativeProviderHandle, out var positionBuffer,
                    out var colorBuffer, out int pointCount);
                if (handlePtr == IntPtr.Zero)
                {
                    return false;
                }

                voxelData = new XRScanningVoxelData(positionBuffer, colorBuffer, pointCount, handlePtr);
                return true;
            }

            public override void ComputeVoxels()
            {
                _api.ComputeVoxels(_nativeProviderHandle);
            }

            public override void DisposeVoxelBuffer(XRScanningVoxelData voxelData)
            {
                _api.ReleaseResource(_nativeProviderHandle, voxelData.nativeHandle);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor()
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
