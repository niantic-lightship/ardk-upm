// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using UnityEngine;

namespace Niantic.Lightship.AR
{
    public struct LightshipCpuBuffer : IDisposable, IEquatable<LightshipCpuBuffer>
    {
        public enum Format
        {
            /// <summary>
            /// The format is unknown or could not be determined.
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// A bitmask image format with 32 bits per pixel.
            /// </summary>
            BitMask32 = 1,

            /// <summary>
            /// IEEE754-2008 binary32 float, for example 0.0f-1.0f to describe the confidence value of a semantic pixel
            /// </summary>
            DepthFloat32 = 2,

        }

        /// <summary>
        /// The dimensions (width and height) of the image.
        /// </summary>
        /// <value>
        /// The dimensions (width and height) of the image.
        /// </value>
        public Vector2Int dimensions { get; private set; }

        /// <summary>
        /// The image width.
        /// </summary>
        /// <value>
        /// The image width.
        /// </value>
        public int width => dimensions.x;

        /// <summary>
        /// The image height.
        /// </summary>
        /// <value>
        /// The image height.
        /// </value>
        public int height => dimensions.y;

        /// <summary>
        /// The format used by the image. This will allow you to determine the bits per pixel and format.
        /// </summary>
        /// <value>
        /// The format used by the image.
        /// </value>
        public Format format { get; private set; }

        public IntPtr buffer { get; private set; }

        public IntPtr nativeHandle { get; private set; }

        public LightshipCpuBuffer(IntPtr nativeHandle,  IntPtr buffer, Vector2Int dimensions, Format format)
        {
            this.dimensions = dimensions;
            this.format = format;
            this.buffer = buffer;
            this.nativeHandle = nativeHandle;
        }

        public void Dispose()
        {
            this.buffer = IntPtr.Zero;
            this.nativeHandle = IntPtr.Zero;
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="LightshipCpuBuffer"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="LightshipCpuBuffer"/>, otherwise false.</returns>
        public bool Equals(LightshipCpuBuffer other) =>
            dimensions.Equals(other.dimensions) &&
            (format == other.format) &&
            (nativeHandle == other.nativeHandle);
    }


}
