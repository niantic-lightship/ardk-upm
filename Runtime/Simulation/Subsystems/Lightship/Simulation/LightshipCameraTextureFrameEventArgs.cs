// Copyright 2022-2024 Niantic.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Simulation
{
    /// <summary>
    /// Based on Unity Simulation's CameraTextureFrameEventArgs.
    /// A structure for camera texture related information pertaining to a particular frame.
    /// </summary>
    internal struct LightshipCameraTextureFrameEventArgs
    {
        /// <summary>
        /// The time, in nanoseconds, associated with this frame.
        /// Use <c>timestampNs.HasValue</c> to determine if this data is available.
        /// </summary>
        public long? timestampNs { get; set; }

        /// <summary>
        /// Gets or sets the projection matrix for the AR Camera. Use
        /// <c>projectionMatrix.HasValue</c> to determine if this data is available.
        /// </summary>
        public Matrix4x4? projectionMatrix { get; set; }

        /// <summary>
        /// Gets or sets the display matrix for the simulation camera. Use
        /// <c>displayMatrix.HasValue</c> to determine if this data is available.
        /// </summary>
        public Matrix4x4? displayMatrix { get; set; }

        /// <summary>
        /// Gets or sets the intrinsics of the simulation camera.
        /// </summary>
        public XRCameraIntrinsics intrinsics { get; set; }

        /// <summary>
        /// The textures associated with this camera frame. These are generally
        /// external textures, which exist only on the GPU. To use them on the
        /// CPU, e.g., for computer vision processing, you will need to read
        /// them back from the GPU.
        /// </summary>
        public List<Texture2D> textures { get; set; }
    }
}
