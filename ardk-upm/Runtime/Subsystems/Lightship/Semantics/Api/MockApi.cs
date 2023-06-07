using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;
using Random = System.Random;

namespace Niantic.Lightship.AR.SemanticsSubsystem
{
    internal class MockApi : IApi
    {
        internal static int _width = 256;
        internal static int _height = 144;
        private const int _textureBufferSize = 2;
        private BufferedTextureCache _semanticsChannelBufferedTextureCache;
        private BufferedTextureCache _packedSemanticsBufferedTextureCache;
        private NativeArray<float> _semanticsChannelCpu;
        private NativeArray<Int32> _packedSemanticsCpu;
        private const string _textureMockSemanticChannelPropertyName = "_SemanticSegmentationChannel";
        private const string _textureMockSemanticPackedPropertyName = "_SemanticSegmentationThresholdBitmask";
        private uint _frameNumber = 0;
        private Random _random = new();

        public MockApi()
        {
            _semanticsChannelBufferedTextureCache = new BufferedTextureCache(_textureBufferSize);
            _packedSemanticsBufferedTextureCache = new BufferedTextureCache(_textureBufferSize);
            _semanticsChannelCpu = new NativeArray<float>(_width * _height, Allocator.Persistent);
            _packedSemanticsCpu = new NativeArray<Int32>(_width * _height, Allocator.Persistent);

            SetSemanticChannelToValue(1.0f);
            InitializePackedBuffer();
        }

        public IntPtr Construct(IntPtr unityContext)
        {
            throw new NotImplementedException();
        }

        public void Start(IntPtr nativeProviderHandle) {}

        public void Stop(IntPtr nativeProviderHandle) {}

        public void Configure(IntPtr nativeProviderHandle, UInt32 framesPerSecond, UInt32 numThresholds, IntPtr thresholds) {}

        public void Destruct(IntPtr nativeProviderHandle)
        {
            _packedSemanticsCpu.Dispose();
            _semanticsChannelCpu.Dispose();
        }

        public bool TryGetSemanticChannel(IntPtr nativeProviderHandle, string channelName,
            out XRTextureDescriptor semanticsChannelDescriptor)
        {
            // Set the center of the channel texture to a random value each request
            int randomInt = _random.Next(1, 9);
            float randomConfidenceValue = 1.0f / randomInt;
            SetSemanticChannelToValue(randomConfidenceValue);
            unsafe
            {
                var nativePtr = (IntPtr) _semanticsChannelCpu.GetUnsafePtr();

                _semanticsChannelBufferedTextureCache.GetUpdatedTextureFromBuffer
                (
                    nativePtr,
                    _semanticsChannelCpu.Length * sizeof(float),
                    _width,
                    _height,
                    TextureFormat.RFloat,
                    _frameNumber,
                    out IntPtr nativeTexturePtr
                );

                semanticsChannelDescriptor =
                    new XRTextureDescriptor
                    (
                        nativeTexturePtr,
                        _width,
                        _height,
                        0,
                        TextureFormat.RFloat,
                        Shader.PropertyToID(_textureMockSemanticChannelPropertyName),
                        0,
                        TextureDimension.Tex2D
                    );
            }

            _frameNumber++;

            return true;
        }

        public bool TryAcquireSemanticChannelCPUImage(IntPtr nativeProviderHandle, string channelName,
            out LightshipCpuBuffer cpuBuffer)
        {
            // Set the center of the channel texture to a random value each request
            int randomInt = _random.Next(1, 9);
            float randomConfidenceValue = 1.0f / randomInt;
            SetSemanticChannelToValue(randomConfidenceValue);

            unsafe
            {
                var nativePtr = (IntPtr) _semanticsChannelCpu.GetUnsafePtr();

                cpuBuffer = new LightshipCpuBuffer(nativeProviderHandle, nativePtr, new Vector2Int(_width, _height), LightshipCpuBuffer.Format.DepthFloat32);
            }

            return true;
        }

        public void DisposeCPUImage(IntPtr nativeProviderHandle, IntPtr resHandle) {}

        public bool TryGetPackedSemanticChannels(IntPtr nativeProviderHandle, out XRTextureDescriptor packedSemanticsDescriptor)
        {
            unsafe
            {
                var nativePtr = (IntPtr) _packedSemanticsCpu.GetUnsafePtr();

                _packedSemanticsBufferedTextureCache.GetUpdatedTextureFromBuffer
                (
                    nativePtr,
                    _packedSemanticsCpu.Length * sizeof(Int32),
                    _width,
                    _height,
                    TextureFormat.RGBA32,
                    _frameNumber,
                    out IntPtr nativeTexturePtr
                );

                packedSemanticsDescriptor =
                    new XRTextureDescriptor
                    (
                        nativeTexturePtr,
                        _width,
                        _height,
                        0,
                        TextureFormat.RGBA32,
                        Shader.PropertyToID(_textureMockSemanticPackedPropertyName),
                        0,
                        TextureDimension.Tex2D
                    );
            }

            _frameNumber++;

            return true;
        }

        public bool TryAcquirePackedSemanticChannelsCPUImage(IntPtr nativeProviderHandle, out LightshipCpuBuffer cpuBuffer)
        {
            unsafe
            {
                var nativePtr = (IntPtr) _packedSemanticsCpu.GetUnsafePtr();

                cpuBuffer = new LightshipCpuBuffer(nativeProviderHandle, nativePtr, new Vector2Int(_width, _height), LightshipCpuBuffer.Format.BitMask32);
            }

            return true;
        }

        public bool GetChannelNames(IntPtr nativeProviderHandle, out List<string> semanticChannelNames)
        {
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
            return true;
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

        private void InitializePackedBuffer()
        {
            GetChannelNames(IntPtr.Zero, out var semanticChannelNames);
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
        }
    }
}
