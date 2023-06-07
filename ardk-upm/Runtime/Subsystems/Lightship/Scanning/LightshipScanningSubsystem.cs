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
    public sealed class LightshipScanningSubsystem : XRScanningSubsystem
    {
        internal class LightshipProvider : Provider
        {
            private const int DEFAULT_RAYCAST_IMAGE_WIDTH = 256;
            private const int DEFAULT_RAYCAST_IMAGE_HEIGHT = 256;
            private const TextureFormat DEFAULT_RAYCAST_IMAGE_FORMAT = TextureFormat.RGBA32;

            private IApi _api;
            private XRScanningConfiguration _currentConfiguration = new XRScanningConfiguration();
            private XRScanningState _state;
            private const string _kTextureRaycastPropertyName = "_RaycastTexture";
            private int _raycastTexturePropertyNameID;
            private const int _kFramesInMemory = 1;
            private BufferedTextureCache _raycastBufferedTextureCache;
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
                _raycastTexturePropertyNameID = Shader.PropertyToID(_kTextureRaycastPropertyName);
                _raycastBufferedTextureCache = new BufferedTextureCache(_kFramesInMemory);
            }

            internal bool _SwitchToMockImplementation(IApi api)
            {
                if (_nativeProviderHandle != IntPtr.Zero)
                {
                    _api.Destruct(_nativeProviderHandle);
                    _nativeProviderHandle = IntPtr.Zero;
                }

                _api = api ?? new MockApi();
                return true;
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
            }

            public override XRScanningState GetState()
            {
                return _state;
            }

            public override XRScanningConfiguration CurrentConfiguration
            {
                get => _currentConfiguration;
                set
                {
                    _currentConfiguration = value;
                    _api.Configure(_nativeProviderHandle,
                        _currentConfiguration.Framerate,
                        _currentConfiguration.RaycasterVisualizationEnabled,
                        (int)_currentConfiguration.RaycasterVisualizationResolution.x,
                        (int)_currentConfiguration.RaycasterVisualizationResolution.y,
                        _currentConfiguration.VoxelVisualizationEnabled);
                }
            }

            /// <summary>
            /// Gets the GPU texture description for a raycast image buffer.
            /// </summary>
            /// <param name="semanticsChannelDescriptor">The raycast buffer texture descriptor provided by the scanning provider .</param>
            public override bool TryGetRaycastBuffer(out XRTextureDescriptor raycastBufferDescriptor)
            {
                raycastBufferDescriptor = default;
                _api.TryGetRaycastBuffer(_nativeProviderHandle, out var memoryBuffer, out int size, out int width,
                    out int height);
                if (memoryBuffer == IntPtr.Zero || size == 0 || width == 0 || height == 0)
                {
                    return false;
                }

                // TODO(sxian): Don't use the hardcoded value but get the dimention, format and
                // frameId from GetRaycasterImage().
                _frameId = _frameId + 1;
                var texture = _raycastBufferedTextureCache.GetUpdatedTextureFromBuffer(
                    memoryBuffer, (int)size, width, height,
                    DEFAULT_RAYCAST_IMAGE_FORMAT, _frameId, out IntPtr nativeTexturePtr);

                raycastBufferDescriptor = new XRTextureDescriptor(nativeTexturePtr, width,
                    height, 0, DEFAULT_RAYCAST_IMAGE_FORMAT,
                    _raycastTexturePropertyNameID, 0, TextureDimension.Tex2D);
                return true;
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
    }
}
