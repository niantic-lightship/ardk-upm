// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using UnityEngine.SubsystemsImplementation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// Base class for a scanning subsystem.
    /// </summary>
    /// <remarks>
    /// <para>This abstract class should be implemented by an XR provider and instantiated using the <c>SubsystemManager</c>
    /// to enumerate the available <see cref="XRScanningSubsystemDescriptor"/>s.</para>
    /// </remarks>
    public class XRScanningSubsystem
        : SubsystemWithProvider<XRScanningSubsystem, XRScanningSubsystemDescriptor, XRScanningSubsystem.Provider>
    {
        /// <summary>
        /// Constructor. Do not invoke directly; use the <c>SubsystemManager</c>
        /// to enumerate the available <see cref="XRScanningSubsystemDescriptor"/>s
        /// and call <c>Create</c> on the desired descriptor.
        /// </summary>
        public XRScanningSubsystem()
        {
        }

        /// <summary>
        /// Get the current state of the scanning subsystem.
        /// </summary>
        public XRScanningState GetState()
        {
            return provider.GetState();
        }

        /// <summary>
        ///  Get the latest raycast image.
        /// </summary>
        public bool TryGetRaycastBuffer(out XRTextureDescriptor raycastBufferDescriptor)
        {
            return provider.TryGetRaycastBuffer(out raycastBufferDescriptor);
        }

        /// <summary>
        /// Get the current scan's ID.
        /// </summary>
        public string GetScanId()
        {
            return provider.GetScanId();
        }

        /// <summary>
        /// Get or set configuration with <paramref>
        ///     <name>XRScanningConfiguration</name>
        /// </paramref>
        /// </summary>
        public XRScanningConfiguration CurrentConfiguration
        {
            get => provider.CurrentConfiguration;
            set => provider.CurrentConfiguration = value;
        }

        /// <summary>
        /// An abstract class to be implemented by providers of this subsystem.
        /// </summary>
        public abstract class Provider : SubsystemProvider<XRScanningSubsystem>
        {
            private XRScanningConfiguration _currentConfiguration;

            /// <summary>
            /// Get the current scan's ID.
            /// </summary>
            public virtual string GetScanId()
            {
                throw new NotImplementedException();
            }

            public virtual XRScanningState GetState()
            {
                throw new NotImplementedException();
            }

            public virtual bool TryGetRaycastBuffer(out XRTextureDescriptor raycastBufferDescriptor)
            {
                raycastBufferDescriptor = default;
                throw new NotImplementedException();
            }

            public virtual XRScanningConfiguration CurrentConfiguration { get; set; }
        }
    }
}
