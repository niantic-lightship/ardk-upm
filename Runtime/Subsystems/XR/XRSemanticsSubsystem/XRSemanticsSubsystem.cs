using System;
using System.Collections.Generic;
using UnityEngine.SubsystemsImplementation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// Defines an interface for interacting with semantic segmentation functionality.
    /// </summary>
    public class XRSemanticsSubsystem
        : SubsystemWithProvider<XRSemanticsSubsystem, XRSemanticsSubsystemDescriptor, XRSemanticsSubsystem.Provider>
    {

        /// <summary>
        /// Construct the subsystem by creating the functionality provider.
        /// </summary>
        public XRSemanticsSubsystem() { }

        /// <summary>
        /// Gets a semantics channel texture descriptor.
        /// </summary>
        /// <param name="channelName">The string description of the semantics channel that is needed.</param>
        /// <param name="semanticsChannelDescriptor">The semantics channel texture descriptor to be populated, if
        /// available from the provider.</param>
        /// <returns>
        /// <c>true</c> if the semantics channel texture descriptor is available and is returned. Otherwise,
        /// <c>false</c>.
        /// </returns>
        /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support semantics channel
        /// texture.</exception>
        public bool TryGetSemanticChannel(string channelName, out XRTextureDescriptor semanticsChannelDescriptor)
            => provider.TryGetSemanticChannel(channelName, out semanticsChannelDescriptor);

        /// <summary>
        /// Tries to acquire the latest semantics channel CPU image.
        /// </summary>
        /// <param name="channelName">The string description of the semantics channel that is needed.</param>
        /// <param name="cpuBuffer">If this method returns `true`, an acquired <see cref="LightshipCpuBuffer"/>. The CPU buffer
        /// must be disposed by the caller.</param>
        /// <returns>Returns `true` if an <see cref="LightshipCpuBuffer"/> was successfully acquired.
        /// Returns `false` otherwise.</returns>
        public bool TryAcquireSemanticChannelCPUImage(string channelName, out LightshipCpuBuffer cpuBuffer)
        {
            if (provider.TryAcquireSemanticChannelCPUImage(channelName, out cpuBuffer))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Once a <see cref="LightshipCpuBuffer"/> is acquired by using one of the other methods, you have to dispose
        /// of it once you are done with it as it holds on to a native resource (the memory for the buffer).
        /// </summary>
        /// <param name="cpuBuffer"> The <see cref="LightshipCpuBuffer"/> you want to dispose of </param>
        public void DisposeCPUImage(LightshipCpuBuffer cpuBuffer)
        {
            provider.DisposeCPUImage(cpuBuffer);
        }

        /// <summary>
        /// Gets a packed semantics texture descriptor.
        /// </summary>
        /// <param name="packedSemanticsDescriptor">The packed semantics texture descriptor to be populated, if
        /// available from the provider.</param>
        /// <returns>
        /// <c>true</c> if the packed semantics texture descriptor is available and is returned. Otherwise,
        /// <c>false</c>.
        /// </returns>
        /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support packed semantics
        /// texture.</exception>
        public bool TryGetPackedSemanticChannels(out XRTextureDescriptor packedSemanticsDescriptor)
            => provider.TryGetPackedSemanticChannels( out packedSemanticsDescriptor);

        /// <summary>
        ///  Tries to acquire the latest packed semantic channels CPU image.
        /// </summary>
        /// <param name="cpuBuffer">If this method returns `true`, an acquired <see cref="LightshipCpuBuffer"/>. The CPU buffer
        /// must be disposed by the caller.</param>
        /// <returns></returns>
        public bool TryAcquirePackedSemanticChannelsCPUImage(out LightshipCpuBuffer cpuBuffer)
        {
            if (provider.TryAcquirePackedSemanticChannelsCPUImage(out cpuBuffer))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

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
        /// Specifies the frame rate for the platform to run semantic segmentation at.
        /// </summary>
        /// <value>
        /// The requested frame rate.
        /// </value>
        /// <exception cref="System.NotSupportedException">Thrown if the requested frame rate is not supported.
        /// </exception>
        public uint FrameRate
        {
            get => provider.FrameRate;
            set => provider.FrameRate = value;
        }

        /// <summary>
        /// Get a list of the semantic channel names for the current semantic model.
        /// </summary>
        /// <returns>
        /// A list of semantic category labels.
        /// </returns>
        /// <exception cref="System.NotSupportedException">Thrown when reading the channel names is not supported
        /// by the implementation.</exception>
        public List<string> GetChannelNames()
        {
            return provider.GetChannelNames();
        }

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
        /// returned by <c>GetChannelNames</c>.
        /// </param>
        /// <exception cref="System.NotSupportedException">Thrown when setting confidence thresholds is not
        /// supported by the implementation.</exception>
        /// <returns>True if the threshold was set. Otherwise, false.</returns>
        public bool TrySetChannelConfidenceThresholds(Dictionary<string,float> channelConfidenceThresholds)
        {
            return provider.TrySetChannelConfidenceThresholds(channelConfidenceThresholds);
        }

        /// <summary>
        /// The provider which will service the <see cref="XRSemanticsSubsystem"/>.
        /// </summary>
        public abstract class Provider : SubsystemProvider<XRSemanticsSubsystem>
        {
            /// <summary>
            /// Method to be implemented by the provider to get the semantic channel texture descriptor.
            /// </summary>
            /// <param name="channelName">The string description of the semantics channel that is needed.</param>
            /// <param name="semanticChannelDescriptor">The semantic channel texture descriptor to be populated, if
            /// available.</param>
            /// <returns>
            /// <c>true</c> if the semantic channel texture descriptor is available and is returned. Otherwise,
            /// <c>false</c>.
            /// </returns>
            /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support semantic
            /// channel texture.</exception>
            public virtual bool TryGetSemanticChannel(string channelName, out XRTextureDescriptor semanticChannelDescriptor)
                => throw new NotSupportedException("Semantic channel texture is not supported by this implementation");

            /// <summary>
            /// Tries to acquire the latest semantic channel CPU image.
            /// </summary>
            /// <param name="cpuBuffer">If this method returns `true`, this should be populated with construction
            /// information for an <see cref="LightshipCpuBuffer"/>.</param>
            /// <returns>Returns `true` if the semantic channel CPU image was acquired.
            /// Returns `false` otherwise.</returns>
            /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support semantic channels
            /// CPU images.</exception>
            public virtual bool TryAcquireSemanticChannelCPUImage(string channelName, out LightshipCpuBuffer cpuBuffer)
                => throw new NotSupportedException("Semantic channel CPU images are not supported by this implementation.");

            /// <summary>
            /// Once a <see cref="LightshipCpuBuffer"/> is acquired by using one of the other methods, you have to dispose
            /// of it once you are done with it as it holds on to a native resource (the memory for the buffer).
            /// </summary>
            /// <param name="cpuBuffer"> The <see cref="LightshipCpuBuffer"/> you want to dispose</param>
            /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support disposing
            /// CPU buffers.</exception>
            public virtual void DisposeCPUImage(LightshipCpuBuffer cpuBuffer)
                => throw new NotSupportedException("Semantic channel CPU images are not supported by this implementation");

            /// <summary>
            /// Gets a packed semantics texture descriptor.
            /// </summary>
            /// <param name="packedSemanticsDescriptor">The packed semantics texture descriptor to be populated, if
            /// available from the provider.</param>
            /// <returns>
            /// <c>true</c> if the packed semantics texture descriptor is available and is returned. Otherwise,
            /// <c>false</c>.
            /// </returns>
            /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support packed semantics
            /// texture.</exception>
            public virtual bool TryGetPackedSemanticChannels(out XRTextureDescriptor packedSemanticsDescriptor)
                => throw new NotSupportedException("Packed Semantic channels texture is not supported by this implementation");

            /// <summary>
            ///  Tries to acquire the latest packed semantic channels CPU image.
            /// </summary>
            /// <param name="cpuBuffer">If this method returns `true`, an acquired <see cref="LightshipCpuBuffer"/>. The CPU buffer
            /// must be disposed by the caller.</param>
            /// <returns></returns>
            public virtual bool TryAcquirePackedSemanticChannelsCPUImage(out LightshipCpuBuffer cpuBuffer)
                => throw new NotSupportedException("Packed Semantic channels cpu buffer is not supported by this implementation");

            /// <summary>
            /// Property to be implemented by the provider to get or set the frame rate for the platform's semantic
            /// segmentation feature.
            /// </summary>
            /// <value>
            /// The requested frame rate in frames per second.
            /// </value>
            /// <exception cref="System.NotSupportedException">Thrown when requesting a frame rate that is not supported
            /// by the implementation.</exception>
            public virtual uint FrameRate
            {
                get => 0;
                set
                {
                    throw new NotSupportedException("Setting semantic segmentation frame rate is not "
                        + "supported by this implementation");
                }
            }

            /// <summary>
            /// Property to be implemented by the provider to get a list of the semantic channel names for the current
            /// semantic model.
            /// </summary>
            /// <value>
            /// A list of semantic category labels.
            /// </value>
            /// <exception cref="System.NotSupportedException">Thrown when reading the channel names is not supported
            /// by the implementation.</exception>
            public virtual List<string> GetChannelNames()
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
            /// returned by <c>GetChannelNames</c>.
            /// </param>
            /// <exception cref="System.NotSupportedException">Thrown when setting confidence thresholds is not
            /// supported by the implementation.</exception>
            /// <returns>True if the threshold was set. Otherwise, false.</returns>
            public virtual bool TrySetChannelConfidenceThresholds(Dictionary<string,float> channelConfidenceThresholds)
                => throw new NotSupportedException("Setting semantic channel confidence thresholds is not "
                    + "supported by this implementation");
        }
    }
}
