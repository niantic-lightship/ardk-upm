// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Textures;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;
using Random = System.Random;

namespace Niantic.Lightship.AR.Subsystems.Semantics
{
    internal class MockApi : IApi
    {
        // Textures
        internal static int _width = 256;
        internal static int _height = 144;
        private const int TextureBufferSize = 2;
        private readonly BufferedTextureCache _semanticsChannelBufferedTextureCache = new (TextureBufferSize);
        private readonly BufferedTextureCache _packedSemanticsBufferedTextureCache = new (TextureBufferSize);
		private readonly BufferedTextureCache _suppressionMaskTextureBufferedTextureCache = new (TextureBufferSize);

        private NativeArray<float> _semanticsChannelCpu;
        private NativeArray<Int32> _packedSemanticsCpu;
		private NativeArray<UInt32> _suppressionMaskCpu;
        private NativeArray<byte> _binaryMaskCpu;
        private bool _initializedPackedBuffer;

        private const string TextureMockSemanticChannelPropertyName = "_SemanticSegmentationChannel";
        private const string TextureMockSemanticPackedPropertyName = "_SemanticSegmentationThresholdBitmask";
        private const string TextureMockSemanticBinaryMaskPropertyName = "_SemanticSegmentationBinaryMask";
		private const string TextureMockSuppressionMaskPropertyName = "_SemanticSegmentationSuppressionMask";

        private uint _frameNumber = 0;
        private readonly Random _random = new();

        // Metadata
        private const int ModelDecryptionTimeInSeconds = 1;
        private bool _started = false;
        private bool _hasMetadata = false;
        private float _startTime;

		private UInt32 _suppressionMaskChannels = 0;

        private XRCpuImage.Api _cpuImageApi => LightshipCpuImageApi.Instance;

        public MockApi()
        {
            _semanticsChannelCpu = new NativeArray<float>(_width * _height, Allocator.Persistent);
            _packedSemanticsCpu = new NativeArray<Int32>(_width * _height, Allocator.Persistent);
			_suppressionMaskCpu = new NativeArray<UInt32>(_width * _height, Allocator.Persistent);
            _binaryMaskCpu = new NativeArray<byte>(_width * _height, Allocator.Persistent);

            SetSemanticChannelToValue(1.0f);
        }

        public IntPtr Construct(IntPtr unityContext)
        {
            return new IntPtr(100);
        }

        public void Start(IntPtr nativeProviderHandle)
        {
            _started = true;
            _startTime = Time.unscaledTime;
            MonoBehaviourEventDispatcher.Updating.AddListener(WaitForModelDecryption);
        }

        public void Stop(IntPtr nativeProviderHandle)
        {
            _started = false;
            _hasMetadata = false;
            MonoBehaviourEventDispatcher.Updating.RemoveListener(IncrementFrameId);
            MonoBehaviourEventDispatcher.Updating.RemoveListener(WaitForModelDecryption);
        }

        private void WaitForModelDecryption()
        {
            if (Time.unscaledTime - _startTime > ModelDecryptionTimeInSeconds)
            {
                _hasMetadata = true;
                MonoBehaviourEventDispatcher.Updating.RemoveListener(WaitForModelDecryption);
                MonoBehaviourEventDispatcher.Updating.AddListener(IncrementFrameId);
            }
        }

        private void IncrementFrameId()
        {
            _frameNumber += 1;
        }

        public void Configure(IntPtr nativeProviderHandle, UInt32 framesPerSecond, UInt32 numThresholds, IntPtr thresholds, List<string> suppressionMaskChannelNames) {
            InitializeSuppressionMaskBuffer(GetFlags(suppressionMaskChannelNames));
		}

        public void Destruct(IntPtr nativeProviderHandle)
        {
            _started = false;
            _packedSemanticsCpu.Dispose();
            _semanticsChannelCpu.Dispose();
            _suppressionMaskCpu.Dispose();
            _binaryMaskCpu.Dispose();
            _packedSemanticsBufferedTextureCache.Dispose();
            _semanticsChannelBufferedTextureCache.Dispose();
        }

        public bool TryGetSemanticChannel
        (
            IntPtr nativeProviderHandle,
            string channelName,
            XRCameraParams? cameraParams,
            Matrix4x4? currentPose,
            out XRTextureDescriptor semanticsChannelDescriptor,
            out Matrix4x4 samplerMatrix
        )
        {
            semanticsChannelDescriptor = default;
            samplerMatrix = Matrix4x4.identity;

            if (!_started)
                return false;

            // Set the center of the channel texture to a random value each request
            int randomInt = _random.Next(1, 9);
            float randomConfidenceValue = 1.0f / randomInt;
            SetSemanticChannelToValue(randomConfidenceValue);
            unsafe
            {
                var nativePtr = (IntPtr) _semanticsChannelCpu.GetUnsafePtr();

                var tex = _semanticsChannelBufferedTextureCache.GetUpdatedTextureFromBuffer
                (
                    nativePtr,
                    _semanticsChannelCpu.Length * sizeof(float),
                    _width,
                    _height,
                    TextureFormat.RFloat,
                    _frameNumber
                );

                if (cameraParams.HasValue)
                {
                    var viewport = cameraParams.Value;
                    samplerMatrix =
                        CameraMath.CalculateDisplayMatrix
                        (
                            _width,
                            _height,
                            (int)viewport.screenWidth,
                            (int)viewport.screenHeight,
                            viewport.screenOrientation,
                            true
                        );
                }

                semanticsChannelDescriptor =
                    new XRTextureDescriptor
                    (
                        tex.GetNativeTexturePtr(),
                        _width,
                        _height,
                        0,
                        TextureFormat.RFloat,
                        Shader.PropertyToID(TextureMockSemanticChannelPropertyName),
                        0,
                        TextureDimension.Tex2D
                    );
            }

            _frameNumber++;

            return true;
        }

        public bool TryAcquireSemanticChannelCpuImage
        (
            IntPtr nativeProviderHandle,
            string channelName,
            XRCameraParams? cameraParams,
            Matrix4x4? currentPose,
            out XRCpuImage cpuImage,
            out Matrix4x4 samplerMatrix
        )
        {
            cpuImage = default;
            samplerMatrix = Matrix4x4.identity;

            if (!_hasMetadata)
            {
                return false;
            }

            // Set the center of the channel texture to a random value each request
            int randomInt = _random.Next(1, 9);
            float randomConfidenceValue = 1.0f / randomInt;
            SetSemanticChannelToValue(randomConfidenceValue);

            unsafe
            {
                var nativePtr = (IntPtr) _semanticsChannelCpu.GetUnsafePtr();

                cpuImage = default; // new LightshipCpuBuffer(nativeProviderHandle, nativePtr, new Vector2Int(_width, _height), LightshipCpuBuffer.Format.DepthFloat32);
            }

            return true;
        }

        public bool TryGetPackedSemanticChannels
        (
            IntPtr nativeProviderHandle,
            XRCameraParams? cameraParams,
            Matrix4x4? currentPose,
            out XRTextureDescriptor packedSemanticsDescriptor,
            out Matrix4x4 samplerMatrix
        )
        {
            packedSemanticsDescriptor = default;
            samplerMatrix = Matrix4x4.identity;

            if (!_hasMetadata)
                return false;

            unsafe
            {
                var nativePtr = (IntPtr)_packedSemanticsCpu.GetUnsafePtr();

                var tex = _packedSemanticsBufferedTextureCache.GetUpdatedTextureFromBuffer
                (
                    nativePtr,
                    _packedSemanticsCpu.Length * sizeof(Int32),
                    _width,
                    _height,
                    TextureFormat.RFloat,
                    _frameNumber
                );

                if (cameraParams.HasValue)
                {
                    var viewport = cameraParams.Value;
                    samplerMatrix = CameraMath.CalculateDisplayMatrix
                    (
                        _width,
                        _height,
                        (int)viewport.screenWidth,
                        (int)viewport.screenHeight,
                        viewport.screenOrientation,
                        true
                    );
                }

                packedSemanticsDescriptor =
                    new XRTextureDescriptor
                    (
                        tex.GetNativeTexturePtr(),
                        _width,
                        _height,
                        0,
                        TextureFormat.RFloat,
                        Shader.PropertyToID(TextureMockSemanticPackedPropertyName),
                        0,
                        TextureDimension.Tex2D
                    );
            }

            _frameNumber++;

            return true;
        }

        public bool TryAcquirePackedSemanticChannelsCpuImage
        (
            IntPtr nativeProviderHandle,
            XRCameraParams? cameraParams,
            Matrix4x4? currentPose,
            out XRCpuImage cpuImage,
            out Matrix4x4 samplerMatrix
        )
        {
            cpuImage = default;
            samplerMatrix = Matrix4x4.identity;

            if (!_hasMetadata)
            {

                return false;
            }

            unsafe
            {
                var nativePtr = (int) _packedSemanticsCpu.GetUnsafePtr();
                var dimensions = new Vector2Int(_width, _height);
                var cinfo = new XRCpuImage.Cinfo(nativePtr, dimensions, 1, Time.unscaledTime, XRCpuImage.Format.OneComponent32);
                cpuImage = new XRCpuImage(_cpuImageApi, cinfo);
            }

            return true;
        }

        public bool TryCalculateSamplerMatrix
        (
            IntPtr nativeProviderHandle,
            IntPtr resourceHandle,
            XRCameraParams cameraParams,
            Matrix4x4 pose,
            float backProjectionPlane,
            out Matrix4x4 result
        )
        {
            result = Matrix4x4.identity;
            return true;
        }

        public bool TryGetChannelNames(IntPtr nativeProviderHandle, out List<string> semanticChannelNames)
        {
            if (!_hasMetadata)
            {
                semanticChannelNames = default;
                return false;
            }

            semanticChannelNames = new();
            semanticChannelNames.Add("sky");
            semanticChannelNames.Add("ground");
            semanticChannelNames.Add("natural_ground");
            semanticChannelNames.Add("artificial_ground");
            semanticChannelNames.Add("water");
            semanticChannelNames.Add("person");
            semanticChannelNames.Add("building");
            semanticChannelNames.Add("foliage");
            semanticChannelNames.Add("grass");
            semanticChannelNames.Add("flower_experimental");
            semanticChannelNames.Add("tree_trunk_experimental");
            semanticChannelNames.Add("pet_experimental");
            semanticChannelNames.Add("sand_experimental");
            semanticChannelNames.Add("tv_experimental");
            semanticChannelNames.Add("dirt_experimental");
            semanticChannelNames.Add("vehicle_experimental");
            semanticChannelNames.Add("food_experimental");
            semanticChannelNames.Add("loungeable_experimental");
            semanticChannelNames.Add("snow_experimental");

            if (!_initializedPackedBuffer)
                InitializePackedBuffer(semanticChannelNames);

            return true;
        }

        public bool TryGetLatestFrameId(IntPtr nativeProviderHandle, out uint frameId)
        {
            if (_hasMetadata)
            {
                // Magic number 2. Proper (but unneeded?) implementation would update per
                // the configured frame rate
                frameId = _frameNumber / 2;
                return true;
            }

            frameId = 0;
            return false;
        }

        public bool TryGetLatestIntrinsicsMatrix(IntPtr nativeProviderHandle, out Matrix4x4 intrinsicsMatrix)
        {
            intrinsicsMatrix = Matrix4x4.identity;

            return true;
        }

        public bool HasMetadata(IntPtr nativeProviderHandle)
        {
            return _hasMetadata;
        }

        public bool TryGetSuppressionMaskTexture
		(
			IntPtr nativeProviderHandle,
			XRCameraParams? cameraParams,
            Matrix4x4? currentPose,
			out XRTextureDescriptor suppressionMaskDescriptor,
            out Matrix4x4 samplerMatrix)
        {
			suppressionMaskDescriptor = default;
            samplerMatrix = Matrix4x4.identity;

            if (!_hasMetadata)
                return false;

            unsafe
            {
                var nativePtr = (IntPtr)_binaryMaskCpu.GetUnsafePtr();

				var tex = _suppressionMaskTextureBufferedTextureCache.GetUpdatedTextureFromBuffer
				(
	                nativePtr,
					_suppressionMaskCpu.Length * sizeof(UInt32),
					_width,
					_height,
					TextureFormat.R8,
					_frameNumber
				);

				if (cameraParams.HasValue)
                {
                    var viewport = cameraParams.Value;
                    samplerMatrix = CameraMath.CalculateDisplayMatrix(_width, _height, (int)viewport.screenWidth,
                        (int)viewport.screenHeight, viewport.screenOrientation, true);
                }

				suppressionMaskDescriptor =
                    new XRTextureDescriptor
                    (
                        tex.GetNativeTexturePtr(),
                        _width,
                        _height,
                        0,
                        TextureFormat.R8,
                        Shader.PropertyToID(TextureMockSuppressionMaskPropertyName),
                        0,
                        TextureDimension.Tex2D
                    );
            }

            _frameNumber++;

            return true;
        }

        public bool TryAcquireSuppressionMaskCpuImage
		(
			IntPtr nativeProviderHandle,
			XRCameraParams? cameraParams,
            Matrix4x4? currentPose,
            out XRCpuImage cpuImage,
			out Matrix4x4 samplerMatrix
		)
        {
            cpuImage = default;
            samplerMatrix = Matrix4x4.identity;

            if (!_hasMetadata)
            {

                return false;
            }

            unsafe
            {
                var nativePtr = (int) _suppressionMaskCpu.GetUnsafePtr();
                var dimensions = new Vector2Int(_width, _height);
                var cinfo = new XRCpuImage.Cinfo(nativePtr, dimensions, 1, Time.unscaledTime, XRCpuImage.Format.OneComponent8);
                cpuImage = new XRCpuImage(_cpuImageApi, cinfo);
            }

            return true;
        }

        public uint GetFlags(IEnumerable<string> channels)
        {
            return UInt32.MaxValue;
        }

        private void SetSemanticChannelToValue(float value)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    // Mark the center third as a different value for a visual check if the texture is updating
                    if (x > _width * .33f && x < _width * .66f && y > _height * .33f && y < _height * .66f)
                        _semanticsChannelCpu[y * _width + x] = value;
                    else
                        _semanticsChannelCpu[y * _width + x] = 0;
                }
            }
        }

        private void InitializePackedBuffer(List<string> semanticChannelNames)
        {
            Debug.Assert(default != semanticChannelNames && semanticChannelNames.Count > 0,
                "For the sake of the mock, it's expected that channel names are available at the time that InitializePackedBuffer is called");
            int allChannelsBitmask = 0;

            // Claim that all channels are detected for simplicity
            for (int channel = 0; channel < semanticChannelNames.Count; channel++)
            {
                allChannelsBitmask |= 1 << (31 - channel);
            }

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    // Mark the center third as a different value for a visual check if the texture is updating
                    if (x > _width * .33f && x < _width * .66f && y > _height * .33f && y < _height * .66f)
                        _packedSemanticsCpu[y * _width + x] = allChannelsBitmask;
                    else
                        _packedSemanticsCpu[y * _width + x] = 0;
                }
            }


            _initializedPackedBuffer = true;
        }

		private void InitializeSuppressionMaskBuffer(UInt32 suppressionMaskChannels)
	    {
			for (int y=0; y < _height; y++)
            {
                for (int x=0; x < _width; x++)
                {
                    // Mark the center third as a different value for a visual check if the texture is updating
                    if (x > _width * .33f && x < _width * .66f && y > _height * .33f && y < _height * .66f)
                        _suppressionMaskCpu[y * _width + x] = suppressionMaskChannels;
                    else
                        _suppressionMaskCpu[y * _width + x] = 0;
                }
            }
		}
    }
}
