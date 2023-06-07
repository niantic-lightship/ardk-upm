using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.SemanticsSubsystem
{
    internal class NativeApi : IApi
    {
        private readonly Dictionary<string, int> _semanticsNameToIndexMap = new ();
        private readonly Dictionary<string, int> _semanticsNameToShaderPropertyIdMap = new ();
        private readonly Dictionary<string, BufferedTextureCache> _semanticsBufferedTextureCaches = new ();
        private const string _kTextureSemanticChannelPropertyPrefix = "_SemanticChannel_";
        private const string _kTexturePackedSemanticChannelPropertyName = "_PackedSemanticChannels";
        private BufferedTextureCache _packedSemanticsBufferedTextureCache;
        private int _packedSemanticsPropertyNameID;
        private const int _kFramesInMemory = 2;


        private void CreateShaderPropertyEntry(string semanticChannelName)
        {
            _semanticsNameToShaderPropertyIdMap[semanticChannelName] = Shader.PropertyToID(_kTextureSemanticChannelPropertyPrefix+semanticChannelName);
        }
        private void InitializeHardcodedMaps()
        {
            //TODO fill this in with semantic mapping retrieved from (yet non-existent) API
            _semanticsNameToIndexMap["sky"] = 0;
            _semanticsNameToIndexMap["ground"] = 1;
            _semanticsNameToIndexMap["natural_ground"] = 2;
            _semanticsNameToIndexMap["artificial_ground"] = 3;
            _semanticsNameToIndexMap["water"] = 4;
            _semanticsNameToIndexMap["person"] = 5;
            _semanticsNameToIndexMap["building"] = 6;
            _semanticsNameToIndexMap["foliage"] = 7;
            _semanticsNameToIndexMap["grass"] = 8;

            //experimental
            _semanticsNameToIndexMap["flower_experimental"] = 9;
            _semanticsNameToIndexMap["tree_trunk_experimental"] = 10;
            _semanticsNameToIndexMap["pet_experimental"] = 11;
            _semanticsNameToIndexMap["sand_experimental"] = 12;
            _semanticsNameToIndexMap["tv_experimental"] = 13;
            _semanticsNameToIndexMap["dirt_experimental"] = 14;
            _semanticsNameToIndexMap["vehicle_experimental"] = 15;
            _semanticsNameToIndexMap["food_experimental"] = 16;
            _semanticsNameToIndexMap["loungeable_experimental"] = 17;
            _semanticsNameToIndexMap["snow_experimental"] = 18;

            foreach (var semanticName in _semanticsNameToIndexMap.Keys)
            {
                CreateShaderPropertyEntry(semanticName);
                _semanticsBufferedTextureCaches[semanticName] = new BufferedTextureCache(_kFramesInMemory);
            }

            _packedSemanticsPropertyNameID = Shader.PropertyToID(_kTexturePackedSemanticChannelPropertyName);
            _packedSemanticsBufferedTextureCache = new BufferedTextureCache(_kFramesInMemory);
        }

        public IntPtr Construct(IntPtr unityContext)
        {
            InitializeHardcodedMaps();
            return Native.Construct(unityContext);
        }

        public void Start(IntPtr nativeProviderHandle)
        {
            Native.Start(nativeProviderHandle);
        }

        public void Stop(IntPtr nativeProviderHandle)
        {
            Native.Stop(nativeProviderHandle);
        }

        /// <summary>
        /// Set the configuration for the currently-loaded Lightship semantic segmentation model
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API. </param>
        /// <param name="framesPerSecond"> The frame rate to run the model at in frames per second. </param>
        /// <param name="numThresholds"> The number of elements in thresholds. This is expected to be the same as the
        /// number of channels returned by <c>GetChannelNames</c>. </param>
        /// <param name="thresholds"> An array of float values. Each index in the array corresponds to an index in the
        /// array of semantic channels returned by <c>GetChannelNames</c>. A negative value will have no effect and will
        /// leave the threshold at the default or previously set value. A new threshold setting must be between 0 and
        /// 1, inclusive.</param>
        public void Configure(IntPtr nativeProviderHandle, UInt32 framesPerSecond, UInt32 numThresholds, IntPtr thresholds)
        {
            Native.Configure(nativeProviderHandle, framesPerSecond, numThresholds, thresholds);
        }

        public void Destruct(IntPtr nativeProviderHandle)
        {
            _semanticsNameToIndexMap.Clear();
            _semanticsNameToShaderPropertyIdMap.Clear();
            Native.Destruct(nativeProviderHandle);
        }

        /// <summary>
        /// Get the texture descriptor for the named semantic channel
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API </param>
        /// <param name="channelName"> The name of the semantic channel whose texture we want to get </param>
        /// <param name="semanticChannelDescriptor"> The semantic channel texture descriptor to be populated, if available. </param>
        /// <returns>
        /// <c>true</c> if the semantic channel texture descriptor is available and is returned. Otherwise,
        /// <c>false</c>.
        /// </returns>
        public bool TryGetSemanticChannel(IntPtr nativeProviderHandle, string channelName, out XRTextureDescriptor semanticsChannelDescriptor)
        {
            semanticsChannelDescriptor = default;
            int semanticsIndex = _semanticsNameToIndexMap[channelName];
            var resourceHandle = Native.GetSemanticsChannel(nativeProviderHandle, semanticsIndex, out IntPtr memoryBuffer, out int size, out int width, out int height, out TextureFormat format, out uint frameId);
            if (resourceHandle != IntPtr.Zero)
            {
                if (memoryBuffer != IntPtr.Zero)
                {
                    _semanticsBufferedTextureCaches[channelName].GetUpdatedTextureFromBuffer
                    (
                        memoryBuffer,
                        size,
                        width,
                        height,
                        format,
                        frameId,
                        out IntPtr nativeTexturePtr
                    );

                    int propertyNameID = _semanticsNameToShaderPropertyIdMap[channelName];
                    semanticsChannelDescriptor =
                        new XRTextureDescriptor
                        (
                            nativeTexturePtr,
                            width,
                            height,
                            0,
                            format,
                            propertyNameID,
                            0,
                            TextureDimension.Tex2D
                        );
                }

                Native.DisposeResource(nativeProviderHandle, resourceHandle);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the CPU Image buffer of the named semantic channel
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API </param>
        /// <param name="channelName"> The name of the semantic channel whose texture we want to get </param>
        /// <param name="cpuBuffer"> The semantic channel <see cref="LightshipCpuBuffer"/> to be populated, if available. </param>
        /// <returns></returns>
        public bool TryAcquireSemanticChannelCPUImage(IntPtr nativeProviderHandle, string channelName, out LightshipCpuBuffer cpuBuffer)
        {
            //create index from channelName
            int index = _semanticsNameToIndexMap[channelName];
            var resHandle = Native.GetSemanticsChannel(nativeProviderHandle, index, out var memoryBuffer, out var size, out var width,
                out var height, out TextureFormat format, out var frameId);
            if (resHandle == IntPtr.Zero)
            {
                cpuBuffer = default;
                return false;
            }
            var dimensions = new Vector2Int(width, height);
            cpuBuffer = new LightshipCpuBuffer(resHandle, memoryBuffer, dimensions, LightshipCpuBuffer.Format.DepthFloat32);
            return true;
        }

        /// <summary>
        /// Dispose of the CPU Image obtained from the CPUImage methods
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API </param>
        /// <param name="resourceHandle">  The handle to the CPU Image resource </param>
        public void DisposeCPUImage(IntPtr nativeProviderHandle,  IntPtr resourceHandle)
        {
            Native.DisposeResource(nativeProviderHandle, resourceHandle);
        }


        /// <summary>
        /// Get the texture descriptor for the packed semantics
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API </param>
        /// <param name="packedSemanticsDescriptor"> The packed semantics texture descriptor to be populated, if available. </param>
        /// <returns>
        /// <c>true</c> if the packed semantics texture descriptor is available and is returned. Otherwise,
        /// <c>false</c>.
        /// </returns>
        public bool TryGetPackedSemanticChannels(IntPtr nativeProviderHandle, out XRTextureDescriptor packedSemanticsDescriptor)
        {
            packedSemanticsDescriptor = default;
            var resourceHandle = Native.GetPackedSemanticsChannels(nativeProviderHandle, out var memoryBuffer, out var size, out var width,
                out var height, out var format, out var frameId);

            if (resourceHandle != IntPtr.Zero)
            {
                if (memoryBuffer != IntPtr.Zero)
                {
                    _packedSemanticsBufferedTextureCache.GetUpdatedTextureFromBuffer
                    (
                        memoryBuffer,
                        size,
                        width,
                        height,
                        format,
                        frameId,
                        out IntPtr nativeTexturePtr
                    );

                    packedSemanticsDescriptor =
                        new XRTextureDescriptor
                        (
                            nativeTexturePtr,
                            width,
                            height,
                            0,
                            format,
                            _packedSemanticsPropertyNameID,
                            0,
                            TextureDimension.Tex2D
                        );
                }

                Native.DisposeResource(nativeProviderHandle, resourceHandle);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the CPU buffer for the packed semantic channels
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API </param>
        /// <param name="cpuBuffer"> The packed semantic channels <see cref="LightshipCpuBuffer"/> to be populated, if available. </param>
        /// <returns></returns>
        public bool TryAcquirePackedSemanticChannelsCPUImage(IntPtr nativeProviderHandle, out LightshipCpuBuffer cpuBuffer)
        {
            var resHandle = Native.GetPackedSemanticsChannels(nativeProviderHandle, out var memoryBuffer, out var size, out var width,
                out var height, out var format, out var frameId);
            if (resHandle == IntPtr.Zero)
            {
                cpuBuffer = default;
                return false;
            }
            var dimensions = new Vector2Int(width, height);
            cpuBuffer = new LightshipCpuBuffer(resHandle, memoryBuffer, dimensions, LightshipCpuBuffer.Format.BitMask32);
            return true;
        }

        public bool GetChannelNames(IntPtr nativeProviderHandle, out List<string> semanticChannelNames)
        {
            //TODO(rbarnes) Remove hard-coding after native implementation is present
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
            //TODO(rbarnes) the final code is below

            var resourceHandle = Native.GetChannelNames(nativeProviderHandle, out var arrayPointer, out var arrayLength);
            if (IntPtr.Zero == resourceHandle || IntPtr.Zero == arrayPointer)
            {
                semanticChannelNames = default;
                return false;
            }

            semanticChannelNames = new(arrayLength);

            unsafe
            {
                NativeArray<NativeStringStruct> nativeSemanticsArray =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<NativeStringStruct>(arrayPointer.ToPointer(),
                        arrayLength, Allocator.None);
                foreach (var nameStruct in nativeSemanticsArray)
                {
                    if (IntPtr.Zero == nameStruct.CharArray || nameStruct.ArrayLength <= 0)
                    {
                        Debug.LogError("Received invalid channel name data from semantics native layer");
                        semanticChannelNames = default;
                        return false;
                    }

                    string name = Marshal.PtrToStringAnsi(nameStruct.CharArray, (int) nameStruct.ArrayLength);
                    semanticChannelNames.Add(name);
                }

                nativeSemanticsArray.Dispose();
            }

            Native.DisposeResource(nativeProviderHandle, resourceHandle);
            return true;
        }

        private static class Native
        {
            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_Construct")]
            public static extern IntPtr Construct(IntPtr unityContext);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_Start")]
            public static extern void Start(IntPtr nativeProviderHandle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_Stop")]
            public static extern void Stop(IntPtr nativeProviderHandle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_Configure")]
            public static extern void Configure(IntPtr nativeProviderHandle, UInt32 framesPerSecond, UInt32 numThresholds, IntPtr thresholds);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_Destruct")]
            public static extern void Destruct(IntPtr nativeProviderHandle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_GetSemanticsChannel")]
            public static extern IntPtr GetSemanticsChannel(IntPtr nativeProviderHandle, int channelId, out IntPtr memoryBuffer, out int size, out int width, out int height, out TextureFormat format, out uint frameId);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_GetPackedSemanticsChannel")]
            public static extern IntPtr GetPackedSemanticsChannels(IntPtr nativeProviderHandle, out IntPtr memoryBuffer, out int size, out int width, out int height, out TextureFormat format, out uint frameId);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_ReleaseResource")]
            public static extern IntPtr DisposeResource(IntPtr nativeProviderHandle, IntPtr resourceHandle);

            [DllImport(_LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_GetChannelNames")]
            public static extern IntPtr GetChannelNames(IntPtr nativeProviderHandle, out IntPtr channelStructs, out int numChannels);
        }
    }
}
