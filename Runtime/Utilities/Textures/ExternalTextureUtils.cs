// Copyright 2022-2024 Niantic.

using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities.Textures
{
    public static class ExternalTextureUtils
    {
        /// <summary>
        /// Creates an external Unity texture using the information provided by the descriptor.
        /// </summary>
        /// <param name="descriptor">Holds information about the native texture resource.</param>
        /// <param name="useLinearColorSpace">Should the Unity texture use linear color space?</param>
        /// <returns>
        /// If the descriptor is valid, the <c>Texture</c> object created from the texture descriptor. Otherwise,
        /// <c>null</c>.
        /// </returns>
        public static Texture CreateExternalTexture(XRTextureDescriptor descriptor, bool useLinearColorSpace = false)
        {
            if (!descriptor.valid)
            {
                return null;
            }

            switch (descriptor.dimension)
            {
                case TextureDimension.Tex2D:
                    var texture = Texture2D.CreateExternalTexture
                    (
                        descriptor.width,
                        descriptor.height,
                        descriptor.format,
                        descriptor.mipmapCount > 1,
                        useLinearColorSpace,
                        descriptor.nativeTexture
                    );

                    // NB: SetWrapMode needs to be the first call here, and the value passed
                    //     needs to be kTexWrapClamp - this is due to limitations of what
                    //     wrap modes are allowed for external textures in OpenGL (which are
                    //     used for ARCore), as Texture::ApplySettings will eventually hit
                    //     an assert about an invalid enum (see calls to glTexParameteri
                    //     towards the top of ApiGLES::TextureSampler)
                    // reference: "3.7.14 External Textures" section of
                    // https://www.khronos.org/registry/OpenGL/extensions/OES/OES_EGL_image_external.txt
                    // (it shouldn't ever matter what the wrap mode is set to normally, since
                    // this is for a pass-through video texture, so we shouldn't ever need to
                    // worry about the wrap mode as textures should never "wrap")
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;
                    texture.hideFlags = HideFlags.HideAndDontSave;

                    return texture;

                case TextureDimension.Tex3D:
                    return Texture3D.CreateExternalTexture
                    (
                        descriptor.width,
                        descriptor.height,
                        descriptor.depth,
                        descriptor.format,
                        descriptor.mipmapCount > 1,
                        descriptor.nativeTexture
                    );

                case TextureDimension.Cube:
                    return Cubemap.CreateExternalTexture
                    (
                        descriptor.width,
                        descriptor.format,
                        descriptor.mipmapCount > 1,
                        descriptor.nativeTexture
                    );

                default:
                    throw new NotSupportedException(
                        $"'{descriptor.dimension.ToString()}' is not a supported texture type.");
            }
        }

        /// <summary>
        /// Creates an external Unity Texture2D object using the information provided by the descriptor.
        /// </summary>
        /// <param name="descriptor">Holds information about the native texture resource.</param>
        /// <param name="useLinearColorSpace">Should the Unity texture use linear color space?</param>
        /// <returns>
        /// If the descriptor is valid, the <c>Texture</c> object created from the texture descriptor. Otherwise,
        /// <c>null</c>.
        /// </returns>
        public static Texture2D CreateExternalTexture2D(XRTextureDescriptor descriptor, bool useLinearColorSpace = false)
        {
            if (!descriptor.valid || descriptor.dimension != TextureDimension.Tex2D)
            {
                return null;
            }

            var texture = Texture2D.CreateExternalTexture
            (
                descriptor.width,
                descriptor.height,
                descriptor.format,
                descriptor.mipmapCount > 1,
                useLinearColorSpace,
                descriptor.nativeTexture
            );

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            return texture;
        }

        /// <summary>
        /// Updates an external Unity texture using the information provided by the descriptor.
        /// </summary>
        /// <param name="texture">The external texture to update.</param>
        /// <param name="descriptor">Holds information about the native texture resource.</param>
        /// <param name="useLinearColorSpace">Whether to use linear color space in case the texture gets re-created.</param>
        /// <returns></returns>
        public static bool UpdateExternalTexture(ref Texture texture, XRTextureDescriptor descriptor, bool useLinearColorSpace = false)
        {
            if (!descriptor.valid)
            {
                return false;
            }

            // If there is a texture already and if the descriptors have identical texture metadata, we only need
            // to update the existing texture with the given native texture object.
            if (texture != null && VerifyMetadata(texture, descriptor))
            {
                // Update the current texture with the native texture object.
                switch (descriptor.dimension)
                {
                    case TextureDimension.Tex3D:
                        ((Texture3D)texture).UpdateExternalTexture(descriptor.nativeTexture);
                        break;
                    case TextureDimension.Tex2D:
                        ((Texture2D)texture).UpdateExternalTexture(descriptor.nativeTexture);
                        break;
                    case TextureDimension.Cube:
                        ((Cubemap)texture).UpdateExternalTexture(descriptor.nativeTexture);
                        break;
                    default:
                        throw new NotSupportedException(
                            $"'{descriptor.dimension.ToString()}' is not a supported texture type.");
                }
            }
            // Else, we need to destroy the existing texture object and create a new texture object.
            else
            {
                if (texture != null)
                {
                    UnityObjectUtils.Destroy(texture);
                }

                texture = CreateExternalTexture(descriptor, useLinearColorSpace);
            }

            return true;
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

            ReadFromExternalTexture(externalTexture, destinationTexture);
            return destinationTexture;
        }

        // Blits the entirety of the source external texture into the destination texture using Unity's default material.
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

        // Blits the entirety of the source external texture into the destination texture using the provided material.
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

        /// <summary>
        /// Compares the external texture's meta data to the descriptor.
        /// </summary>
        /// <param name="externalTexture">The texture to verify.</param>
        /// <param name="descriptor">The texture descriptor.</param>
        /// <returns>True, if the texture is compatible with the descriptor and does not need to be re-allocated.</returns>
        private static bool VerifyMetadata(Texture externalTexture, XRTextureDescriptor descriptor)
        {
            if (externalTexture == null)
            {
                return false;
            }

            if (descriptor.width.Equals(externalTexture.width) &&
                descriptor.height.Equals(externalTexture.height) &&
                descriptor.dimension == externalTexture.dimension &&
                descriptor.mipmapCount.Equals(externalTexture.mipmapCount))
            {
                switch (descriptor.dimension)
                {
                    case TextureDimension.Tex2D:
                        var tex2d = externalTexture as Texture2D;
                        return tex2d != null && tex2d.format == descriptor.format;
                    case TextureDimension.Tex3D:
                        var tex3d = externalTexture as Texture3D;
                        return tex3d != null && tex3d.format == descriptor.format && tex3d.depth == descriptor.depth;
                    case TextureDimension.Cube:
                        var cubemap = externalTexture as Cubemap;
                        return cubemap != null && cubemap.format == descriptor.format;
                    default:
                        throw new ArgumentException(
                            "Encountered an unsupported texture dimension during texture verification.");
                }
            }

            return false;
        }
    }
}
