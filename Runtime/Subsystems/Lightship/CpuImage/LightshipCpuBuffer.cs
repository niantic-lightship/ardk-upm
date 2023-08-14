// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

        public bool valid => buffer != IntPtr.Zero && nativeHandle != IntPtr.Zero;

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

        /// <summary>
        /// Copies the contents of this CPU image to the specified texture.
        /// Destroying the texture is the responsibility of the caller.
        /// If the provided texture has been pre-allocated with mismatching
        /// attributes, it will be recreated to match the CPU image properties.
        /// </summary>
        /// <param name="texture">The texture to copy the buffer's data to.</param>
        /// <param name="apply">Whether to upload the data to GPU memory.</param>
        public void CopyToTexture(ref Texture2D texture, bool apply = true)
        {
            var allocate = false;
            if (texture != null)
            {
                if (texture.width != dimensions.x || texture.height != dimensions.y ||
                    texture.format != GetTextureFormat())
                {
                    UnityEngine.Object.Destroy(texture);
                    allocate = true;
                }
            }
            else
            {
                allocate = true;
            }

            if (allocate)
            {
                texture = new Texture2D(width, height, GetTextureFormat(), false, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0
                };
            }

            unsafe
            {
                // Access the data through a native array
                var nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(
                    (void*)buffer, width * height * GetPixelStride(), Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, AtomicSafetyHandle.GetTempMemoryHandle());
#endif

                // Copy data
                texture.SetPixelData(nativeArray, 0);
                nativeArray.Dispose();

                // Push to GPU
                if (apply)
                {
                    texture.Apply();
                }
            }
        }

        /// <summary>
        /// Returns the texture format appropriate for representing the contents of this buffer.
        /// </summary>
        private TextureFormat GetTextureFormat()
        {
            switch (format)
            {
                case Format.Unknown:
                    return TextureFormat.ARGB32;
                case Format.BitMask32:
                case Format.DepthFloat32:
                    return TextureFormat.RFloat;
                default:
                    throw new ArgumentOutOfRangeException($"format", "Unhandled CPU image format.");
            }
        }

        /// <summary>
        /// Returns the size of an individual pixel in bytes, based on the buffer's image format.
        /// </summary>
        private int GetPixelStride()
        {
            switch (format)
            {
                case Format.Unknown:
                    return 1;
                case Format.BitMask32:
                case Format.DepthFloat32:
                    return 4;
                default:
                    throw new ArgumentOutOfRangeException($"format", "Unhandled CPU image format.");
            }
        }
    }
}
