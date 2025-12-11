// Copyright 2022-2025 Niantic.

using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Experimental.Rendering;
#endif

namespace Niantic.Lightship.AR.Utilities.Textures
{
    public static class ExternalTextureUtils
    {
        /// <summary>
        /// Creates a new Texture2D object from the provided external texture.
        /// </summary>
        /// <remarks>Avoid using in production code, since this function involves a CPU readback and is slow.</remarks>
        /// <param name="externalTexture">The source external/native texture to copy from.</param>
        /// <param name="outputFormat">Optional. The desired output texture format. If null, the format of the external texture will be used.</param>
        /// <returns>The Texture2D object created from the external texture.</returns>
        public static Texture2D CreateFromExternalTexture(Texture2D externalTexture, TextureFormat? outputFormat = null)
        {
            var destinationTexture = new Texture2D
            (
                externalTexture.width,
                externalTexture.height,
#if UNITY_6000_0_OR_NEWER
                GraphicsFormatUtility.GetGraphicsFormat(outputFormat ?? externalTexture.format,
                    externalTexture.isDataSRGB),
                externalTexture.mipmapCount > 1
                    ? TextureCreationFlags.MipChain
                    : TextureCreationFlags.DontInitializePixels
#else
                    outputFormat ?? externalTexture.format, externalTexture.mipmapCount > 1
#endif
            );

            ReadFromExternalTexture(externalTexture, destinationTexture);
            return destinationTexture;
        }

        /// <summary>
        /// Reads the entirety of the source external texture into the destination texture.
        /// </summary>
        public static void ReadFromExternalTexture
        (
            this Texture externalTexture,
            Texture2D destinationTexture
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
            var viewRect = new Rect(0, 0, externalTexture.width, externalTexture.height);
            destinationTexture.ReadPixels(viewRect, 0, 0, false);

            RenderTexture.active = cachedRenderTarget;
        }

        /// <summary>
        /// Reads the entirety of the source external texture into the destination texture using the provided material.
        /// </summary>
        public static void ReadFromExternalTexture
        (
            this Texture externalTexture,
            Texture2D destinationTexture,
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
            var viewRect = new Rect(0, 0, externalTexture.width, externalTexture.height);
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
