using Niantic.Lightship.AR.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Occlusion
{
    internal static class OcclusionExtensionUtils
    {
        /// <summary>
        /// Sparsely samples the specified subregion for the closest depth value.
        /// </summary>
        public static float SampleImageSubregion
        (
            XRCpuImage cpuImage,
            Matrix4x4 imageTransform,
            Rect region
        )
        {
            var imageData = cpuImage.GetPlane(0).data;
            using var nativeArray = imageData.Reinterpret<float>(UnsafeUtility.SizeOf<byte>());

            // Inspect the image
            var width = cpuImage.width;
            var height = cpuImage.height;
            var depth = 100.0f;

            // Helpers
            const int numSamples = 5;
            var position = region.position;
            var center = region.center;
            var stepX = region.width * (1.0f / numSamples);
            var stepY = region.height * (1.0f / numSamples);

            // Sample
            var uv = new Vector2();
            for (int i = 0; i <= numSamples; i++)
            {
                // Sample horizontally
                uv.x = position.x + i * stepX;
                uv.y = center.y;

                var horizontal = ImageSamplingUtils.Sample(nativeArray, width, height, uv, imageTransform);
                if (horizontal < depth)
                {
                    depth = horizontal;
                }

                // Sample vertically
                uv.x = center.x;
                uv.y = position.y + i * stepY;

                var vertical = ImageSamplingUtils.Sample(nativeArray, width, height, uv, imageTransform);
                if (vertical < depth)
                {
                    depth = vertical;
                }
            }

            return depth;
        }

        public static Rect CalculateScreenRect(Renderer forRenderer, Camera usingCamera)
        {
            var bounds = forRenderer.bounds;
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            Vector3[] points =
            {
                usingCamera.WorldToViewportPoint(new Vector3(c.x + e.x, c.y + e.y, c.z + e.z)),
                usingCamera.WorldToViewportPoint(new Vector3(c.x + e.x, c.y + e.y, c.z - e.z)),
                usingCamera.WorldToViewportPoint(new Vector3(c.x + e.x, c.y - e.y, c.z + e.z)),
                usingCamera.WorldToViewportPoint(new Vector3(c.x + e.x, c.y - e.y, c.z - e.z)),
                usingCamera.WorldToViewportPoint(new Vector3(c.x - e.x, c.y + e.y, c.z + e.z)),
                usingCamera.WorldToViewportPoint(new Vector3(c.x - e.x, c.y + e.y, c.z - e.z)),
                usingCamera.WorldToViewportPoint(new Vector3(c.x - e.x, c.y - e.y, c.z + e.z)),
                usingCamera.WorldToViewportPoint(new Vector3(c.x - e.x, c.y - e.y, c.z - e.z))
            };

            float maxX = 0.0f;
            float maxY = 0.0f;
            float minX = 1.0f;
            float minY = 1.0f;
            for (var i = 0; i < points.Length; i++)
            {
                Vector3 entry = points[i];
                maxX = Mathf.Max(entry.x, maxX);
                maxY = Mathf.Max(entry.y, maxY);
                minX = Mathf.Min(entry.x, minX);
                minY = Mathf.Min(entry.y, minY);
            }

            maxX = Mathf.Clamp(maxX, 0.0f, 1.0f);
            minX = Mathf.Clamp(minX, 0.0f, 1.0f);
            maxY = Mathf.Clamp(maxY, 0.0f, 1.0f);
            minY = Mathf.Clamp(minY, 0.0f, 1.0f);

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Returns whether depth (z) direction is reversed on the running platform.
        /// </summary>
        /// <returns></returns>
        public static bool IsDepthReversed()
        {
            // https://docs.unity3d.com/Manual/SL-PlatformDifferences.html
            var device = SystemInfo.graphicsDeviceType;
            switch (device)
            {
                case GraphicsDeviceType.Direct3D11:
                case GraphicsDeviceType.Direct3D12:
                case GraphicsDeviceType.Metal:
                    return true;
                default:
                    return false;
            }
        }
    }
}
