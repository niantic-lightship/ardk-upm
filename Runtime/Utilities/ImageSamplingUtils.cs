// Copyright 2022-2025 Niantic.
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static Niantic.Lightship.AR.Utilities.Logging.Log;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    public static class ImageSamplingUtils
    {
        /// <summary>
        /// Samples a native array as if it was an image. Employs nearest neighbour algorithm.
        /// </summary>
        /// <param name="data">The native array containing the data.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="uv">Normalized image coordinates to sample.</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>The nearest value in the array to the normalized coordinates.</returns>
        public static T Sample<T>(this NativeArray<T> data, int width, int height, Vector2 uv)
            where T : struct
        {
            var st = new Vector4(uv.x, uv.y, 1.0f, 1.0f);
            var sx = st.x / st.z;
            var sy = st.y / st.z;

            var x = Mathf.Clamp(Mathf.RoundToInt(sx * width - 0.5f), 0, width - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(sy * height - 0.5f), 0, height - 1);

            return data[x + width * y];
        }

        /// <summary>
        /// Samples a native array as if it was an image. Employs nearest neighbour algorithm.
        /// </summary>
        /// <param name="data">The native array containing the data.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="uv">Normalized image coordinates to sample.</param>
        /// <param name="transform">Transforms the uv coordinates before sampling.</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>The nearest value in the array to the transformed UV coordinates.</returns>
        public static T Sample<T>(this NativeArray<T> data, int width, int height, Vector2 uv, Matrix4x4 transform)
            where T : struct
        {
            var st = transform * new Vector4(uv.x, uv.y, 1.0f, 1.0f);
            var sx = st.x / st.z;
            var sy = st.y / st.z;

            var x = Mathf.Clamp(Mathf.RoundToInt(sx * width - 0.5f), 0, width - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(sy * height - 0.5f), 0, height - 1);

            return data[x + width * y];
        }

        /// <summary>
        /// Samples a CPU image. Employs nearest neighbour algorithm.
        /// </summary>
        /// <param name="image">The image to sample from.</param>
        /// <param name="uv">Normalized image coordinates to sample.</param>
        /// <param name="transform">Transforms the uv coordinates before sampling.</param>
        /// <param name="plane">The index of the image plane to access.</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>The nearest value in the image to the transformed UV coordinates.</returns>
        public static T Sample<T>(this XRCpuImage image, Vector2 uv, Matrix4x4 transform, int plane = 0)
            where T : struct
        {
            XRCpuImage.Plane imagePlane;
            try
            {
                imagePlane = image.GetPlane(plane);
            }
            catch (Exception)
            {
                Error($"Could not retrieve image plane: {plane} during sampling.");
                throw;
            }

            var data = imagePlane.data.Reinterpret<T>(UnsafeUtility.SizeOf<byte>());
            return data.Sample(image.width, image.height, uv, transform);
        }

        /// <summary>
        /// Transforms pixel coordinates using the specified matrix.
        /// </summary>
        /// <param name="coordinates">The pixel coordinates to transform.</param>
        /// <param name="sourceContainer">The resolution of the container the coordinates are interpreted in.</param>
        /// <param name="targetContainer">The resolution of the container the resulting coordinates are interpreted in.</param>
        /// <param name="transform">The transformation matrix.</param>
        /// <param name="clampToContainer">Whether to clamp the resulting coordinates to the bounds of the target container.</param>
        /// <returns>The transformed pixel coordinates.</returns>
        public static Vector2Int TransformImageCoordinates
        (
            Vector2Int coordinates,
            Vector2Int sourceContainer,
            Vector2Int targetContainer,
            Matrix4x4 transform,
            bool clampToContainer = false
        )
        {
            var uv =
                new Vector4
                (
                    coordinates.x / (float)sourceContainer.x,
                    coordinates.y / (float)sourceContainer.y,
                    1.0f,
                    1.0f
                );

            var st = transform * uv;
            var sx = st.x / st.z;
            var sy = st.y / st.z;

            var x = clampToContainer
                ? Mathf.Clamp(Mathf.RoundToInt(sx * targetContainer.x), 0, targetContainer.x)
                : Mathf.RoundToInt(sx * targetContainer.x);

            var y = clampToContainer
                ? Mathf.Clamp(Mathf.RoundToInt(sy * targetContainer.y), 0, targetContainer.y)
                : Mathf.RoundToInt(sy * targetContainer.y);

            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Acquires the data buffer of the specified XRCpuImage.
        /// </summary>
        /// <param name="image">The image to access data of.</param>
        /// <param name="plane">The image plane index.</param>
        /// <typeparam name="T">The type of the data buffer.</typeparam>
        /// <returns>The image data.</returns>
        internal static NativeArray<T>? AcquireDataBuffer<T>(this XRCpuImage image, int plane = 0) where T: struct
        {
            if (!image.valid)
            {
                return null;
            }

            // Access the image plane
            XRCpuImage.Plane imagePlane;
            try
            {
                imagePlane = image.GetPlane(plane);
            }
            catch (Exception)
            {
                Error($"Could not retrieve image plane: {image} during sampling.");
                return null;
            }

            // Acquire image data
            return imagePlane.data.Reinterpret<T>(UnsafeUtility.SizeOf<byte>());
        }

        /// <summary>
        /// Copies the contents of an XRCpuImage to the destination texture.
        /// If the destination texture does not exist, it will be created.
        /// Releasing the destination texture is the responsibility of the caller.
        /// </summary>
        /// <param name="source">The source XRCpuImage.</param>
        /// <param name="sourcePlane">The index of the image plane to copy.</param>
        /// <param name="destination">The target texture to copy to.</param>
        /// <param name="destinationFilter">The sampling method of the target texture.</param>
        /// <param name="linearColorSpace">Whether to sample the target texture is in linear color space.</param>
        /// <param name="pushToGpu">Whether to upload the texture to gpu when done.</param>
        /// <returns>Whether the target texture was successfully updated.</returns>
        internal static bool CreateOrUpdateTexture(
            this XRCpuImage source,
            ref Texture2D destination,
            int sourcePlane = 0,
            FilterMode destinationFilter = FilterMode.Bilinear,
            bool linearColorSpace = false,
            bool pushToGpu = true
        )
        {
            if (!source.valid)
            {
                return false;
            }

            // Access the image data
            XRCpuImage.Plane imagePlane;
            try
            {
                imagePlane = source.GetPlane(sourcePlane);
            }
            catch (Exception ex)
            {
                Error($"Failed to get plane {sourcePlane}: {ex.Message}");
                return false;
            }

            // Infer the destination format
            var textureFormat = source.format.AsTextureFormat();

            // Check whether the destination container needs to be created
            if (destination == null || destination.width != source.width || destination.height != source.height ||
                destination.format != textureFormat)
            {
                if (destination != null)
                {
                    // Release the previously allocated texture
                    UnityEngine.Object.Destroy(destination);
                }

                // Allocate the target texture
                destination = new Texture2D(source.width, source.height, textureFormat, false, linearColorSpace)
                {
                    wrapMode = TextureWrapMode.Clamp, anisoLevel = 0
                };
            }

            if (destination.filterMode != destinationFilter)
            {
                destination.filterMode = destinationFilter;
            }

            // Copy to texture
            destination.GetPixelData<byte>(mipLevel: 0).CopyFrom(imagePlane.data);

            // Upload to gpu memory
            if (pushToGpu)
            {
                destination.Apply(false, false);
            }

            // Success
            return true;
        }

        /// <summary>
        /// Copies the contents of all planes of an XRCpuImage into a corresponding array of textures.
        /// If any destination texture is missing or mismatched, it will be created.
        /// Releasing the textures is the responsibility of the caller.
        /// </summary>
        /// <param name="source">The source XRCpuImage.</param>
        /// <param name="destTextures">The target textures array, one per image plane. Will be updated or resized as needed.</param>
        /// <param name="destinationFilter">Filter mode for the created textures.</param>
        /// <param name="linearColorSpace">Whether the textures are in linear color space.</param>
        /// <param name="pushToGpu">Whether to upload the textures to GPU after writing.</param>
        /// <returns>True if textures were successfully updated, false otherwise.</returns>
        internal static bool CreateOrUpdateTextures(
            this XRCpuImage source,
            ref Texture2D[] destTextures,
            FilterMode destinationFilter = FilterMode.Bilinear,
            bool linearColorSpace = false,
            bool pushToGpu = true)
        {
            // Check if the source image is valid
            if (!source.valid)
            {
                Error("XRCpuImage is not valid.");
                return false;
            }

            // Get the number of planes
            var planeCount = source.planeCount;

            // Check if the format is supported
            if (planeCount > 1 && source.format != XRCpuImage.Format.AndroidYuv420_888 &&
                source.format != XRCpuImage.Format.IosYpCbCr420_8BiPlanarFullRange)
            {
                Error($"XRCpuImage format {source.format} is not supported for this method.");
                return false;
            }

            // Resize or allocate the texture array if needed
            if (destTextures == null || destTextures.Length != planeCount)
            {
                if (destTextures != null)
                {
                    for (int i = 0; i < destTextures.Length; i++)
                    {
                        if (destTextures[i] != null)
                        {
                            UnityEngine.Object.Destroy(destTextures[i]);
                            destTextures[i] = null;
                        }
                    }
                }

                destTextures = new Texture2D[planeCount];
            }

            for (int planeIdx = 0; planeIdx < planeCount; planeIdx++)
            {
                XRCpuImage.Plane plane;
                try
                {
                    plane = source.GetPlane(planeIdx);
                }
                catch (Exception ex)
                {
                    Error($"Failed to get plane {planeIdx}: {ex.Message}");
                    return false;
                }

                // Infer the texture format per plane
                var textureFormat = source.GetTextureFormatForPlane(planeIdx);
                if (!textureFormat.HasValue)
                {
                    Error($"Failed to get texture format for plane {planeIdx}.");
                    return false;
                }

                // Infer plane dimensions
                int width = source.width >> (planeIdx > 0 ? 1 : 0);
                int height = source.height >> (planeIdx > 0 ? 1 : 0);

                // Create or recreate texture if needed
                if (destTextures[planeIdx] == null ||
                    destTextures[planeIdx].width != width ||
                    destTextures[planeIdx].height != height ||
                    destTextures[planeIdx].format != textureFormat)
                {
                    if (destTextures[planeIdx] != null)
                    {
                        UnityEngine.Object.Destroy(destTextures[planeIdx]);
                    }

                    destTextures[planeIdx] = new Texture2D(width, height, textureFormat.Value, false, linearColorSpace)
                    {
                        wrapMode = TextureWrapMode.Clamp, anisoLevel = 0
                    };
                }

                // Update filter mode if changed
                if (destTextures[planeIdx].filterMode != destinationFilter)
                {
                    destTextures[planeIdx].filterMode = destinationFilter;
                }

                // Copy data from plane
                destTextures[planeIdx].GetPixelData<byte>(0).CopyFrom(plane.data);

                if (pushToGpu)
                {
                    destTextures[planeIdx].Apply(false, false);
                }
            }

            return true;
        }

        /// <summary>
        /// Infers the texture format for a given image plane based on the XRCpuImage format.
        /// </summary>
        /// <param name="source">The source XRCpuImage.</param>
        /// <param name="planeIndex">The index of the image plane.</param>
        /// <returns>The inferred texture format, or null if the format cannot be determined.</returns>
        private static TextureFormat? GetTextureFormatForPlane(this XRCpuImage source, int planeIndex)
        {
            if (!source.valid || planeIndex >= source.planeCount)
            {
                return null;
            }

            // Android YUV_420_888 has 3 planes: Y (R8), U/V (RG16 or R8)
            // iOS BiPlanar: Y (R8), CbCr (RG16)
            var format = source.format;
            switch (format)
            {
                case XRCpuImage.Format.IosYpCbCr420_8BiPlanarFullRange:
                    return planeIndex == 0
                        ? TextureFormat.R8 // Luma
                        : TextureFormat.RG16; // Chroma (interleaved)

                case XRCpuImage.Format.AndroidYuv420_888:
                    return planeIndex == 0
                        ? TextureFormat.R8 // Luma
                        : source.planeCount == 2
                            ? TextureFormat.RG16 // Chroma (interleaved)
                            : TextureFormat.R8;  // Chroma (separate planes)
                default:
                    return format.AsTextureFormat();
            }
        }
    }
}
