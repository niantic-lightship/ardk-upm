// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Subsystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.ARFoundation
{
    /// <summary>
    /// The manager for the semantic segmentation subsystem.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ARUpdateOrder.k_OcclusionManager)]
    public class ARSemanticSegmentationManager :
        SubsystemLifecycleManager<XRSemanticsSubsystem, XRSemanticsSubsystemDescriptor, XRSemanticsSubsystem.Provider>
    {
        /// <summary>
        /// The semantic segmentation confidence texture infos.
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
            if (subsystem != null)
            {
                UpdateTexturesInfos();
            }
        }

        private void ResetTextureInfos()
        {
            _packedBitmaskTextureInfo.Reset();

            foreach (KeyValuePair<string, ARTextureInfo> pair in _semanticChannelTextureInfos)
                pair.Value.Dispose();

            _semanticChannelTextureInfos.Clear();
        }

        /// <summary>
        /// Read a semantic segmentation texture.
        /// </summary>
        /// <value>
        /// The texture for the specified semantic channel, if any. Otherwise, <c>null</c>.
        /// </value>
        public Texture2D GetSemanticChannelTexture(string channelName)
        {
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

                return semanticConfidenceTextureInfo.Texture as Texture2D;
            }
            return null;
        }

        /// <summary>
        /// Attempt to get the latest semantic segmentation CPU buffer. This provides direct access to the raw pixel data.
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
        /// The <c>LightshipCpuBuffer</c> must have been successfully acquired with <c>TryAcquireSemanticChannelCPUBuffer</c>.
        /// </remarks>
        /// <param name="cpuBuffer">The <c>LightshipCpuBuffer</c> to dispose.</param>
        public void DisposeSemanticChannelCPUBuffer(LightshipCpuBuffer cpuBuffer)
        {
            subsystem.DisposeCPUImage(cpuBuffer);
        }

        /// <summary>
        ///  Tries to acquire the latest packed semantic channels CPU image.
        /// </summary>
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
        /// Get the channel index of a specified semantic class.
        /// </summary>
        /// <param name="channelName">Name of the semantic class.</param>
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
        /// Returns the semantics of the specified pixel in a viewport.
        /// </summary>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <param name="viewportWidth">Width of the viewport containing the semantic texture.</param>
        /// <param name="viewportHeight">Height of the viewport containing the semantic texture.</param>
        /// <returns>
        /// The result is a 32-bit packed unsigned integer where each bit is a binary indicator for a class.
        /// </returns>
        public uint GetSemantics(int viewportX, int viewportY, int viewportWidth, int viewportHeight)
        {
            if (!_packedBitmaskTextureInfo.Descriptor.valid || null == _packedBitmaskTextureInfo.Texture)
            {
                return 0;
            }

            Assert.IsTrue(viewportX >= 0 && viewportX < viewportWidth,
                $"viewportX must be inside the bounds of the specified viewport width " +
                $"({viewportWidth.ToString()} px) but instead is {viewportX.ToString()} px.");

            Assert.IsTrue(viewportY >= 0 && viewportY < viewportHeight,
                $"viewportY must be inside the bounds of the specified viewport height " +
                $"({viewportHeight.ToString()} px) but instead is {viewportY.ToString()} px.");

            // Get normalized coordinates
            var x = viewportX + 0.5f;
            var y = viewportY + 0.5f;
            var uv = new Vector3(x / viewportWidth, y / viewportHeight, 1.0f);
            var bufferWidth = _packedBitmaskTextureInfo.Descriptor.width;
            var bufferHeight = _packedBitmaskTextureInfo.Descriptor.height;

            int textureX = (int) (uv[0] * (bufferWidth - 1));
            // viewport coords origin is bottom-left
            int textureY = ((bufferHeight - 1) - (int) (uv[1] * (bufferHeight - 1))) *
                bufferWidth;

            TryAcquirePackedSemanticChannelsCPUBuffer(out var packedBuffer);
            uint sample = 0;

            unsafe
            {
                var bufferView = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<UInt32>((void*) packedBuffer.buffer,
                    packedBuffer.dimensions.x * packedBuffer.dimensions.y, Allocator.None);
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref bufferView, AtomicSafetyHandle.GetTempMemoryHandle());
                #endif

                sample = bufferView[textureY + textureX];
            }

            DisposePackedSemanticChannelsCPUBuffer(packedBuffer);

            return sample;
        }

        /// <summary>
        /// Returns an array of channel indices that are present for the specified pixel.
        /// </summary>
        /// <remarks>This query allocates garbage.</remarks>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <param name="viewportWidth">Width of the viewport containing the semantic texture.</param>
        /// <param name="viewportHeight">Height of the viewport containing the semantic texture.</param>
        /// <returns>An array of channel indices present for the pixel.</returns>
        public List<int> GetChannelIndicesAt(int viewportX, int viewportY, int viewportWidth, int viewportHeight)
        {
            var indices = new List<int>();
            if (_semanticChannelTextureInfos.Count == 0)
            {
                return indices;
            }

            uint sample = GetSemantics(viewportX, viewportY, viewportWidth, viewportHeight);

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
        /// Returns an array of channel names that are present for the specified pixel.
        /// </summary>
        /// <remarks>This query allocates garbage. It is assumed that the texture fills the viewport.</remarks>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <param name="viewportWidth">Width of the viewport containing the semantic texture.</param>
        /// <param name="viewportHeight">Height of the viewport containing the semantic texture.</param>
        /// <returns>An array of channel names present for the pixel.</returns>
        public List<string> GetChannelNamesAt(int viewportX, int viewportY, int viewportWidth, int viewportHeight)
        {
            var names = new List<string>();
            if (_semanticChannelTextureInfos.Count == 0 || _semanticChannelNames.Count == 0)
            {
                return names;
            }

            var indices = GetChannelIndicesAt(viewportX, viewportY, viewportWidth, viewportHeight);

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
        /// Check if a pixel is of a certain semantics class.
        /// </summary>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <param name="viewportWidth">Width of the viewport containing the semantic texture.</param>
        /// <param name="viewportHeight">Height of the viewport containing the semantic texture.</param>
        /// <param name="channelName">Name of the semantic class to look for.</param>
        /// <returns>True if the semantic channel exists at the given coordinates.</returns>
        public bool DoesChannelExistAt(int viewportX, int viewportY, int viewportWidth, int viewportHeight, string channelName)
        {
            if (_semanticChannelTextureInfos.Count == 0)
            {
                return false;
            }

            var channelNamesList = GetChannelNamesAt(viewportX, viewportY, viewportWidth, viewportHeight);
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

                if (!_semanticChannelTextureInfos.ContainsKey(channel))
                {
                    _semanticChannelTextureInfos.Add(channel, new ARTextureInfo(textureDescriptor));
                }
                else
                {
                    _semanticChannelTextureInfos[channel] = ARTextureInfo.GetUpdatedTextureInfo(_semanticChannelTextureInfos[channel], textureDescriptor);
                }
            }

            if (subsystem.TryGetPackedSemanticChannels(out var packedTextureDescriptor))
            {
                _packedBitmaskTextureInfo = ARTextureInfo.GetUpdatedTextureInfo(_packedBitmaskTextureInfo, packedTextureDescriptor);
            }
        }
    }
}
