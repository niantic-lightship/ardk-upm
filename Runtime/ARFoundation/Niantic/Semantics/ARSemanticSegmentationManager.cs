// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.ARFoundation
{
    /// <summary>
    /// The <c>ARSemanticSegmentationManager</c> controls the <c>XRSemanticsSubsystem</c> and updates the semantics
    /// textures on each Update loop. Textures and CPU buffers are available for confidence maps of individual semantic
    /// segmentation channels and a bit array indicating which semantic channels have surpassed the chosen confidence
    /// threshold per pixel. For cases where a semantic segmentation texture is overlaid on the screen, utilities are
    /// provided to read semantic properties at a given point on the screen.
    /// </summary>
    [PublicAPI]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ARUpdateOrder.k_OcclusionManager)]
    public class ARSemanticSegmentationManager :
        SubsystemLifecycleManager<XRSemanticsSubsystem, XRSemanticsSubsystemDescriptor, XRSemanticsSubsystem.Provider>
    {
        /// <summary>
        /// A dictionary mapping semantic confidence textures (<c>ARTextureInfo</c>s) to their respective semantic
        /// segmentation channel names.
        /// </summary>
        /// <value>
        /// The semantic segmentation confidence texture infos.
        /// </value>
        private Dictionary<string, ARTextureInfo> _semanticChannelTextureInfos = new();

        /// <summary>
        /// The semantic segmentation packed thresholded bitmask.
        /// </summary>
        /// <value>
        /// The semantic segmentation packed thresholded bitmask.
        /// </value>
        private ARTextureInfo _packedBitmaskTextureInfo;

        private List<string> _semanticChannelNames = new List<string>();

        /// <summary>
        /// The names of the semantic channels that the current model is able to detect.
        /// </summary>
        public List<string> SemanticChannelNames
        {
            get { return _semanticChannelNames; }
        }

        /// <summary>
        /// Callback before the subsystem is started (but after it is created).
        /// </summary>
        protected override void OnBeforeStart()
        {
            ResetTextureInfos();
            _semanticChannelNames = subsystem.GetChannelNames();
        }

        /// <summary>
        /// Callback when the manager is being disabled.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            ResetTextureInfos();
        }

        /// <summary>
        /// Callback as the manager is being updated.
        /// </summary>
        public void Update()
        {
            if (subsystem != null && subsystem.running)
            {
                UpdateTexturesInfos();
            }
        }

        /// <summary>
        /// Clears the references to the packed and confidence semantic segmentation textures
        /// </summary>
        private void ResetTextureInfos()
        {
            _packedBitmaskTextureInfo.Reset();

            foreach (KeyValuePair<string, ARTextureInfo> pair in _semanticChannelTextureInfos)
                pair.Value.Dispose();

            _semanticChannelTextureInfos.Clear();
        }

        /// <summary>
        /// Read a semantic segmentation texture for the specified semantic class.
        /// </summary>
        /// <param name="channelName">The semantic channel to acquire.</param>
        /// <param name="samplerMatrix">A matrix to transform from viewport space to semantic texture space, assuming
        /// that the device's screen is the viewport.</param>
        /// <value>
        /// The texture for the specified semantic channel, if any. Otherwise, <c>null</c>.
        /// </value>
        public Texture2D GetSemanticChannelTexture(string channelName, out Matrix4x4 samplerMatrix)
        {
            samplerMatrix = Matrix4x4.identity;

            if (descriptor?.semanticSegmentationImageSupported == Supported.Supported
                && _semanticChannelTextureInfos.ContainsKey(channelName))
            {
                var semanticConfidenceTextureInfo = _semanticChannelTextureInfos[channelName];
                if (semanticConfidenceTextureInfo.Descriptor.dimension != TextureDimension.Tex2D
                    && semanticConfidenceTextureInfo.Descriptor.dimension != TextureDimension.None)
                {
                    Debug.Log("Semantic confidence texture needs to be a Texture2D, but instead is "
                        + $"{semanticConfidenceTextureInfo.Descriptor.dimension.ToString()}.");
                    return null;
                }

                samplerMatrix = semanticConfidenceTextureInfo.SamplerMatrix;

                return semanticConfidenceTextureInfo.Texture as Texture2D;
            }
            return null;
        }

        /// <summary>
        /// Attempt to acquire the latest semantic segmentation CPU buffer for the specified semantic class. This
        /// provides direct access to the raw pixel data.
        /// </summary>
        /// <remarks>
        /// The <c>LightshipCpuBuffer</c> must be disposed to avoid resource leaks.
        /// </remarks>
        /// <param name="channel">The semantic channel to acquire.</param>
        /// <param name="cpuBuffer">If this method returns `true`, an acquired <c>LightshipCpuBuffer</c>.</param>
        /// <returns>Returns `true` if the CPU buffer was acquired. Returns `false` otherwise.</returns>
        public bool TryAcquireSemanticChannelCPUBuffer(string channel, out LightshipCpuBuffer cpuBuffer)
        {
            return subsystem.TryAcquireSemanticChannelCPUImage(channel, out cpuBuffer);
        }

        /// <summary>
        /// Dispose a semantic segmentation CPU buffer.
        /// </summary>
        /// <remarks>
        /// The <c>LightshipCpuBuffer</c> must have been successfully acquired with
        /// <c>TryAcquireSemanticChannelCPUBuffer</c>.
        /// </remarks>
        /// <param name="cpuBuffer">The <c>LightshipCpuBuffer</c> to dispose.</param>
        public void DisposeSemanticChannelCPUBuffer(LightshipCpuBuffer cpuBuffer)
        {
            subsystem.DisposeCPUImage(cpuBuffer);
        }

        /// <summary>
        /// Tries to acquire the latest packed semantic channels CPU image. Each element of the buffer is a bit field
        /// indicating which semantic channels have surpassed their respective detection confidence thresholds for that
        /// pixel. (See <c>GetChannelIndex</c>)
        /// </summary>
        /// <remarks>The utility <c>GetChannelNamesAt</c> can be used for reading semantic channel names at a viewport
        /// location.</remarks>
        /// <param name="cpuBuffer">If this method returns `true`, an acquired <see cref="LightshipCpuBuffer"/>. The CPU buffer
        /// must be disposed by the caller.</param>
        /// <returns>True if the CPU image is acquired. Otherwise, false</returns>
        public bool TryAcquirePackedSemanticChannelsCPUBuffer(out LightshipCpuBuffer cpuBuffer)
        {
            return subsystem.TryAcquirePackedSemanticChannelsCPUImage(out cpuBuffer);
        }

        /// <summary>
        /// Dispose a semantic segmentation CPU buffer.
        /// </summary>
        /// <remarks>
        /// The <c>LightshipCpuBuffer</c> must have been successfully acquired with <c>TryAcquireSemanticChannelCPUBuffer</c>.
        /// </remarks>
        /// <param name="cpuBuffer">The <c>LightshipCpuBuffer</c> to dispose.</param>
        public void DisposePackedSemanticChannelsCPUBuffer(LightshipCpuBuffer cpuBuffer)
        {
            subsystem.DisposeCPUImage(cpuBuffer);
        }

        /// <summary>
        /// Get the channel index of a specified semantic class. This corresponds to a bit position in the packed
        /// semantics buffer, with index 0 being the most-significant bit.
        /// </summary>
        /// <param name="channelName">The name of the semantic class.</param>
        /// <returns>The index of the specified semantic class, or -1 if the channel does not exist.</returns>
        public int GetChannelIndex(string channelName)
        {
            for (int idx = 0; idx < _semanticChannelNames.Count; idx++)
            {
                if (_semanticChannelNames[idx] == channelName)
                {
                    return idx;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the semantics at the specified pixel in a viewport.
        /// </summary>
        /// <remarks>It is assumed that the texture fills the screen, so the viewport is the screen.</remarks>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <returns>
        /// The result is a 32-bit packed unsigned integer where each bit is a binary indicator for a class.
        /// </returns>
        public uint GetSemantics(int viewportX, int viewportY)
        {
            if (!_packedBitmaskTextureInfo.Descriptor.valid || null == _packedBitmaskTextureInfo.Texture)
            {
                return 0;
            }

            // TODO(rbarnes): don't request new buffer each sample
            TryAcquirePackedSemanticChannelsCPUBuffer(out var packedBuffer);

            // TODO(rbarnes): remove once native impl is complete
            var samplerMatrix = GenerateDisplayMatrix(packedBuffer.width, packedBuffer.height);
            // sampler matrix = warp matrix * display matrix
            //var samplerMatrix = subsystem.SamplerMatrix;

            var viewportWidth = Screen.width;
            var viewportHeight = Screen.height;

            // Get normalized coordinates
            var xMid = viewportX + 0.5f;
            var yMid = viewportY + 0.5f;
            var uv = new Vector4(xMid / viewportWidth, yMid / viewportHeight, 1.0f, 1.0f);

            // Transform with warp and display matrices
            var st = samplerMatrix * uv;
            var sx = st.x / st.z;
            var sy = st.y / st.z;

            var textureWidth = packedBuffer.dimensions.x;
            var textureHeight = packedBuffer.dimensions.y;
            var x = Mathf.Clamp(Mathf.RoundToInt(sx * textureWidth - 0.5f), 0, textureWidth - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(sy * textureHeight - 0.5f), 0, textureHeight - 1);
            uint sample = 0;

            unsafe
            {
                var bufferView = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<UInt32>((void*) packedBuffer.buffer,
                    textureWidth * textureHeight, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref bufferView, AtomicSafetyHandle.GetTempMemoryHandle());
#endif

                sample = bufferView[x + textureWidth * y];
            }

            DisposePackedSemanticChannelsCPUBuffer(packedBuffer);

            return sample;
        }

        /// <summary>
        /// Returns an array of channel indices that are present at the specified pixel.
        /// </summary>
        /// <remarks>It is assumed that the texture fills the screen, so the viewport is the screen.</remarks>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <returns>An array of channel indices present for the pixel.</returns>
        public List<int> GetChannelIndicesAt(int viewportX, int viewportY)
        {
            var indices = new List<int>();
            if (_semanticChannelTextureInfos.Count == 0)
            {
                return indices;
            }

            uint sample = GetSemantics(viewportX, viewportY);

            for (int idx = 0; idx < 32; idx++)
            {
                // MSB = beginning of the channel names list
                if ((sample & (1 << 31)) != 0)
                {
                    indices.Add(idx);
                }

                sample <<= 1;
            }

            return indices;
        }

        /// <summary>
        /// Returns an array of channel names that are present at the specified pixel.
        /// </summary>
        /// <remarks>This query allocates garbage. It is assumed that the texture fills the screen.</remarks>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <returns>An array of channel names present for the pixel.</returns>
        public List<string> GetChannelNamesAt(int viewportX, int viewportY)
        {
            var names = new List<string>();
            if (_semanticChannelTextureInfos.Count == 0 || _semanticChannelNames.Count == 0)
            {
                return names;
            }

            var indices = GetChannelIndicesAt(viewportX, viewportY);

            foreach (var idx in indices)
            {
                if (idx >= _semanticChannelNames.Count)
                {
                    Debug.LogError("Semantics channel index exceeded channel names list");
                    return new List<string>();
                }

                names.Add(_semanticChannelNames[idx]);
            }

            return names;
        }

        /// <summary>
        /// Check if a semantic class is detected at the specified location in viewport space, based on the confidence
        /// threshold set for this channel. (See <c>TrySetChannelConfidenceThresholds</c>)
        /// </summary>
        /// <remarks>It is assumed that the texture fills the screen, so the viewport is the screen.</remarks>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <param name="channelName">Name of the semantic class to look for.</param>
        /// <returns>True if the semantic class exists at the given coordinates.</returns>
        public bool DoesChannelExistAt(int viewportX, int viewportY, string channelName)
        {
            if (_semanticChannelTextureInfos.Count == 0)
            {
                return false;
            }

            var channelNamesList = GetChannelNamesAt(viewportX, viewportY);
            return channelNamesList.Contains(channelName);
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
        /// returned by <c>GetChannelNames</c>.
        /// </param>
        /// <exception cref="System.NotSupportedException">Thrown when setting confidence thresholds is not
        /// supported by the implementation.</exception>
        /// <returns>True if the threshold was set. Otherwise, false.</returns>
        public bool TrySetChannelConfidenceThresholds(Dictionary<string,float> channelConfidenceThresholds)
        {
            return subsystem.TrySetChannelConfidenceThresholds(channelConfidenceThresholds);
        }

        /// <summary>
        /// Pull the texture descriptors from the semantic segmentation subsystem, and update the texture information
        /// maintained by this component.
        /// </summary>
        private void UpdateTexturesInfos()
        {
            foreach (string channel in SemanticChannelNames)
            {
                if (!subsystem.TryGetSemanticChannel(channel, out var textureDescriptor))
                {
                    continue;
                }

                var samplerMatrix = GenerateDisplayMatrix(textureDescriptor.width, textureDescriptor.height);

                if (!_semanticChannelTextureInfos.ContainsKey(channel))
                {
                    _semanticChannelTextureInfos.Add(channel, new ARTextureInfo(textureDescriptor, samplerMatrix));
                }
                else
                {
                    _semanticChannelTextureInfos[channel] = ARTextureInfo.GetUpdatedTextureInfo(_semanticChannelTextureInfos[channel], textureDescriptor, samplerMatrix);
                }
            }

            if (subsystem.TryGetPackedSemanticChannels(out var packedTextureDescriptor))
            {
                var samplerMatrix = GenerateDisplayMatrix(packedTextureDescriptor.width, packedTextureDescriptor.height);
                _packedBitmaskTextureInfo = ARTextureInfo.GetUpdatedTextureInfo(_packedBitmaskTextureInfo, packedTextureDescriptor, samplerMatrix);
            }
        }

        // Temporary function until the native sampler matrix implementation is ready
        private Matrix4x4 GenerateDisplayMatrix(int sourceWidth, int sourceHeight)
        {
            return _CameraMath.CalculateDisplayMatrix(
                sourceWidth,
                sourceHeight,
                (int)Screen.width,
                (int)Screen.height,
                Screen.orientation,
                layout: _CameraMath.MatrixLayout.RowMajor
            ).transpose;
        }
    }
}
