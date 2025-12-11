using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// Lightship's version of ARFoundation's ARTextureInfo.
    /// It is used for surfacing external (native) textures provided by Lightship AR subsystems.
    /// </summary>
    internal abstract class LightshipExternalTexture : IDisposable
    {
        /// <summary>
        /// The Unity <c>Texture</c> object wrapping the external (native) texture.
        /// </summary>
        public abstract Texture Texture { get; }

        /// <summary>
        /// The format of the external texture.
        /// </summary>
        public abstract GraphicsFormat Format { get; }

        /// <summary>
        /// The width of the texture.
        /// </summary>
        public int Width => Descriptor.width;

        /// <summary>
        /// The height of the texture.
        /// </summary>
        public int Height => Descriptor.height;

        /// <summary>
        /// The texture descriptor describing the metadata for the native texture object.
        /// </summary>
        public XRTextureDescriptor Descriptor { get; private set; }

        /// <summary>
        /// Whether the texture should be created in linear color space.
        /// </summary>
        protected bool UseLinearColorSpace;

        /// <summary>
        /// Called when the texture needs to be created for the first time or recreated with new metadata.
        /// </summary>
        /// <param name="descriptor">The descriptor containing the texture information.</param>
        /// <returns>True if the texture was created successfully, false otherwise.</returns>
        protected abstract bool OnCreateTexture(XRTextureDescriptor descriptor);

        /// <summary>
        /// Called when the texture needs to be updated with new data but the metadata has not changed.
        /// </summary>
        /// <param name="descriptor">The descriptor containing the texture information.</param>
        /// <returns>True if the texture was updated successfully, false otherwise.</returns>
        protected abstract bool OnUpdateTexture(XRTextureDescriptor descriptor);

        /// <summary>
        /// Called when the texture needs to be destroyed and resources released.
        /// </summary>
        protected abstract void OnDestroyTexture();

        /// <summary>
        /// Updates the Unity representation of the external texture based on the provided descriptor.
        /// </summary>
        /// <param name="descriptor">The descriptor containing the texture information.</param>
        /// <returns>True if the texture was created or updated successfully, false otherwise.</returns>
        public bool Update(XRTextureDescriptor descriptor)
        {
            // Check if the descriptor is valid and matches the expected texture type
#if ARF_6_1_OR_NEWER
            if (descriptor.textureType != Descriptor.textureType)
                throw new ArgumentException(
                    $"Invalid texture type {descriptor.textureType}. Expected {Descriptor.textureType}.");
#else
            if (descriptor.dimension != Descriptor.dimension)
                throw new ArgumentException(
                    $"Invalid texture type {descriptor.dimension}. Expected {Descriptor.dimension}.");
#endif

            // Check if the descriptor is valid
            if (!descriptor.valid)
            {
                Debug.LogWarning($"Invalid texture descriptor: {descriptor}");
                return false;
            }

            // Check if the descriptor has changed
            if (descriptor.Equals(Descriptor))
            {
                return true;
            }

            // Update texture data if the descriptor has changed
            if (Descriptor.hasIdenticalTextureMetadata(descriptor))
            {
                if (!OnUpdateTexture(descriptor))
                {
                    Debug.LogWarning($"Failed to update texture with descriptor: {descriptor}");
                    return false;
                }
            }
            else
            {
                // Recreate the texture if the metadata has changed
                OnDestroyTexture();
                if (!OnCreateTexture(descriptor))
                {
                    Debug.LogWarning($"Failed to create texture with descriptor: {descriptor}");
                    return false;
                }
            }

            // Update the descriptor
            Descriptor = descriptor;
            return true;
        }

        /// <summary>
        /// Factory method to create a LightshipExternalTexture from a XRTextureDescriptor.
        /// </summary>
        /// <param name="descriptor">The texture descriptor.</param>
        /// <param name="useLinearColorSpace">
        /// Whether to create the texture in linear color space. Based on the pixel format, this may be ignored.
        /// </param>
        /// <returns>A LightshipExternalTexture instance.</returns>
        /// <exception cref="NotSupportedException">Thrown if the texture descriptor is not supported.</exception>
        public static LightshipExternalTexture Create(XRTextureDescriptor descriptor, bool useLinearColorSpace = false)
        {
            if (!HasTexture(descriptor))
            {
                return null;
            }

            if (!IsTextureTypeSupported(descriptor))
            {
                Debug.LogError($"Unsupported texture descriptor: {descriptor}");
                return null;
            }

            LightshipExternalTexture instance;

#if ARF_6_1_OR_NEWER
            switch (descriptor.textureType)
            {
                case XRTextureType.Texture2D:
                    instance = new LightshipExternalTexture2D();
                    break;
                case XRTextureType.ColorRenderTexture:
                case XRTextureType.DepthRenderTexture:
                    instance = new LightshipExternalRenderTexture();
                    break;
                default:
                    throw new NotSupportedException($"Unsupported texture type: {descriptor.textureType}");
            }
#else
            switch (descriptor.dimension)
            {
                case TextureDimension.Tex2D:
                    instance = new LightshipExternalTexture2D();
                    break;
                default:
                    throw new NotSupportedException($"Unsupported texture dimension: {descriptor.dimension}");
            }
#endif
            instance.UseLinearColorSpace = useLinearColorSpace;
            if (instance.OnCreateTexture(descriptor))
            {
                instance.Descriptor = descriptor;
                return instance;
            }

            instance.Dispose();
            throw new NotSupportedException($"Failed to create texture from descriptor: {descriptor}");
        }

        /// <summary>
        /// Creates or updates a LightshipExternalTexture based on the provided descriptor.
        /// </summary>
        /// <param name="texture">The LightshipExternalTexture to create or update.</param>
        /// <param name="descriptor">The texture descriptor.</param>
        public static bool CreateOrUpdate(ref LightshipExternalTexture texture, XRTextureDescriptor descriptor)
        {
            if (texture == null)
            {
                texture = Create(descriptor);
                return texture != null;
            }

            return texture.Update(descriptor);
        }

        /// <summary>
        /// Checks if the texture descriptor is of a supported type.
        /// </summary>
        /// <param name="descriptor">The texture descriptor to check.</param>
        /// <returns>True if the texture descriptor is supported, false otherwise.</returns>
        private static bool IsTextureTypeSupported(XRTextureDescriptor descriptor)
        {
#if ARF_6_1_OR_NEWER
            return descriptor.textureType is XRTextureType.Texture2D
                or XRTextureType.ColorRenderTexture or XRTextureType.DepthRenderTexture;
#else
            return descriptor.dimension is UnityEngine.Rendering.TextureDimension.Tex2D;
#endif
        }

        /// <summary>
        /// Checks if the texture descriptor has a valid texture.
        /// </summary>
        private static bool HasTexture(XRTextureDescriptor descriptor)
        {
#if ARF_6_1_OR_NEWER
            return descriptor.textureType != XRTextureType.None
#else
            return descriptor.dimension != TextureDimension.None
#endif
                && descriptor.valid
                && descriptor.nativeTexture != IntPtr.Zero;
        }

        /// <summary>
        /// Hides the deprecation warning for XRTextureDescriptor.dimension.
        /// </summary>
        internal static TextureDimension GetTextureDimension(XRTextureDescriptor textureDescriptor)
        {
#if ARF_6_1_OR_NEWER
            switch (textureDescriptor.textureType)
            {
                case XRTextureType.None:
                    return TextureDimension.None;
                case XRTextureType.Texture2D:
                    return TextureDimension.Tex2D;
                case XRTextureType.Texture3D:
                    return TextureDimension.Tex3D;
                case XRTextureType.Cube:
                    return TextureDimension.Cube;
                default:
                    return TextureDimension.Unknown;
            }
#else
            return textureDescriptor.dimension;
#endif
        }

        public void Dispose()
        {
            OnDestroyTexture();
            GC.SuppressFinalize(this);
        }

        ~LightshipExternalTexture()
        {
            OnDestroyTexture();
        }
    }
}
