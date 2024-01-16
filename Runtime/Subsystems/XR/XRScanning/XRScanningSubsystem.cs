// Copyright 2022-2024 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using UnityEngine.SubsystemsImplementation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Defines an interface for interacting with scanning functionality.
    /// </summary>
    /// <remarks>
    /// <para>This abstract class should be implemented by an XR provider and instantiated using the <c>SubsystemManager</c>
    /// to enumerate the available <see cref="XRScanningSubsystemDescriptor"/>s.</para>
    /// </remarks>
    [PublicAPI]
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
        ///  Get the latest raycast textures.
        /// </summary>
        public bool TryGetRaycastBuffer(out XRTextureDescriptor colorBufferDescriptor,
            out XRTextureDescriptor normalBufferDescriptor,
            out XRTextureDescriptor positionTextureDescriptor)
        {
            return provider.TryGetRaycastBuffer(out colorBufferDescriptor,
                out normalBufferDescriptor, out positionTextureDescriptor);
        }

        /// <summary>
        /// Request a voxel buffer to be computed. This is an async operation that takes some time. Obtain the result with
        /// <see cref="TryGetVoxels"/>. "enableVoxels" must be set to true on the <see cref="XRScanningConfiguration"/>
        /// </summary>
        public void ComputeVoxels()
        {
            provider.ComputeVoxels();
        }

        /// <summary>
        /// Get latest computed voxel buffer. This then must be later disposed with <see cref="DisposeVoxelBuffer"/>
        /// </summary>
        public bool TryGetVoxelBuffer(out XRScanningVoxelData voxelData)
        {
            return provider.TryGetVoxelBuffer(out voxelData);
        }

        /// <summary>
        /// Dispose a voxel buffer previously obtained from <see cref="TryGetVoxelBuffer"/>
        /// </summary>
        public void DisposeVoxelBuffer(XRScanningVoxelData voxelData)
        {
            provider.DisposeVoxelBuffer(voxelData);
        }

        /// <summary>
        /// Get the current scan's ID.
        /// </summary>
        public string GetScanId()
        {
            return provider.GetScanId();
        }

        /// <summary>
        /// Save the current scan. Recording will stop after save. ScanID will be reset.
        /// </summary>
        public void SaveCurrentScan()
        {
            provider.SaveCurrentScan();
        }

        /// <summary>
        /// Discards the current scan. Anything previously saved will be deleted. Recording will stop after discard.
        /// ScanID will be reset.
        /// </summary>
        public void DiscardCurrentScan()
        {
            provider.DiscardCurrentScan();
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

            public virtual bool TryGetRaycastBuffer(out XRTextureDescriptor raycastBufferDescriptor,
                out XRTextureDescriptor raycastNormalBufferDescriptor,
                out XRTextureDescriptor raycastPositionAndConfidenceDescriptor)
            {
                raycastBufferDescriptor = default;
                raycastNormalBufferDescriptor = default;
                raycastPositionAndConfidenceDescriptor = default;
                throw new NotImplementedException();
            }

            public virtual void SaveCurrentScan()
            {
                throw new NotImplementedException();
            }

            public virtual void DiscardCurrentScan()
            {
                throw new NotImplementedException();
            }

            public virtual void ComputeVoxels()
            {
                throw new NotImplementedException();
            }

            public virtual bool TryGetVoxelBuffer(out XRScanningVoxelData voxelData)
            {
                voxelData = default;
                throw new NotImplementedException();
            }

            public virtual void DisposeVoxelBuffer(XRScanningVoxelData voxelData)
            {
                throw new NotImplementedException();
            }

            public virtual XRScanningConfiguration CurrentConfiguration { get; set; }
        }
    }
}
