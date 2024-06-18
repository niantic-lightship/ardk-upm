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
    internal readonly struct LightshipCameraTextureFrameEventArgs
    {
        public LightshipCameraTextureFrameEventArgs(long timestampNs, Matrix4x4 projectionMatrix, Matrix4x4 displayMatrix, XRCameraIntrinsics intrinsics, Texture2D texture)
        {
            TimestampNs = timestampNs;
            ProjectionMatrix = projectionMatrix;
            DisplayMatrix = displayMatrix;
            Intrinsics = intrinsics;
            Texture = texture;
        }

        /// <summary>
        /// The time, in nanoseconds, associated with this frame.
        /// Use <c>timestampNs.HasValue</c> to determine if this data is available.
        /// </summary>
        public long TimestampNs { get; }

        /// <summary>
        /// Gets or sets the projection matrix for the AR Camera. Use
        /// <c>projectionMatrix.HasValue</c> to determine if this data is available.
        /// </summary>
        public Matrix4x4 ProjectionMatrix { get; }

        /// <summary>
        /// Gets or sets the display matrix for the simulation camera. Use
        /// <c>displayMatrix.HasValue</c> to determine if this data is available.
        /// </summary>
        public Matrix4x4 DisplayMatrix { get; }

        /// <summary>
        /// Gets or sets the intrinsics of the simulation camera.
        /// </summary>
        public XRCameraIntrinsics Intrinsics { get; }

        /// <summary>
        /// The textures associated with this camera frame. These are generally
        /// external textures, which exist only on the GPU. To use them on the
        /// CPU, e.g., for computer vision processing, you will need to read
        /// them back from the GPU.
        /// </summary>
        public Texture2D Texture { get; }
    }
}
