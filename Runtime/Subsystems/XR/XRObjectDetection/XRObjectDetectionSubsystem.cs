// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Subsystems.XR;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.SubsystemsImplementation;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Defines an interface for interacting with object detection functionality.
    /// </summary>
    /// <remarks>
    /// This is an experimental feature. Experimental features are subject to breaking changes,
    /// not officially supported, and may be deprecated without notice.
    /// </remarks>
    [PublicAPI]
    public class XRObjectDetectionSubsystem
        : SubsystemWithProvider<XRObjectDetectionSubsystem, XRObjectDetectionSubsystemDescriptor, XRObjectDetectionSubsystem.Provider>,
            ISubsystemWithModelMetadata
    {
        /// <summary>
        /// Specifies the target frame rate for the platform to target running the object detection algorithm at.
        /// </summary>
        /// <value>
        /// The target frame rate.
        /// </value>
        /// <exception cref="System.NotSupportedException">Thrown if frame rate configuration is not supported.
        /// </exception>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public uint TargetFrameRate
        {
            get => provider.TargetFrameRate;
            set => provider.TargetFrameRate = value;
        }

        /// <summary>
        /// Is true if metadata has been downloaded and decrypted on the current device. Only if this value
        /// is true can the object detection category names or results be acquired.
        /// </summary>
        /// <value>
        /// If metadata is available.
        /// </value>
        /// <exception cref="System.NotSupportedException">
        /// Thrown frame rate configuration is not supported.
        /// </exception>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public bool IsMetadataAvailable
        {
            get
            {
                return provider.IsMetadataAvailable;
            }
        }

        /// <summary>
        /// Returns the frame id of the most recent object detection output.
        /// </summary>
        /// <value>
        /// The frame id.
        /// </value>
        /// <exception cref="System.NotSupportedException">Thrown if getting frame id is not supported.
        /// </exception>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public uint? LatestFrameId
        {
            get
            {
                return provider.LatestFrameId;
            }
        }

        /// <summary>
        /// Tries to get a list of the object detection category names
        /// for the current model.
        /// </summary>
        /// <param name="names">A list of category names. It will be empty if the method returns false.</param>
        /// <returns>True if category names are available. False if not.</returns>
        /// <exception cref="System.NotSupportedException">Thrown when reading the category names is not supported
        /// by the implementation.</exception>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public bool TryGetCategoryNames(out IReadOnlyList<string> names)
        {
            return provider.TryGetCategoryNames(out names);
        }

        /// <summary>
        /// Tries to acquire the latest object detection results from the camera image.
        /// </summary>
        /// <param name="results">An array of object detection instances.</param>
        /// <returns>Whether any object detection instances could be retrieved.</returns>
        /// <exception cref="System.NotSupportedException"> Thrown if the implementation does not support getting
        /// object detection results. </exception>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public bool TryGetDetectedObjects(out XRDetectedObject[] results)
        {
            return provider.TryGetDetectedObjects(out results);
        }

        /// <summary>
        /// Construct the subsystem by creating the functionality provider.
        /// </summary>
        public XRObjectDetectionSubsystem() { }

        /// <summary>
        /// Register the descriptor for the object detection subsystem implementation.
        /// </summary>
        /// <param name="cinfo">The object detection subsystem implementation construction information.
        /// </param>
        /// <returns>
        /// <c>true</c> if the descriptor was registered. Otherwise, <c>false</c>.
        /// </returns>
        public static bool Register(XRObjectDetectionSubsystemCinfo cinfo)
        {
            var descriptor = XRObjectDetectionSubsystemDescriptor.Create(cinfo);
            SubsystemDescriptorStore.RegisterDescriptor(descriptor);
            return true;
        }

        /// <summary>
        /// The provider which will service the <see cref="XRObjectDetectionSubsystem"/>.
        /// </summary>
        public abstract class Provider : SubsystemProvider<XRObjectDetectionSubsystem>
        {
            /// <summary>
            /// Property to be implemented by the provider to get or set the frame rate for the platform's object
            /// detection feature.
            /// </summary>
            /// <value>
            /// The requested frame rate in frames per second.
            /// </value>
            /// <exception cref="System.NotSupportedException">Thrown when requesting frame rate is not supported
            /// by the implementation.</exception>
            /// <remarks>
            /// This is an experimental API. Experimental features are subject to breaking changes,
            /// not officially supported, and may be deprecated without notice.
            /// </remarks>
            public virtual uint TargetFrameRate
            {
                get => 0;
                set
                {
                    throw new NotSupportedException("Setting object detection frame rate is not "
                        + "supported by this implementation");
                }
            }

            /// <summary>
            /// Frame id of the most recent object detection output.
            /// </summary>
            /// <value>
            /// The frame id.
            /// </value>
            /// <exception cref="System.NotSupportedException">Thrown if getting frame id is not supported.
            /// </exception>
            /// <remarks>
            /// This is an experimental API. Experimental features are subject to breaking changes,
            /// not officially supported, and may be deprecated without notice.
            /// </remarks>
            public virtual uint? LatestFrameId
                => throw new NotSupportedException("Getting the latest frame id is not supported by this implementation");

            /// <summary>
            /// Is true if metadata has been downloaded and decrypted on the current device. Only if this value
            /// is true can the object detection category names or results be acquired.
            /// </summary>
            /// <value>
            /// If metadata is available.
            /// </value>
            /// <exception cref="System.NotSupportedException">
            /// Thrown frame rate configuration is not supported.
            /// </exception>
            /// <remarks>
            /// This is an experimental API. Experimental features are subject to breaking changes,
            /// not officially supported, and may be deprecated without notice.
            /// </remarks>
            public virtual bool IsMetadataAvailable
                => throw new NotSupportedException("Getting if metadata is available is not supported by this implementation");

            /// <summary>
            /// Method to be implemented by the provider to get a list of the object detection category names
            /// for the current model.
            /// </summary>
            /// <param name="names">A list of category labels. It will be empty if the method returns false.</param>
            /// <returns>True if channel names are available. False if not.</returns>
            /// <exception cref="System.NotSupportedException"> Thrown when reading the category names is
            /// not supported by the implementation. </exception>
            /// <remarks>
            /// This is an experimental API. Experimental features are subject to breaking changes,
            /// not officially supported, and may be deprecated without notice.
            /// </remarks>
            public virtual bool TryGetCategoryNames(out IReadOnlyList<string> names)
                => throw new NotSupportedException("Getting object detection category names is not "
                    + "supported by this implementation");

            /// <summary>
            /// Tries to acquire the latest object detection results from the camera image.
            /// </summary>
            /// <param name="results">An array of objects detected in the latest input camera image. If no objects were
            /// detected, this array will be empty.</param>
            /// <returns>True if the object detection neural network has produced output.</returns>
            /// <exception cref="System.NotSupportedException"> Thrown if the implementation does not support getting
            /// detected objects. </exception>
            /// <remarks>
            /// This is an experimental API. Experimental features are subject to breaking changes,
            /// not officially supported, and may be deprecated without notice.
            /// </remarks>
            public virtual bool TryGetDetectedObjects(out XRDetectedObject[] results)
                => throw new NotSupportedException("Getting object detections is not supported by this implementation.");
        }
    }
}
