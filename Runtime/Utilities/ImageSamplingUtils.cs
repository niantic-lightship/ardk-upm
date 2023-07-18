using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    public static class ImageSamplingUtils
    {
        /// Samples a native array as if it was an image. Employs nearest neighbour algorithm.
        /// @param data The array containing the data.
        /// @param width The width of the image.
        /// @param height The height of the image.
        /// @param uv Normalized image coordinates to sample.
        /// @returns The nearest value in the array to the normalized coordinates.
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

        /// Samples a native array as if it was an image. Employs nearest neighbour algorithm.
        /// @param data The array containing the data.
        /// @param width The width of the image.
        /// @param height The height of the image.
        /// @param uv Normalized image coordinates to sample.
        /// @param transform Transforms the uv coordinates before sampling.
        /// @returns The nearest value in the array to the transformed UV coordinates.
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

        /// Samples a CPU image. Employs nearest neighbour algorithm.
        /// @param image The image to sample from.
        /// @param uv Normalized image coordinates to sample.
        /// @param transform Transforms the uv coordinates before sampling.
        /// @returns The nearest value in the image to the transformed UV coordinates.
        public static T Sample<T>(this XRCpuImage image, Vector2 uv, Matrix4x4 transform, int plane = 0)
            where T : struct
        {
            XRCpuImage.Plane imagePlane;
            try
            {
                imagePlane = image.GetPlane(plane);
            }
            catch (Exception e)
            {
                Debug.LogError("Could not retrieve image plane " + plane + " during sampling.");
                throw;
            }

            var data = imagePlane.data.Reinterpret<T>(UnsafeUtility.SizeOf<byte>());
            return data.Sample(image.width, image.height, uv, transform);
        }
    }
}
