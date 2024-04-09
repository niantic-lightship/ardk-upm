// Copyright 2022-2024 Niantic.
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
        /// <param name="linearColorSpace">Whether to sample the target texture in linear color space.</param>
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
            catch (Exception)
            {
                Error($"Could not retrieve image plane: {source}.");
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
    }
}
