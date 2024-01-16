// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Utilities.Log;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Occlusion;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Textures;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace  Niantic.Lightship.AR.Subsystems.Semantics
{
    internal class NativeApi : IApi
    {
        private const int FramesInMemory = 2;
        private const string TextureSemanticChannelPropertyPrefix = "_SemanticChannel_";
        private const string TexturePackedSemanticChannelPropertyName = "_PackedSemanticChannels";

        private readonly Dictionary<string, int> _semanticsNameToIndexMap = new ();
        private readonly Dictionary<string, int> _semanticsNameToShaderPropertyIdMap = new ();
        private readonly Dictionary<string, BufferedTextureCache> _semanticsBufferedTextureCaches = new ();

        private readonly int _packedSemanticsPropertyNameID =  Shader.PropertyToID(TexturePackedSemanticChannelPropertyName);
        private BufferedTextureCache _packedSemanticsBufferedTextureCache = new BufferedTextureCache(FramesInMemory);
        private uint _latestTimestampMs;

        private bool _initializedMetadataDependencies = false;

        /// <summary>
        /// The CPU image API for interacting with the environment depth image.
        /// </summary>
        private LightshipCpuImageApi _cpuImageApi => LightshipCpuImageApi.instance;

        public IntPtr Construct(IntPtr unityContext)
        {
            return Native.Construct(unityContext);
        }

        public void Start(IntPtr nativeProviderHandle)
        {
            Native.Start(nativeProviderHandle);
        }

        public void Stop(IntPtr nativeProviderHandle)
        {
            Native.Stop(nativeProviderHandle);

            ResetMetadataDependencies();
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

            if (!_initializedMetadataDependencies && HasMetadata(nativeProviderHandle))
            {
                SetMetadataDependencies(nativeProviderHandle);
            }

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
                    out uint frameId,
                    out _latestTimestampMs
                );

            if (resourceHandle == IntPtr.Zero || memoryBuffer == IntPtr.Zero)
                return false;

            var texture =
                _semanticsBufferedTextureCaches[channelName].GetUpdatedTextureFromBuffer
                (
                    memoryBuffer,
                    size,
                    width,
                    height,
                    format,
                    frameId
                );

            samplerMatrix = GetSamplerMatrix(nativeProviderHandle, resourceHandle, cameraParams, width, height);

            Native.DisposeResource(nativeProviderHandle, resourceHandle);

            // Package results
            semanticsChannelDescriptor = new XRTextureDescriptor
            (
                texture.GetNativeTexturePtr(),
                width,
                height,
                0,
                format,
                propertyNameId: _semanticsNameToShaderPropertyIdMap[channelName],
                depth: 0,
                dimension: TextureDimension.Tex2D
            );

            return true;
        }

        /// <summary>
        /// Get the XRCpuImage of the named semantic channel.
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API </param>
        /// <param name="channelName"> The name of the semantic channel whose texture we want to get </param>
        /// <param name="cameraParams">Describes the viewport.</param>
        /// <param name="cpuImage">If this method returns `true`, an acquired <see cref="XRCpuImage"/>. The XRCpuImage
        /// must be disposed by the caller.</param>
        /// <param name="samplerMatrix">A matrix that converts from viewport to texture coordinates.</param>
        /// <returns>Whether acquiring the XRCpuImage was successful.</returns>
        public bool TryAcquireSemanticChannelCpuImage
        (
            IntPtr nativeProviderHandle,
            string channelName,
            XRCameraParams? cameraParams,
            out XRCpuImage cpuImage,
            out Matrix4x4 samplerMatrix
        )
        {
            cpuImage = default;
            samplerMatrix = Matrix4x4.identity;

            if (!_initializedMetadataDependencies && HasMetadata(nativeProviderHandle))
            {
                SetMetadataDependencies(nativeProviderHandle);
            }

            if (!_semanticsNameToIndexMap.ContainsKey(channelName))
            {
                return false;
            }

            int index = _semanticsNameToIndexMap[channelName];
            var resourceHandle =
                Native.GetSemanticsChannel
                (
                    nativeProviderHandle,
                    index,
                    out var memoryBuffer,
                    out var size,
                    out var width,
                    out var height,
                    out var format,
                    out var frameId,
                    out _latestTimestampMs
                );

            if (resourceHandle == IntPtr.Zero || memoryBuffer == IntPtr.Zero)
                return false;

            var gotCpuImage =
                _cpuImageApi.TryAddManagedXRCpuImage
                (
                    memoryBuffer,
                    size,
                    width,
                    height,
                    format,
                    _latestTimestampMs,
                    out var cinfo
                );

            samplerMatrix = GetSamplerMatrix(nativeProviderHandle, resourceHandle, cameraParams, width, height);

            Native.DisposeResource(nativeProviderHandle, resourceHandle);

            if (gotCpuImage)
            {
                cpuImage = new XRCpuImage(_cpuImageApi, cinfo);
                return true;
            }

            return false;
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
                    out var frameId,
                    out _latestTimestampMs
                );

            if (resourceHandle == IntPtr.Zero || memoryBuffer == IntPtr.Zero)
                return false;

            var texture = _packedSemanticsBufferedTextureCache.GetUpdatedTextureFromBuffer
            (
                memoryBuffer,
                size,
                width,
                height,
                format,
                frameId
            );

            samplerMatrix = GetSamplerMatrix(nativeProviderHandle, resourceHandle, cameraParams, width, height);

            Native.DisposeResource(nativeProviderHandle, resourceHandle);

            packedSemanticsDescriptor = new XRTextureDescriptor
            (
                texture.GetNativeTexturePtr(),
                width,
                height,
                0,
                format,
                _packedSemanticsPropertyNameID,
                0,
                TextureDimension.Tex2D
            );

            return true;
        }

        /// <summary>
        /// Get the XRCpuImage for the packed semantic channels
        /// </summary>
        /// <param name="nativeProviderHandle"> The handle to the semantics native API </param>
        /// <param name="cameraParams">Describes the viewport.</param>
        /// <param name="cpuImage">If this method returns `true`, an acquired <see cref="XRCpuImage"/>. The XRCpuImage
        /// must be disposed by the caller.</param>
        /// <param name="samplerMatrix">A matrix that converts from viewport to texture coordinates.</param>
        /// <returns>Whether the XRCpuImage was successfully acquired.</returns>
        public bool TryAcquirePackedSemanticChannelsCpuImage
        (
            IntPtr nativeProviderHandle,
            XRCameraParams? cameraParams,
            out XRCpuImage cpuImage,
            out Matrix4x4 samplerMatrix
        )
        {
            cpuImage = default;
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
                    out var frameId,
                    out _latestTimestampMs
                );

            if (resourceHandle == IntPtr.Zero || memoryBuffer == IntPtr.Zero)
                return false;

            var gotCpuImage =
                _cpuImageApi.TryAddManagedXRCpuImage
                (
                    memoryBuffer,
                    size,
                    width,
                    height,
                    format,
                    _latestTimestampMs,
                    out var cinfo
                );

            samplerMatrix = GetSamplerMatrix(nativeProviderHandle, resourceHandle, cameraParams, width, height);

            Native.DisposeResource(nativeProviderHandle, resourceHandle);

            if (gotCpuImage)
            {
                cpuImage = new XRCpuImage(_cpuImageApi, cinfo);
                return true;
            }

            return true;
        }

        private List<string> _cachedSemanticChannelNames = new();

        public bool TryGetChannelNames(IntPtr nativeProviderHandle, out List<string> semanticChannelNames)
        {
            if (_initializedMetadataDependencies)
            {
                semanticChannelNames = new List<string>(_cachedSemanticChannelNames);
                return true;
            }

            var resourceHandle = Native.GetChannelNames(nativeProviderHandle, out var arrayPointer, out var arrayLength);
            if (IntPtr.Zero == resourceHandle || IntPtr.Zero == arrayPointer)
            {
                semanticChannelNames = new List<string>();
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
                        Log.Error("Received invalid channel name data from semantics native layer");
                        semanticChannelNames = default;
                        return false;
                    }

                    string name = Marshal.PtrToStringAnsi(nameStruct.CharArrayIntPtr, (int) nameStruct.ArrayLength);
                    semanticChannelNames.Add(name);
                }

                nativeSemanticsArray.Dispose();
            }

            SetMetadataDependencies(semanticChannelNames);

            Native.DisposeResource(nativeProviderHandle, resourceHandle);

            return true;
        }

        public bool TryGetLatestFrameId(IntPtr nativeProviderHandle, out uint frameId)
        {
            return Native.TryGetLatestFrameId(nativeProviderHandle, out frameId);
        }

        public bool TryGetLatestIntrinsicsMatrix(IntPtr nativeProviderHandle, out Matrix4x4 intrinsicsMatrix)
        {
            float[] intrinsics = new float[9];
            bool gotIntrinsics = Native.TryGetLatestIntrinsics(nativeProviderHandle, intrinsics);

            if (!gotIntrinsics)
            {
                intrinsicsMatrix = default;
                return false;
            }

            intrinsicsMatrix = new Matrix4x4
            (
                new Vector4(intrinsics[0], intrinsics[1], intrinsics[2], 0),
                new Vector4(intrinsics[3], intrinsics[4], intrinsics[5], 0),
                new Vector4(intrinsics[6], intrinsics[7], intrinsics[8], 0),
                new Vector4(0, 0, 0, 1)
            );
            return true;
        }

        public bool HasMetadata(IntPtr nativeProviderHandle)
        {
            return Native.HasMetadata(nativeProviderHandle);
        }

        private void SetMetadataDependencies(IntPtr nativeProviderHandle)
        {
            if (_initializedMetadataDependencies)
            {
                Log.Warning("Failed to set metadata dependencies because they are already set.");
                return;
            }

            if (TryGetChannelNames(nativeProviderHandle, out var semanticChannelNames))
            {
                SetMetadataDependencies(semanticChannelNames);
            }
        }

        private void SetMetadataDependencies(List<string> semanticChannelNames)
        {
            if (_initializedMetadataDependencies)
            {
                Log.Warning("Failed to set metadata dependencies because they are already set.");
                return;
            }

            _cachedSemanticChannelNames = semanticChannelNames;

            _semanticsNameToIndexMap.Clear();
            for (int i = 0; i < semanticChannelNames.Count; i++)
            {
                var channelName = semanticChannelNames[i];
                _semanticsNameToIndexMap.Add(channelName, i);

                _semanticsNameToShaderPropertyIdMap[channelName] =
                    Shader.PropertyToID(TextureSemanticChannelPropertyPrefix + channelName);

                _semanticsBufferedTextureCaches[channelName] =
                    new BufferedTextureCache(FramesInMemory);
            }

            _initializedMetadataDependencies = true;
        }

        private void ResetMetadataDependencies()
        {
            _initializedMetadataDependencies = false;

            _packedSemanticsBufferedTextureCache?.Dispose();

            foreach (var bufferedTextureCache in _semanticsBufferedTextureCaches.Values)
                bufferedTextureCache.Dispose();


            _semanticsBufferedTextureCaches.Clear();
            _semanticsNameToShaderPropertyIdMap.Clear();
            _semanticsNameToIndexMap.Clear();
            _semanticsNameToShaderPropertyIdMap.Clear();
        }

        private Matrix4x4 GetSamplerMatrix
        (
            IntPtr nativeProviderHandle,
            IntPtr resourceHandle,
            XRCameraParams? cameraParams,
            int imageWidth,
            int imageHeight
        )
        {
            var samplerMatrix = Matrix4x4.identity;

            // Calculate the sampler matrix
            if (!cameraParams.HasValue)
            {
                return samplerMatrix;
            }

            var viewport = cameraParams.Value;

            if (PoseProvider.TryAcquireCurrentPose(out var currentPose))
            {
                TryCalculateSamplerMatrixWithInterpolationMatrix
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
                        imageWidth,
                        imageHeight,
                        (int)viewport.screenWidth,
                        (int)viewport.screenHeight,
                        viewport.screenOrientation,
                        invertVertically: true
                    );
            }

            return samplerMatrix;
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
        private bool TryCalculateSamplerMatrixWithInterpolationMatrix
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

            Log.Warning("Interpolation matrix for semantic prediction could not be calculated.");
            result = Matrix4x4.identity;
            return false;
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
                out uint frameId,
                out uint timestamp
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
                out uint frameId,
                out uint timestamp
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

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_TryGetLatestFrameId")]
            public static extern bool TryGetLatestFrameId(IntPtr nativeProviderHandle, out uint frameId);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_TryGetLatestIntrinsics")]
            public static extern bool TryGetLatestIntrinsics(IntPtr nativeProviderHandle, float[] intrinsics);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_SemanticsProvider_HasMetadata")]
            public static extern bool HasMetadata(IntPtr nativeProviderHandle);
        }
    }
}
