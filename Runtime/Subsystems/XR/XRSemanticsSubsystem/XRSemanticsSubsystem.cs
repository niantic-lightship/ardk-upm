// Copyright 2022-2025 Niantic.
using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Subsystems.XR;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.SubsystemsImplementation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Defines an interface for interacting with semantic segmentation functionality.
    /// </summary>
    [PublicAPI]
    public class XRSemanticsSubsystem
        : SubsystemWithProvider<XRSemanticsSubsystem, XRSemanticsSubsystemDescriptor, XRSemanticsSubsystem.Provider>,
            ISubsystemWithModelMetadata
    {
        /// <summary>
        /// Construct the subsystem by creating the functionality provider.
        /// </summary>
        public XRSemanticsSubsystem() { }

        /// <summary>
        /// Get the XRTextureDescriptor for the specified semantic channel.
        /// </summary>
        /// <param name="channelName">The string description of the semantics channel to acquire.</param>
        /// <param name="semanticsChannelDescriptor">
        /// The resulting semantic channel texture descriptor, if available.
        /// </param>
        /// <param name="samplerMatrix">
        /// The matrix that transforms from normalized viewport coordinates to normalized texture coordinates.
        /// </param>
        /// <param name="cameraParams">Describes the viewport the texture is to be displayed on.</param>
        /// <returns>
        /// <c>true</c> if the semantic channel texture descriptor is available and is returned. Otherwise,
        /// <c>false</c>.
        /// </returns>
        /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support semantics channel
        /// texture.</exception>
        public bool TryGetSemanticChannel(string channelName, out XRTextureDescriptor semanticsChannelDescriptor, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
            => provider.TryGetSemanticChannel(channelName, out semanticsChannelDescriptor, out samplerMatrix, cameraParams);

        /// <summary>
        /// Acquire the latest semantic channel CPU image.
        /// </summary>
        /// <param name="channelName">The string description of the semantics channel to acquire.</param>
        /// <param name="cpuImage">
        /// The resulting <see cref="XRCpuImage"/>. The XRCpuImage must be disposed by the caller.
        /// </param>
        /// <param name="samplerMatrix">
        /// The matrix that transforms from normalized viewport coordinates to normalized image coordinates.
        /// </param>
        /// <param name="cameraParams">Describes the viewport the image is to be displayed on.</param>
        /// <returns>
        /// Returns `true` if an <see cref="XRCpuImage"/> was successfully acquired. Returns `false` otherwise.
        /// </returns>
        /// <exception cref="System.NotSupportedException">
        /// Thrown if the implementation does not support semantic channels CPU images.
        /// </exception>
        public bool TryAcquireSemanticChannelCpuImage(string channelName, out XRCpuImage cpuImage, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
        {
            return provider.TryAcquireSemanticChannelCpuImage(channelName, out cpuImage, out samplerMatrix, cameraParams);
        }

        /// <summary>
        /// Get the packed semantics texture descriptor.
        /// </summary>
        /// <param name="packedSemanticsDescriptor">
        /// The resulting semantic channel texture descriptor, if available.
        /// </param>
        /// <param name="samplerMatrix">
        /// The matrix that transforms from normalized viewport coordinates to normalized texture coordinates.
        /// </param>
        /// <param name="cameraParams">Describes the viewport the texture is to be displayed on.</param>
        /// <returns>
        /// <c>true</c> if the packed semantics texture descriptor is available and is returned. Otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support packed semantics
        /// texture.</exception>
        public bool TryGetPackedSemanticChannels(out XRTextureDescriptor packedSemanticsDescriptor, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
            => provider.TryGetPackedSemanticChannels(out packedSemanticsDescriptor, out samplerMatrix, cameraParams);

        /// <summary>
        /// Acquire the latest packed semantic channels XRCpuImage.
        /// </summary>
        /// <param name="cpuImage">
        /// The resulting <see cref="XRCpuImage"/>. The XRCpuImage must be disposed by the caller.
        /// </param>
        /// <param name="samplerMatrix">
        /// The matrix that transforms from normalized viewport coordinates to normalized image coordinates.
        /// </param>
        /// <param name="cameraParams">Describes the viewport the image is to be displayed on.</param>
        /// <returns>
        /// Returns `true` if an <see cref="XRCpuImage"/> was successfully acquired. Returns `false` otherwise.
        /// </returns>
        public bool TryAcquirePackedSemanticChannelsCpuImage(out XRCpuImage cpuImage, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
            => provider.TryAcquirePackedSemanticChannelsCpuImage(out cpuImage, out samplerMatrix, cameraParams);

        /// <summary>
        /// Get a semantic suppression texture descriptor.
        /// </summary>
        /// <param name="suppressionMaskDescriptor">
        /// The resulting semantic suppression mask texture descriptor, if available.
        /// </param>
        /// <param name="samplerMatrix">
        /// The matrix that transforms from normalized viewport coordinates to normalized texture coordinates.
        /// </param>
        /// <param name="cameraParams">Describes the viewport the texture is to be displayed on.</param>
        /// <returns>
        /// <c>true</c> if the suppression mask texture descriptor is available and is returned. Otherwise,
        /// <c>false</c>.
        /// </returns>
        /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support suppression mask texture.</exception>
        public bool TryGetSuppressionMaskTexture(out XRTextureDescriptor suppressionMaskDescriptor,
            out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
            => provider.TryGetSuppressionMaskTexture(out suppressionMaskDescriptor, out samplerMatrix, cameraParams);

        /// <summary>
        /// Acquire the latest suppression mask XRCpuImage.
        /// </summary>
        /// <param name="cpuImage">
        /// The resulting <see cref="XRCpuImage"/>. The XRCpuImage must be disposed by the caller.
        /// </param>
        /// <param name="samplerMatrix">
        /// The matrix that transforms from normalized viewport coordinates to normalized image coordinates.
        /// </param>
        /// <param name="cameraParams">Describes the viewport the image is to be displayed on.</param>
        /// <returns>
        /// Returns `true` if an <see cref="XRCpuImage"/> was successfully acquired. Returns `false` otherwise.
        /// </returns>
        public bool TryAcquireSuppressionMaskCpuImage(out XRCpuImage cpuImage,
            out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
            => provider.TryAcquireSuppressionMaskCpuImage(out cpuImage, out samplerMatrix, cameraParams);

        /// <summary>
        /// Acquire the latest suppression mask XRCpuImage.
        /// </summary>
        /// <param name="cpuImage">
        /// The resulting <see cref="XRCpuImage"/>. The XRCpuImage must be disposed by the caller.
        /// </param>
        /// <param name="samplerMatrix">
        /// The matrix that transforms from normalized viewport coordinates to normalized image coordinates.
        /// </param>
        /// <param name="cameraParams">Describes the viewport the image is to be displayed on.</param>
        /// <param name="targetPose">
        /// Any image acquired has been captured in the past. The target pose argument defines the pose the image
        /// needs to synchronize with. If the image can be synchronized, the <see cref="samplerMatrix"/> will be
        /// calibrated to warp the image as if it was taken from the target pose. If this argument is null, the
        /// image will not be warped.
        /// </param>
        /// <returns>
        /// Returns `true` if an <see cref="XRCpuImage"/> was successfully acquired. Returns `false` otherwise.
        /// </returns>
        public bool TryAcquireSuppressionMaskCpuImage(out XRCpuImage cpuImage,
            out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams, Matrix4x4? targetPose)
            => provider.TryAcquireSuppressionMaskCpuImage(out cpuImage, out samplerMatrix, cameraParams, targetPose);

        /// <summary>
        /// Register the descriptor for the semantics subsystem implementation.
        /// </summary>
        /// <param name="semanticsSubsystemCinfo">The semantics subsystem implementation construction information.
        /// </param>
        /// <returns>
        /// <c>true</c> if the descriptor was registered. Otherwise, <c>false</c>.
        /// </returns>
        public static bool Register(XRSemanticsSubsystemCinfo semanticsSubsystemCinfo)
        {
            var semanticsSubsystemDescriptor = XRSemanticsSubsystemDescriptor.Create(semanticsSubsystemCinfo);
            SubsystemDescriptorStore.RegisterDescriptor(semanticsSubsystemDescriptor);
            return true;
        }

        /// <summary>
        /// Specifies the target frame rate for the platform to target running semantic segmentation inference at.
        /// </summary>
        /// <value>
        /// The target frame rate.
        /// </value>
        /// <exception cref="System.NotSupportedException">Thrown frame rate configuration is not supported.
        /// </exception>
        public uint TargetFrameRate
        {
            get => provider.TargetFrameRate;
            set => provider.TargetFrameRate = value;
        }

        public HashSet<string> SuppressionMaskChannels
        {
            get => provider.SuppressionMaskChannels;
            set => provider.SuppressionMaskChannels = value;
        }

        /// <summary>
        /// Returns the frame id of the most recent semantic segmentation prediction.
        /// </summary>
        /// <value>
        /// The frame id.
        /// </value>
        /// <exception cref="System.NotSupportedException">Thrown if getting frame id is not supported.
        /// </exception>
        public uint? LatestFrameId
        {
            get
            {
                return provider.LatestFrameId;
            }
        }

        /// <summary>
        /// Is true if metadata has been downloaded and decrypted on the current device. Only if this value
        /// is true can the semantic segmentation label names or inference results be acquired.
        /// </summary>
        /// <value>
        /// If metadata is available.
        /// </value>
        /// <exception cref="System.NotSupportedException">
        /// Thrown frame rate configuration is not supported.
        /// </exception>
        public bool IsMetadataAvailable
        {
            get
            {
                return provider.IsMetadataAvailable;
            }
        }

        /// <summary>
        /// Get a list of the semantic channel names for the current semantic model.
        /// </summary>
        /// <returns>
        /// A list of semantic category labels. The list will be empty if metadata has not yet become available.
        /// </returns>
        /// <exception cref="System.NotSupportedException">Thrown when reading the channel names is not supported
        /// by the implementation.</exception>
        public bool TryGetChannelNames(out IReadOnlyList<string> names) => provider.TryGetChannelNames(out names);

        /// <summary>
        /// Sets the confidence thresholds used for including the specified semantic channels in the packed semantic
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
        /// returned by <c>TryGetChannelNames</c>.
        /// </param>
        /// <exception cref="System.NotSupportedException">Thrown when setting confidence thresholds is not
        /// supported by the implementation.</exception>
        /// <returns>True if the threshold was set. Otherwise, false.</returns>
        public bool TrySetChannelConfidenceThresholds(Dictionary<string,float> channelConfidenceThresholds)
            => provider.TrySetChannelConfidenceThresholds(channelConfidenceThresholds);

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
            => provider.TryResetChannelConfidenceThresholds();


        /// <summary>
        /// Invoked when the subsystem is being stopped.
        /// </summary>
        protected override void OnStop()
        {
            // Reset the suppression mask channels
            if (IsMetadataAvailable)
            {
                SuppressionMaskChannels = null;
            }

            base.OnStop();
        }

        /// <summary>
        /// The provider which will service the <see cref="XRSemanticsSubsystem"/>.
        /// </summary>
        public abstract class Provider : SubsystemProvider<XRSemanticsSubsystem>
        {
            /// <summary>
            /// If the semantic segmentation model is ready, prepare the subsystem's data structures.
            /// </summary>
            /// <returns>
            /// <c>true</c> if the semantic segmentation model is ready and the subsystem has prepared its data structures. Otherwise,
            /// <c>false</c>.
            /// </returns>
            public virtual bool TryPrepareSubsystem()
                => throw new NotSupportedException("TryPrepareSubsystem is not supported by this implementation");

            /// <summary>
            /// Get the XRTextureDescriptor for the specified semantic channel.
            /// </summary>
            /// <param name="channelName">The string description of the semantics channel to acquire.</param>
            /// <param name="semanticChannelDescriptor">
            /// The resulting semantic channel texture descriptor, if available.
            /// </param>
            /// <param name="samplerMatrix">
            /// The matrix that transforms from normalized viewport coordinates to normalized texture coordinates.
            /// </param>
            /// <param name="cameraParams">Describes the viewport the texture is to be displayed on.</param>
            /// <returns>
            /// <c>true</c> if the semantic channel texture descriptor is available and is returned. Otherwise,
            /// <c>false</c>.
            /// </returns>
            /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support semantics channel
            /// texture.</exception>
            public virtual bool TryGetSemanticChannel(string channelName, out XRTextureDescriptor semanticChannelDescriptor, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
                => throw new NotSupportedException("Semantic channel texture is not supported by this implementation");

            /// <summary>
            /// Acquire the latest semantic channel CPU image.
            /// </summary>
            /// <param name="channelName">The string description of the semantics channel to acquire.</param>
            /// <param name="cpuImage">
            /// The resulting <see cref="XRCpuImage"/>. The XRCpuImage must be disposed by the caller.
            /// </param>
            /// <param name="samplerMatrix">
            /// The matrix that transforms from normalized viewport coordinates to normalized image coordinates.
            /// </param>
            /// <param name="cameraParams">Describes the viewport the image is to be displayed on.</param>
            /// <returns>
            /// Returns `true` if an <see cref="XRCpuImage"/> was successfully acquired. Returns `false` otherwise.
            /// </returns>
            /// <exception cref="System.NotSupportedException">
            /// Thrown if the implementation does not support semantic channels CPU images.
            /// </exception>
            public virtual bool TryAcquireSemanticChannelCpuImage(string channelName, out XRCpuImage cpuImage,  out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
                => throw new NotSupportedException("Semantic channel CPU images are not supported by this implementation.");

            /// <summary>
            /// Get the packed semantics texture descriptor.
            /// </summary>
            /// <param name="packedSemanticsDescriptor">
            /// The resulting semantic channel texture descriptor, if available.
            /// </param>
            /// <param name="samplerMatrix">
            /// The matrix that transforms from normalized viewport coordinates to normalized texture coordinates.
            /// </param>
            /// <param name="cameraParams">Describes the viewport the texture is to be displayed on.</param>
            /// <returns>
            /// <c>true</c> if the packed semantics texture descriptor is available and is returned. Otherwise, <c>false</c>.
            /// </returns>
            /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support packed semantics
            /// texture.</exception>
            public virtual bool TryGetPackedSemanticChannels(out XRTextureDescriptor packedSemanticsDescriptor, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
                => throw new NotSupportedException("Packed Semantic channels texture is not supported by this implementation");

            /// <summary>
            /// Acquire the latest packed semantic channels XRCpuImage.
            /// </summary>
            /// <param name="cpuImage">
            /// The resulting <see cref="XRCpuImage"/>. The XRCpuImage must be disposed by the caller.
            /// </param>
            /// <param name="samplerMatrix">
            /// The matrix that transforms from normalized viewport coordinates to normalized image coordinates.
            /// </param>
            /// <param name="cameraParams">Describes the viewport the image is to be displayed on.</param>
            /// <returns>
            /// Returns `true` if an <see cref="XRCpuImage"/> was successfully acquired. Returns `false` otherwise.
            /// </returns>
            public virtual bool TryAcquirePackedSemanticChannelsCpuImage(out XRCpuImage cpuImage, out Matrix4x4 samplerMatrix, XRCameraParams? cameraParams = null)
                => throw new NotSupportedException("Packed Semantic channels cpu image is not supported by this implementation");

            /// <summary>
            /// Get a semantic suppression texture descriptor.
            /// </summary>
            /// <param name="suppressionMaskDescriptor">
            /// The resulting semantic suppression mask texture descriptor, if available.
            /// </param>
            /// <param name="samplerMatrix">
            /// The matrix that transforms from normalized viewport coordinates to normalized texture coordinates.
            /// </param>
            /// <param name="cameraParams">Describes the viewport the texture is to be displayed on.</param>
            /// <returns>
            /// <c>true</c> if the suppression mask texture descriptor is available and is returned. Otherwise,
            /// <c>false</c>.
            /// </returns>
            /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support suppression mask texture.</exception>
            public virtual bool TryGetSuppressionMaskTexture(
                out XRTextureDescriptor suppressionMaskDescriptor,
                out Matrix4x4 samplerMatrix,
                XRCameraParams? cameraParams = null)
                => throw new NotSupportedException("Generating a suppression mask is not supported by this implementation");

            /// <summary>
            /// Acquire the latest suppression mask XRCpuImage.
            /// </summary>
            /// <param name="cpuImage">
            /// The resulting <see cref="XRCpuImage"/>. The XRCpuImage must be disposed by the caller.
            /// </param>
            /// <param name="samplerMatrix">
            /// The matrix that transforms from normalized viewport coordinates to normalized image coordinates.
            /// </param>
            /// <param name="cameraParams">Describes the viewport the image is to be displayed on.</param>
            /// <returns>
            /// Returns `true` if an <see cref="XRCpuImage"/> was successfully acquired. Returns `false` otherwise.
            /// </returns>
            public virtual bool TryAcquireSuppressionMaskCpuImage(
                out XRCpuImage cpuImage,
                out Matrix4x4 samplerMatrix,
                XRCameraParams? cameraParams = null)
                => throw new NotSupportedException("Generating a suppression mask is not supported by this implementation");

            /// <summary>
            /// Acquire the latest suppression mask XRCpuImage.
            /// </summary>
            /// <param name="cpuImage">
            /// The resulting <see cref="XRCpuImage"/>. The XRCpuImage must be disposed by the caller.
            /// </param>
            /// <param name="samplerMatrix">
            /// The matrix that transforms from normalized viewport coordinates to normalized image coordinates.
            /// </param>
            /// <param name="cameraParams">Describes the viewport the image is to be displayed on.</param>
            /// <param name="targetPose">
            /// Any image acquired has been captured in the past. The target pose argument defines the pose the image
            /// needs to synchronize with. If the image can be synchronized, the <see cref="samplerMatrix"/> will be
            /// calibrated to warp the image as if it was taken from the target pose. If this argument is null, the
            /// image will not be warped.
            /// </param>
            /// <returns>
            /// Returns `true` if an <see cref="XRCpuImage"/> was successfully acquired. Returns `false` otherwise.
            /// </returns>
            internal virtual bool TryAcquireSuppressionMaskCpuImage(
                out XRCpuImage cpuImage,
                out Matrix4x4 samplerMatrix,
                XRCameraParams? cameraParams,
                Matrix4x4? targetPose)
                => throw new NotSupportedException("Generating a suppression mask is not supported by this implementation");

            /// <summary>
            /// Property to be implemented by the provider to get or set the frame rate for the platform's semantic
            /// segmentation feature.
            /// </summary>
            /// <value>
            /// The requested frame rate in frames per second.
            /// </value>
            /// <exception cref="System.NotSupportedException">Thrown when requesting a frame rate that is not supported
            /// by the implementation.</exception>
            public virtual uint TargetFrameRate
            {
                get => 0;
                set
                {
                    throw new NotSupportedException("Setting semantic segmentation frame rate is not "
                        + "supported by this implementation");
                }
            }

            /// <summary>
            /// Property to be implemented by the provider to get or set the list of suppression channels for the platform's semantic
            /// segmentation feature.
            /// </summary>
            /// <value>
            /// The requested list of suppression channels
            /// </value>
            /// <exception cref="System.NotSupportedException">Thrown if the list of channels is not supported by this implementation.</exception>
            public virtual HashSet<string> SuppressionMaskChannels
            {
                get => new();
                set
                {
                    throw new NotSupportedException("Setting suppression mask channels is not "
                        + "supported by this implementation");
                }
            }

            public virtual uint? LatestFrameId
                => throw new NotSupportedException("Getting the latest frame id is not supported by this implementation");

            public virtual bool IsMetadataAvailable
                => throw new NotSupportedException("Getting if metadata is available is not supported by this implementation");

            /// <summary>
            /// Method to be implemented by the provider to get a list of the semantic channel names for the current
            /// semantic model.
            /// </summary>
            /// <param name="names">A list of semantic category labels. It will be empty if the method returns false.</param>
            /// <returns>True if channel names are available. False if not.</returns>
            /// <exception cref="System.NotSupportedException">Thrown when reading the channel names is not supported
            /// by the implementation.</exception>
            public virtual bool TryGetChannelNames(out IReadOnlyList<string> names)
                => throw new NotSupportedException("Getting semantic segmentation channel names is not "
                    + "supported by this implementation");

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
            /// returned by <c>TryGetChannelNames</c>.
            /// </param>
            /// <exception cref="System.NotSupportedException">Thrown when setting confidence thresholds is not
            /// supported by the implementation.</exception>
            /// <returns>True if the threshold was set. Otherwise, false.</returns>
            public virtual bool TrySetChannelConfidenceThresholds(Dictionary<string, float> channelConfidenceThresholds)
                => throw new NotSupportedException("Setting semantic channel confidence thresholds is not "
                    + "supported by this implementation");

            /// <summary>
            /// Resets the confidence thresholds for all semantic channels to the default values from the current model.
            /// </summary>
            /// <remarks>
            /// This reverts any changes made with <see cref="TrySetChannelConfidenceThresholds"/>.
            /// </remarks>
            /// <exception cref="System.NotSupportedException">Thrown when resetting confidence thresholds is not
            /// supported by the implementation.</exception>
            /// <returns>True if the thresholds were reset. Otherwise, false.</returns>
            public virtual bool TryResetChannelConfidenceThresholds()
                => throw new NotSupportedException("Resetting semantic channel confidence thresholds is not "
                    + "supported by this implementation");
        }
    }
}
