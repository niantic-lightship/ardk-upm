// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    public static class ExternalTextureUtils
    {
        public static Texture2D CreateExternalTexture(XRTextureDescriptor descriptor)
        {
            var tex =
                Texture2D.CreateExternalTexture
                (
                    descriptor.width,
                    descriptor.height,
                    descriptor.format,
                    (descriptor.mipmapCount > 1),
                    false,
                    descriptor.nativeTexture
                );

            // SetWrapMode needs to be the first call here. See ARFoundation/ARTextureInfo.cs for more.
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            return tex;
        }

        public static Texture2D CreateFromExternalTexture(Texture2D externalTexture, TextureFormat outputFormat)
        {
            var destinationTexture =
                new Texture2D
                (
                    externalTexture.width,
                    externalTexture.height,
                    outputFormat,
                    externalTexture.mipmapCount > 1
                );

            var rect = new Rect(0, 0, externalTexture.width, externalTexture.height);
            ReadFromExternalTexture(externalTexture, destinationTexture, rect);
            return destinationTexture;
        }

        // Performs direct blit from image to output texture using Unity's default material.
        public static void ReadFromExternalTexture
        (
            this Texture externalTexture,
            Texture2D destinationTexture,
            Rect viewRect
        )
        {
            var tmp =
                RenderTexture.GetTemporary
                (
                    destinationTexture.width,
                    destinationTexture.height,
                    0,
                    destinationTexture.graphicsFormat
                );

            var cachedRenderTarget = RenderTexture.active;
            Graphics.Blit(externalTexture, tmp);
            RenderTexture.ReleaseTemporary(tmp);

            // Reads pixels from the current render target and writes them to a texture.
            destinationTexture.ReadPixels(viewRect, 0, 0, false);

            RenderTexture.active = cachedRenderTarget;
        }

        public static void ReadFromExternalTexture
        (
            this Texture externalTexture,
            Texture2D destinationTexture,
            Rect viewRect,
            Material material
        )
        {
            var tmp =
                RenderTexture.GetTemporary
                (
                    destinationTexture.width,
                    destinationTexture.height
                );

            var cachedRenderTarget = RenderTexture.active;
            Graphics.Blit(externalTexture, tmp, material);
            RenderTexture.ReleaseTemporary(tmp);

            // Reads pixels from the current render target and writes them to a texture.
            destinationTexture.ReadPixels(viewRect, 0, 0, false);

            RenderTexture.active = cachedRenderTarget;
        }

        // Note: Changes to destination texture's raw data are not applied. Must call Apply() after using this method
        // to upload changes to the GPU.
        public static void LoadMirroredOverXAxis(this Texture2D destination, Texture2D source, int stride)
        {
            unsafe
            {
                var sourcePtr = (IntPtr)source.GetRawTextureData<byte>().GetUnsafePtr();
                var destPtr = (IntPtr) destination.GetRawTextureData<byte>().GetUnsafePtr();

                var width = source.width;
                var height = source.height;

                for (int row = 0; row < height; row++)
                {
                    int flippedRow = height - row - 1;
                    var rowLength = width * stride;

                    Buffer.MemoryCopy
                    (
                        (void*)(sourcePtr + row * rowLength), // source
                        (void*)(destPtr + flippedRow * rowLength), // dest
                        width * stride, // count
                        width * stride
                    );
                }
            }
        }
    }
}
