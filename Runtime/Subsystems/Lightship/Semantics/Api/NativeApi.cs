using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.ARFoundation.Occlusion;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.XR.CoreUtils;
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

        public IntPtr Construct(IntPtr unityContext)
        {
            return Native.Construct(unityContext);
        }

        public void Start(IntPtr nativeProviderHandle)
        {
            Native.Start(nativeProviderHandle);
            // TryPrepareSubsystem() can only complete once the model has been downloaded, so it must be deferred.
        }

        public void Stop(IntPtr nativeProviderHandle)
        {
            Native.Stop(nativeProviderHandle);

            foreach (var bufferedTextureCache in _semanticsBufferedTextureCaches)
                bufferedTextureCache.Value.Dispose();

            _semanticsBufferedTextureCaches.Clear();
            _semanticsNameToShaderPropertyIdMap.Clear();
            _semanticsNameToIndexMap.Clear();
            _semanticsNameToShaderPropertyIdMap.Clear();
            _packedSemanticsPropertyNameID = default;

            if (null != _packedSemanticsBufferedTextureCache)
                _packedSemanticsBufferedTextureCache.Dispose();
        }

        /// <summary>
        /// If the semantic segmentation model is ready, prepare the subsystem's data structures.
        /// </summary>
        /// <param name="nativeProviderHandle">The handle to the semantics native API</param>
        /// <returns>
        /// <c>true</c> if the semantic segmentation model is ready and the subsystem has prepared its data structures. Otherwise,
        /// <c>false</c>.
        /// </returns>
        public bool TryPrepareSubsystem(IntPtr nativeProviderHandle)
        {
            if (!TryGetChannelNames(nativeProviderHandle, out var semanticChannelNames))
            {
                return false;
            }

            for (int i = 0; i < semanticChannelNames.Count; i++)
            {
                var semanticName = semanticChannelNames[i];
                _semanticsNameToIndexMap.Add(semanticName, i);

                CreateShaderPropertyEntry(semanticName);
                _semanticsBufferedTextureCaches[semanticName] = new BufferedTextureCache(_kFramesInMemory);
            }

            _packedSemanticsPropertyNameID = Shader.PropertyToID(_kTexturePackedSemanticChannelPropertyName);
            _packedSemanticsBufferedTextureCache = new BufferedTextureCache(_kFramesInMemory);
            return true;
        }

        /// <summary>
        /// Set the configuration for the currently-loaded Lightship semantic segmentation model
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API. </param>
        /// <param name="framesPerSecond"> The frame rate to run the model at in frames per second. </param>
        /// <param name="numThresholds"> The number of elements in thresholds. This is expected to be the same as the
        /// number of channels returned by <c>TryGetChannelNames</c>. </param>
        /// <param name="thresholds"> An array of float values. Each index in the array corresponds to an index in the
        /// array of semantic channels returned by <c>TryGetChannelNames</c>. A negative value will have no effect and will
        /// leave the threshold at the default or previously set value. A new threshold setting must be between 0 and
        /// 1, inclusive.</param>
        public void Configure(IntPtr nativeProviderHandle, UInt32 framesPerSecond, UInt32 numThresholds, IntPtr thresholds)
        {
            // TODO(rbarnes): expose semantics mode in Lightship settings UI
            Native.Configure(nativeProviderHandle, framesPerSecond, numThresholds, thresholds, 0);
        }

        public void Destruct(IntPtr nativeProviderHandle)
        {
            Native.Destruct(nativeProviderHandle);
        }

        /// <summary>
        /// Gets the texture descriptor for the named semantic channel.
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API </param>
        /// <param name="channelName"> The name of the semantic channel whose texture we want to get </param>
        /// <param name="cameraParams">Describes the viewport. If this is null, the samplerMatrix will result in an identity matrix.</param>
        /// <param name="semanticsChannelDescriptor"> The semantic channel texture descriptor to be populated, if available. </param>
        /// <param name="samplerMatrix">The matrix that converts from normalized viewport coordinates to normalized image coordinates.</param>
        /// <returns>
        /// <c>true</c> if the semantic channel texture descriptor is available and is returned. Otherwise,
        /// <c>false</c>.
        /// </returns>
        public bool TryGetSemanticChannel
        (
            IntPtr nativeProviderHandle,
            string channelName,
            XRCameraParams? cameraParams,
            out XRTextureDescriptor semanticsChannelDescriptor,
            out Matrix4x4 samplerMatrix
        )
        {
            semanticsChannelDescriptor = default;
            samplerMatrix = Matrix4x4.identity;

            if (!_semanticsNameToIndexMap.ContainsKey(channelName))
            {
                return false;
            }

            int semanticsIndex = _semanticsNameToIndexMap[channelName];
            var resourceHandle =
                Native.GetSemanticsChannel
                (
                    nativeProviderHandle,
                    semanticsIndex,
                    out IntPtr memoryBuffer,
                    out int size,
                    out int width,
                    out int height,
                    out TextureFormat format,
                    out uint frameId
                );

            if (resourceHandle == IntPtr.Zero)
            {
                return false;
            }

            if (memoryBuffer != IntPtr.Zero)
            {
                // Copy data to a texture, acquire its pointer
                var tex = _semanticsBufferedTextureCaches[channelName].GetUpdatedTextureFromBuffer
                (
                    memoryBuffer,
                    size,
                    width,
                    height,
                    format,
                    frameId
                );

                // Calculate the sampler matrix
                if (cameraParams.HasValue)
                {
                    var viewport = cameraParams.Value;
                    if (PoseProvider.TryAcquireCurrentPose(out var currentPose))
                    {
                        TryCalculateSamplerMatrix
                        (
                            nativeProviderHandle,
                            resourceHandle,
                            viewport,
                            currentPose,
                            OcclusionContext.Shared.OccludeeEyeDepth,
                            out samplerMatrix
                        );
                    }
                    else
                    {
                        samplerMatrix =
                            CameraMath.CalculateDisplayMatrix
                            (
                                width,
                                height,
                                (int)viewport.screenWidth,
                                (int)viewport.screenHeight,
                                viewport.screenOrientation,
                                invertVertically: true
                            );
                    }
                }

                // Package results
                semanticsChannelDescriptor = new XRTextureDescriptor
                (
                    tex.GetNativeTexturePtr(),
                    width,
                    height,
                    0,
                    format,
                    propertyNameId: _semanticsNameToShaderPropertyIdMap[channelName],
                    depth: 0,
                    dimension: TextureDimension.Tex2D
                );
            }

            Native.DisposeResource(nativeProviderHandle, resourceHandle);
            return true;
        }

        /// <summary>
        /// Get the CPU Image buffer of the named semantic channel.
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API </param>
        /// <param name="channelName"> The name of the semantic channel whose texture we want to get </param>
        /// <param name="cpuBuffer"> The semantic channel <see cref="LightshipCpuBuffer"/> to be populated, if available. </param>
        /// <returns>Whether acquiring the CPU image was successful.</returns>
        public bool TryAcquireSemanticChannelCPUImage(IntPtr nativeProviderHandle, string channelName, out LightshipCpuBuffer cpuBuffer)
        {
            if (!_semanticsNameToIndexMap.ContainsKey(channelName))
            {
                cpuBuffer = default;
                return false;
            }

            int index = _semanticsNameToIndexMap[channelName];
            var resHandle =
                Native.GetSemanticsChannel
                (
                    nativeProviderHandle,
                    index,
                    out var memoryBuffer,
                    out var size,
                    out var width,
                    out var height,
                    out _,
                    out _
                );

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
        /// <param name="cameraParams">Describes the viewport.</param>
        /// <param name="packedSemanticsDescriptor"> The packed semantics texture descriptor to be populated, if available. </param>
        /// <param name="samplerMatrix">A matrix that converts from viewport to texture coordinates.</param>
        /// <returns>
        /// <c>true</c> if the packed semantics texture descriptor is available and is returned. Otherwise,
        /// <c>false</c>.
        /// </returns>
        public bool TryGetPackedSemanticChannels
        (
            IntPtr nativeProviderHandle,
            XRCameraParams? cameraParams,
            out XRTextureDescriptor packedSemanticsDescriptor,
            out Matrix4x4 samplerMatrix
        )
        {
            packedSemanticsDescriptor = default;
            samplerMatrix = Matrix4x4.identity;

            var resourceHandle =
                Native.GetPackedSemanticsChannels
                (
                    nativeProviderHandle,
                    out var memoryBuffer,
                    out var size,
                    out var width,
                    out var height,
                    out var format,
                    out var frameId
                );

            if (resourceHandle == IntPtr.Zero)
                return false;

            if (memoryBuffer != IntPtr.Zero)
            {
                var tex = _packedSemanticsBufferedTextureCache.GetUpdatedTextureFromBuffer
                (
                    memoryBuffer,
                    size,
                    width,
                    height,
                    format,
                    frameId
                );

                // Calculate the sampler matrix
                if (cameraParams.HasValue)
                {
                    var viewport = cameraParams.Value;
                    if (PoseProvider.TryAcquireCurrentPose(out var currentPose))
                    {
                        TryCalculateSamplerMatrix
                        (
                            nativeProviderHandle,
                            resourceHandle,
                            viewport,
                            currentPose,
                            OcclusionContext.Shared.OccludeeEyeDepth,
                            out samplerMatrix
                        );
                    }
                    else
                    {
                        samplerMatrix =
                            CameraMath.CalculateDisplayMatrix
                            (
                                width,
                                height,
                                (int)viewport.screenWidth,
                                (int)viewport.screenHeight,
                                viewport.screenOrientation,
                                invertVertically: true
                            );
                    }
                }

                packedSemanticsDescriptor = new XRTextureDescriptor
                (
                    tex.GetNativeTexturePtr(),
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

        /// <summary>
        /// Get the CPU buffer for the packed semantic channels
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API </param>
        /// <param name="cpuBuffer"> The packed semantic channels <see cref="LightshipCpuBuffer"/> to be populated, if available. </param>
        /// <returns>Whether the CPU image was successfully acquired.</returns>
        public bool TryAcquirePackedSemanticChannelsCPUImage(IntPtr nativeProviderHandle, out LightshipCpuBuffer cpuBuffer)
        {
            var resHandle =
                Native.GetPackedSemanticsChannels
                (
                    nativeProviderHandle,
                    out var memoryBuffer,
                    out var size,
                    out var width,
                    out var height,
                    out var format,
                    out var frameId
                );

            if (resHandle == IntPtr.Zero)
            {
                cpuBuffer = default;
                return false;
            }

            var dimensions = new Vector2Int(width, height);
            cpuBuffer = new LightshipCpuBuffer(resHandle, memoryBuffer, dimensions, LightshipCpuBuffer.Format.BitMask32);
            return true;
        }


        /// <summary>
        /// Calculates a 3x3 transformation matrix that when applied to the image,
        /// aligns its pixels such that the image was taken from the specified pose.
        /// </summary>
        /// <param name="nativeProviderHandle">The handle to the semantics native API </param>
        /// <param name="resourceHandle">The handle to the semantics buffer resource.</param>
        /// <param name="cameraParams">Describes the viewport.</param>
        /// <param name="pose">The camera pose the image needs to align with.</param>
        /// <param name="backProjectionPlane">The distance from the camera to the plane that
        /// the image should be projected onto (in meters).</param>
        /// <param name="result"></param>
        /// <returns>True, if the matrix could be calculated, otherwise false (in case the </returns>
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
            var outMatrix = new float[9];
            var poseArray = MatrixConversionHelper.Matrix4x4ToInternalArray(pose.FromUnityToArdk());

            var gotInterpolationMatrix =
                Native.CalculateInterpolationMatrix
                (
                    nativeProviderHandle,
                    resourceHandle,
                    (int)cameraParams.screenWidth,
                    (int)cameraParams.screenHeight,
                    cameraParams.screenOrientation.FromUnityToArdk(),
                    poseArray,
                    backProjectionPlane,
                    outMatrix
                );

            if (gotInterpolationMatrix)
            {
                result = new Matrix4x4
                (
                    new Vector4(outMatrix[0], outMatrix[1], outMatrix[2], 0),
                    new Vector4(outMatrix[3], outMatrix[4], outMatrix[5], 0),
                    new Vector4(outMatrix[6], outMatrix[7], outMatrix[8], 0),
                    new Vector4(0, 0, 0, 1)
                );

                return true;
            }

            result = Matrix4x4.identity;
            return false;
        }

        public bool TryGetChannelNames(IntPtr nativeProviderHandle, out List<string> semanticChannelNames)
        {
            var resourceHandle = Native.GetChannelNames(nativeProviderHandle, out var arrayPointer, out var arrayLength);
            if (IntPtr.Zero == resourceHandle || IntPtr.Zero == arrayPointer)
            {
                semanticChannelNames = default;
                return false;
            }

            semanticChannelNames = new(arrayLength);

            unsafe
            {
                var nativeSemanticsArray =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<NativeStringStruct>
                    (
                        arrayPointer.ToPointer(),
                        arrayLength,
                        Allocator.None
                    );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeSemanticsArray, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                foreach (var nameStruct in nativeSemanticsArray)
                {
                    if (IntPtr.Zero == nameStruct.CharArrayIntPtr || nameStruct.ArrayLength <= 0)
                    {
                        Debug.LogError("Received invalid channel name data from semantics native layer");
                        semanticChannelNames = default;
                        return false;
                    }

                    string name = Marshal.PtrToStringAnsi(nameStruct.CharArrayIntPtr, (int) nameStruct.ArrayLength);
                    semanticChannelNames.Add(name);
                }

                nativeSemanticsArray.Dispose();
            }

            Native.DisposeResource(nativeProviderHandle, resourceHandle);
            return true;
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_Construct")]
            public static extern IntPtr Construct(IntPtr unityContext);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_Start")]
            public static extern void Start(IntPtr nativeProviderHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_Stop")]
            public static extern void Stop(IntPtr nativeProviderHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_Configure")]
            public static extern void Configure(IntPtr nativeProviderHandle, UInt32 framesPerSecond, UInt32 numThresholds, IntPtr thresholds, byte mode);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_Destruct")]
            public static extern void Destruct(IntPtr nativeProviderHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_GetSemanticsChannel")]
            public static extern IntPtr GetSemanticsChannel
            (
                IntPtr nativeProviderHandle,
                int channelId,
                out IntPtr memoryBuffer,
                out int size,
                out int width,
                out int height,
                out TextureFormat format,
                out uint frameId
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_GetPackedSemanticsChannel")]
            public static extern IntPtr GetPackedSemanticsChannels
            (
                IntPtr nativeProviderHandle,
                out IntPtr memoryBuffer,
                out int size,
                out int width,
                out int height,
                out TextureFormat format,
                out uint frameId
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_CalculateSamplerMatrix")]
            public static extern bool CalculateInterpolationMatrix
            (
                IntPtr nativeProviderHandle,
                IntPtr nativeResourceHandle,
                int viewportWidth,
                int viewportHeight,
                uint orientation,
                float[] poseMatrix,
                float backProjectionPlane,
                float[] outMatrix3X3
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_ReleaseResource")]
            public static extern IntPtr DisposeResource(IntPtr nativeProviderHandle, IntPtr resourceHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_GetChannelNames")]
            public static extern IntPtr GetChannelNames(IntPtr nativeProviderHandle, out IntPtr channelStructs, out int numChannels);
        }
    }
}
