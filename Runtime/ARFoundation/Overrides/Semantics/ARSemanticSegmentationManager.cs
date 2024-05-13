// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Niantic.Lightship.AR.ARFoundation.Unity;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Subsystems.Common;
using Niantic.Lightship.AR.Subsystems.Semantics;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Semantics
{
    /// <summary>
    /// The <c>ARSemanticSegmentationManager</c> controls the <c>XRSemanticsSubsystem</c> and updates the semantics
    /// textures on each Update loop. Textures and XRCpuImages are available for confidence maps of individual semantic
    /// segmentation channels and a bit array indicating which semantic channels have surpassed the chosen confidence
    /// threshold per pixel. For cases where a semantic segmentation texture is overlaid on the screen, utilities are
    /// provided to read semantic properties at a given point on the screen.
    /// </summary>
    [PublicAPI("apiref/Niantic/Lightship/AR/Semantics/ARSemanticSegmentationManager/")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(LightshipARUpdateOrder.SemanticSegmentationManager)]
    public class ARSemanticSegmentationManager :
        SubsystemLifecycleManager<XRSemanticsSubsystem, XRSemanticsSubsystemDescriptor, XRSemanticsSubsystem.Provider>
    {
        [SerializeField]
        [Tooltip("Frame rate that semantic segmentation inference will aim to run at")]
        [Range(1, 90)]
        private uint _targetFrameRate = LightshipSemanticsSubsystem.MaxRecommendedFrameRate;

        /// <summary>
        /// Frame rate that semantic segmentation inference will aim to run at.
        /// </summary>
        public uint TargetFrameRate
        {
            get => subsystem?.TargetFrameRate ?? _targetFrameRate;
            set
            {
                if (value <= 0)
                {
                    Log.Error("Target frame rate value must be greater than zero.");
                    return;
                }

                _targetFrameRate = value;
                if (subsystem != null)
                {
                    subsystem.TargetFrameRate = value;
                }
            }
        }

        // This is used to cache the suppression mask channels until metadata is available
        private List<string> _cachedSuppressionMaskChannels = new List<string>();

        internal List<string> SuppressionMaskChannels
        {
            get
            {
                if (subsystem != null && subsystem.IsMetadataAvailable)
                {
                    return subsystem?.SuppressionMaskChannels ?? new List<string>();
                }
                else
                {
                    return _cachedSuppressionMaskChannels;
                }
            }
            set
            {
                if (subsystem != null && subsystem.IsMetadataAvailable)
                {
                    subsystem.SuppressionMaskChannels = value;
                    _cachedSuppressionMaskChannels = value;
                }
                else
                {
                    _cachedSuppressionMaskChannels = value;
                }
            }
        }

        /// <summary>
        /// The names of the semantic channels that the current model is able to detect.
        /// </summary>
        public IReadOnlyList<string> ChannelNames => _readOnlyChannelNames;

        private IReadOnlyList<string> _readOnlyChannelNames;

        /// <summary>
        /// The indices of the semantic channels that the current model is able to detect.
        /// </summary>
        public IReadOnlyDictionary<string, int> ChannelIndices => _readOnlyChannelNamesToIndices;

        private IReadOnlyDictionary<string, int> _readOnlyChannelNamesToIndices;
        private Dictionary<string, int> _channelNamesToIndices = new();

        /// <summary>
        /// True if the underlying subsystem has finished initialization.
        /// </summary>
        public bool IsMetadataAvailable { get; private set; }

        /// <summary>
        /// An event which fires when the underlying subsystem has finished initializing.
        /// </summary>
        public event Action<ARSemanticSegmentationModelEventArgs> MetadataInitialized
        {
            add
            {
                _metadataInitialized += value;
                if (IsMetadataAvailable)
                {
                    var args =
                        new ARSemanticSegmentationModelEventArgs
                        {
                            ChannelNames = ChannelNames,
                            ChannelIndices = ChannelIndices
                        };

                    value.Invoke(args);
                }
            }
            remove
            {
                _metadataInitialized -= value;
            }
        }

        private Action<ARSemanticSegmentationModelEventArgs> _metadataInitialized;

        public event Action<ARSemanticSegmentationFrameEventArgs> FrameReceived;

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

        /// <summary>
        /// The suppression mask texture info.
        /// </summary>
        /// <value>
        ///The suppression mask texture info.
        /// </value>
        private ARTextureInfo _suppressionMaskTextureInfo;

        /// <summary>
        /// Frequently updated information about the viewport.
        /// </summary>
        private XRCameraParams _viewport;

        /// <summary>
        /// The frame id of the last seen semantic segmentation output buffer.
        /// </summary>
        private uint? _lastKnownFrameId;

        /// <summary>
        /// Callback before the subsystem is started (but after it is created).
        /// </summary>
        protected override void OnBeforeStart()
        {
            TargetFrameRate = _targetFrameRate;
            ResetTextureInfos();
            ResetModelMetadata();
        }

        /// <summary>
        /// Callback when the manager is being disabled.
        /// </summary>
        protected override void OnDisable()
        {
            // Reset suppression channels before
            // shutting down the subsystem
            SuppressionMaskChannels = null;

            // Stop the subsystem
            base.OnDisable();

            // Reset textures and meta data
            ResetTextureInfos();
            ResetModelMetadata();
        }

        /// <summary>
        /// Callback as the manager is being updated.
        /// </summary>
        public void Update()
        {
            if (subsystem == null)
                return;

            TargetFrameRate = _targetFrameRate;

            if (!subsystem.running)
                return;

            if (!IsMetadataAvailable)
            {
                if (!subsystem.TryGetChannelNames(out var channelNames))
                    return;

                subsystem.SuppressionMaskChannels = _cachedSuppressionMaskChannels;
                SetModelMetadata(channelNames);

                var args =
                    new ARSemanticSegmentationModelEventArgs
                    {
                        ChannelNames = ChannelNames,
                        ChannelIndices = ChannelIndices
                    };

                _metadataInitialized?.Invoke(args);
            }

            // Method will have exited already if metadata (ie channel names) are not available,
            // so below code only executes when metadata is available.

            // Update viewport info
            _viewport.screenWidth = Screen.width;
            _viewport.screenHeight = Screen.height;
            _viewport.screenOrientation = XRDisplayContext.GetScreenOrientation();

            // Invoke event if new keyframe is available
            var currentFrameId = subsystem.LatestFrameId;
            if (currentFrameId != _lastKnownFrameId)
            {
                _lastKnownFrameId = currentFrameId;
                var eventArgs = new ARSemanticSegmentationFrameEventArgs();
                FrameReceived?.Invoke(eventArgs);
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

        private void ResetModelMetadata()
        {
            IsMetadataAvailable = false;
            _readOnlyChannelNames = new List<string>().AsReadOnly();

            _channelNamesToIndices = new Dictionary<string, int>();
            _readOnlyChannelNamesToIndices = new ReadOnlyDictionary<string, int>(_channelNamesToIndices);
        }

        private void SetModelMetadata(IReadOnlyList<string> channelNames)
        {
            IsMetadataAvailable = true;
            _readOnlyChannelNames = channelNames;

            _channelNamesToIndices.Clear();
            for (int i = 0; i < channelNames.Count; i++)
            {
                _channelNamesToIndices.Add(channelNames[i], i);
            }

            _readOnlyChannelNamesToIndices = new ReadOnlyDictionary<string, int>(_channelNamesToIndices);
        }

        /// <summary>
        /// Returns semantic segmentation texture for the specified semantic channel.
        /// </summary>
        /// <param name="channelName">The semantic channel to acquire.</param>
        /// <param name="samplerMatrix">A matrix that converts from viewport to image coordinates according to the latest pose.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>The texture for the specified semantic channel, if any. Otherwise, <c>null</c>.</returns>
        public Texture2D GetSemanticChannelTexture(string channelName, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
        {
            cameraParams ??= _viewport;

            // If semantic segmentation is unavailable
            if (descriptor?.semanticSegmentationImageSupported != Supported.Supported)
            {
                samplerMatrix = default;
                return null;
            }

            // If we already have an up-to-date texture
            if (_semanticChannelTextureInfos.TryGetValue(channelName, out ARTextureInfo info))
            {
                if (!info.IsDirty && info.CameraParams == cameraParams)
                {
                    samplerMatrix = info.SamplerMatrix;
                    return info.Texture as Texture2D;
                }
            }

            // Acquire the new texture descriptor
            if (!subsystem.TryGetSemanticChannel(channelName, out var textureDescriptor, out samplerMatrix, cameraParams))
            {
                samplerMatrix = default;
                return null;
            }

            // Format mismatch
            if (textureDescriptor.dimension != TextureDimension.Tex2D)
            {
                Log.Error
                (
                    "Semantic confidence texture needs to be a Texture2D, but is " + textureDescriptor.dimension + "."
                );

                samplerMatrix = default;
                return null;
            }

            // Cache the texture
            if (!_semanticChannelTextureInfos.ContainsKey(channelName))
            {
                _semanticChannelTextureInfos.Add
                (
                    channelName,
                    new ARTextureInfo(textureDescriptor, samplerMatrix, cameraParams.Value)
                );
            }
            else
            {
                _semanticChannelTextureInfos[channelName] =
                    ARTextureInfo.GetUpdatedTextureInfo
                    (
                        _semanticChannelTextureInfos[channelName],
                        textureDescriptor,
                        samplerMatrix,
                        cameraParams.Value
                    );
            }

            return _semanticChannelTextureInfos[channelName].Texture as Texture2D;
        }

        /// <summary>
        /// Retrieves the texture of semantic data where each pixel can be interpreted as a uint with bits
        /// corresponding to different classifications.
        /// </summary>
        /// <param name="samplerMatrix">A matrix that converts from viewport to image coordinates according to the latest pose.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>The packed semantics texture, owned by the manager, if any. Otherwise, <c>null</c>.</returns>
        public Texture2D GetPackedSemanticsChannelsTexture(out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
        {
            cameraParams ??= _viewport;

            // Update the packed semantics texture. This will only update the external texture if
            // the subsystem returns a different textureDescriptor.
            if (subsystem.TryGetPackedSemanticChannels(out var textureDescriptor, out samplerMatrix, cameraParams))
            {
                _packedBitmaskTextureInfo = ARTextureInfo.GetUpdatedTextureInfo(_packedBitmaskTextureInfo,
                    textureDescriptor, samplerMatrix, cameraParams.Value);
                return _packedBitmaskTextureInfo.Texture as Texture2D;
            }

            samplerMatrix = default;
            return null;
        }

        /// <summary>
        /// Retrieves the suppression mask texture, where each pixel contains a uint which can be used to interpolate
        /// between the predicted depth and the far field depth of the scene. This is useful for enabling smooth occlusion suppression.
        /// </summary>
        /// <param name="samplerMatrix">A matrix that converts from viewport to image coordinates according to the latest pose.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>The suppression mask texture, owned by the manager, if any. Otherwise, <c>null</c>.</returns>
        public Texture2D GetSuppressionMaskTexture(out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
        {
            cameraParams ??= _viewport;

            if (subsystem.TryGetSuppressionMaskTexture(out var textureDescriptor, out samplerMatrix, cameraParams))
            {
                _suppressionMaskTextureInfo = ARTextureInfo.GetUpdatedTextureInfo(_suppressionMaskTextureInfo,
                    textureDescriptor, samplerMatrix, cameraParams.Value);
                return _suppressionMaskTextureInfo.Texture as Texture2D;
            }

            samplerMatrix = default;
            return null;
        }

        /// <summary>
        /// Attempt to acquire the latest semantic segmentation XRCpuImage for the specified semantic class. This
        /// provides direct access to the raw pixel data.
        /// </summary>
        /// <remarks>
        /// The <c>XRCpuImage</c> must be disposed to avoid resource leaks.
        /// </remarks>
        /// <param name="channel">The semantic channel to acquire.</param>
        /// <param name="cpuImage">If this method returns `true`, an acquired <see cref="XRCpuImage"/>. The XRCpuImage
        /// must be disposed by the caller.</param>
        /// <param name="samplerMatrix">A matrix that converts from viewport to image coordinates according to the latest pose.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>True if the CPU image was acquired. Otherwise, false</returns>
        public bool TryAcquireSemanticChannelCpuImage(string channel, out XRCpuImage cpuImage, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
        {
            cameraParams ??= _viewport;
            return subsystem.TryAcquireSemanticChannelCpuImage(channel, out cpuImage, out samplerMatrix, cameraParams);
        }

        /// <summary>
        /// Tries to acquire the latest packed semantic channels XRCpuImage. Each element of the XRCpuImage is a bit field
        /// indicating which semantic channels have surpassed their respective detection confidence thresholds for that
        /// pixel. (See <c>GetChannelIndex</c>)
        /// </summary>
        /// <remarks>The utility <c>GetChannelNamesAt</c> can be used for reading semantic channel names at a viewport
        /// location.</remarks>
        /// <param name="cpuImage">If this method returns `true`, an acquired <see cref="XRCpuImage"/>. The CPU image
        /// must be disposed by the caller.</param>
        /// <param name="samplerMatrix">A matrix that converts from viewport to image coordinates according to the latest pose.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>True if the CPU image was acquired. Otherwise, false</returns>
        public bool TryAcquirePackedSemanticChannelsCpuImage(out XRCpuImage cpuImage, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
        {
            cameraParams ??= _viewport;
            return subsystem.TryAcquirePackedSemanticChannelsCpuImage(out cpuImage, out samplerMatrix, cameraParams);
        }

        /// <summary>
        /// Tries to acquire the latest suppression mask XRCpuImage. Each element of the XRCpuImage is a uint32 value
        /// which can be used to interpolate between instantaneous depth and far field depth.
        /// </summary>
        /// <param name="cpuImage">If this method returns `true`, an acquired <see cref="XRCpuImage"/>. The CPU image
        /// must be disposed by the caller.</param>
        /// <param name="samplerMatrix">A matrix that converts from viewport to image coordinates according to the latest pose.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>True if the CPU image was acquired. Otherwise, false</returns>
        public bool TryAcquireSuppressionMaskCpuImage(out XRCpuImage cpuImage, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
        {
            cameraParams ??= _viewport;
            return subsystem.TryAcquireSuppressionMaskCpuImage(out cpuImage, out samplerMatrix, cameraParams);
        }

        /// <summary>
        /// Get the channel index of a specified semantic class. This corresponds to a bit position in the packed
        /// semantics buffer, with index 0 being the most-significant bit.
        /// </summary>
        /// <param name="channelName">The name of the semantic class.</param>
        /// <returns>The index of the specified semantic class, or -1 if the channel does not exist.</returns>
        public int GetChannelIndex(string channelName)
        {
            if (_channelNamesToIndices.TryGetValue(channelName, out int index))
            {
                return index;
            }

            return -1;
        }

        /// <summary>
        /// Returns the semantics at the specified pixel on screen.
        /// </summary>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>A 32-bit packed unsigned integer where each
        /// bit is a binary indicator for a class, and the most-significant bit corresponds to the channel that
        /// is the 0th element of the ChannelNames list.</returns>
        public uint GetSemantics(int viewportX, int viewportY, XRCameraParams? cameraParams = null)
        {
            cameraParams ??= _viewport;

            // Acquire the CPU image
            if (!subsystem.TryAcquirePackedSemanticChannelsCpuImage(out var cpuImage, out var samplerMatrix, cameraParams))
            {
                return 0u;
            }

            // Get normalized image coordinates
            var x = viewportX + 0.5f;
            var y = viewportY + 0.5f;
            var uv = new Vector2(x / cameraParams.Value.screenWidth, y / cameraParams.Value.screenHeight);

            // Sample the image
            uint sample = cpuImage.Sample<uint>(uv, samplerMatrix);

            cpuImage.Dispose();
            return sample;
        }

        /// <summary>
        /// Returns an array of channel indices that are present at the specified pixel onscreen.
        /// </summary>
        /// <remarks>This query allocates garbage.</remarks>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>An array of channel indices present for the pixel.</returns>
        public List<int> GetChannelIndicesAt(int viewportX, int viewportY, XRCameraParams? cameraParams = null)
        {
            uint sample = GetSemantics(viewportX, viewportY, cameraParams);

            var indices = new List<int>();
            for (int idx = 0; idx < ChannelNames.Count; idx++)
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
        /// Returns an array of channel names that are present for the specified pixel onscreen.
        /// </summary>
        /// <remarks>This query allocates garbage.</remarks>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>An array of channel names present for the pixel.</returns>
        public List<string> GetChannelNamesAt(int viewportX, int viewportY, XRCameraParams? cameraParams = null)
        {
            var names = new List<string>();
            if (_readOnlyChannelNames.Count == 0)
            {
                return names;
            }

            var indices = GetChannelIndicesAt(viewportX, viewportY, cameraParams);

            foreach (var idx in indices)
            {
                if (idx >= _readOnlyChannelNames.Count)
                {
                    Log.Error("Semantics channel index exceeded channel names list");
                    return new List<string>();
                }

                names.Add(_readOnlyChannelNames[idx]);
            }

            return names;
        }

        /// <summary>
        /// Check if a semantic class is detected at the specified location in screen space, based on the confidence
        /// threshold set for this channel. (See <c>TrySetChannelConfidenceThresholds</c>)
        /// </summary>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <param name="channelName">Name of the semantic class to look for.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>True if the semantic class exists at the given coordinates.</returns>
        public bool DoesChannelExistAt(int viewportX, int viewportY, string channelName, XRCameraParams? cameraParams = null)
        {
            if (ChannelIndices.TryGetValue(channelName, out int index))
            {
                return DoesChannelExistAt(viewportX, viewportY, index, cameraParams);
            }

            return false;
        }

        /// <summary>
        /// Check if a semantic class is detected at the specified location in screen space, based on the confidence
        /// threshold set for this channel. (See <c>TrySetChannelConfidenceThresholds</c>)
        /// </summary>
        /// <param name="viewportX">Horizontal coordinate in viewport space.</param>
        /// <param name="viewportY">Vertical coordinate in viewport space.</param>
        /// <param name="channelIndex">Index of the semantic class to look for in the ChannelNames list.</param>
        /// <param name="cameraParams">Params of the viewport to sample with. Defaults to current screen dimensions if null.</param>
        /// <returns>True if the semantic class exists at the given coordinates.</returns>
        public bool DoesChannelExistAt(int viewportX, int viewportY, int channelIndex, XRCameraParams? cameraParams = null)
        {
            uint channelIndices = GetSemantics(viewportX, viewportY, cameraParams);
            return (channelIndices & (1 << (31 - channelIndex))) != 0;
        }

        /// <summary>
        /// Sets the confidence threshold for including the specified semantic channel in the packed semantic
        /// channel buffer.
        /// </summary>
        /// <remarks>
        /// Each semantic channel will use its default threshold value chosen by the model until a new value is set
        /// by this function during the AR session.
        /// Changes to the semantic segmentation thresholds are undone by either restarting the subsystem or by calling
        /// <see cref="TryResetChannelConfidenceThresholds"/>.
        /// </remarks>
        /// <param name="channelConfidenceThresholds">
        /// A dictionary consisting of keys specifying the name of the semantics channel that is needed and values
        /// between 0 and 1, inclusive, that set the threshold above which the platform will include the specified
        /// channel in the packed semantics buffer. The key must be a semantic channel name present in the list
        /// returned by <c>TryGetChannelNames</c>.
        /// </param>
        /// <exception cref="System.NotSupportedException">Thrown when setting confidence thresholds is not
        /// supported by the implementation.</exception>
        /// <returns>True if the thresholds were set. Otherwise, false.</returns>
        public bool TrySetChannelConfidenceThresholds(Dictionary<string,float> channelConfidenceThresholds)
        {
            return subsystem.TrySetChannelConfidenceThresholds(channelConfidenceThresholds);
        }

        /// <summary>
        /// Resets the confidence thresholds for all semantic channels to the default values from the current model.
        /// </summary>
        /// <remarks>
        /// This reverts any changes made with <see cref="TrySetChannelConfidenceThresholds"/>.
        /// </remarks>
        /// <exception cref="System.NotSupportedException">Thrown when resetting confidence thresholds is not
        /// supported by the implementation.</exception>
        /// <returns>True if the thresholds were reset. Otherwise, false.</returns>
        public bool TryResetChannelConfidenceThresholds()
        {
            return subsystem.TryResetChannelConfidenceThresholds();
        }
    }
}
