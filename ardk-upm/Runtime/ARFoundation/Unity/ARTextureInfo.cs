using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Rendering;

using UnityObject = UnityEngine.Object;

namespace Niantic.Lightship.AR.ARFoundation
{
    /// <summary>
    /// Container that pairs a <see cref="UnityEngine.XR.ARSubsystems.XRTextureDescriptor"/> wrapping a native texture
    /// object with a <c>Texture</c> that is created for the native texture object.
    /// </summary>
    internal struct ARTextureInfo : IEquatable<ARTextureInfo>, IDisposable
    {
        /// <summary>
        /// Constant for whether the texture is in a linear color space.
        /// </summary>
        /// <value>
        /// Constant for whether the texture is in a linear color space.
        /// </value>
        private const bool _textureHasLinearColorSpace = false;

        /// <summary>
        /// The texture descriptor describing the metadata for the native texture object.
        /// </summary>
        /// <value>
        /// The texture descriptor describing the metadata for the native texture object.
        /// </value>
        public XRTextureDescriptor Descriptor
        {
            get { return _descriptor; }
        }
        private XRTextureDescriptor _descriptor;

        /// <summary>
        /// The Unity <c>Texture</c> object for the native texture.
        /// </summary>
        /// <value>
        /// The Unity <c>Texture</c> object for the native texture.
        /// </value>
        public Texture Texture
        {
            get { return _texture; }
        }
        private Texture _texture;

        /// <summary>
        /// Constructs the texture info with the given descriptor and material.
        /// </summary>
        /// <param name="descriptor">The texture descriptor wrapping a native texture object.</param>
        public ARTextureInfo(XRTextureDescriptor descriptor)
        {
            _descriptor = descriptor;
            _texture = CreateTexture(_descriptor);
        }

        /// <summary>
        /// Resets the texture info back to the default state destroying the texture GameObject, if one exists.
        /// </summary>
        public void Reset()
        {
            _descriptor.Reset();
            DestroyTexture();
        }

        /// <summary>
        /// Destroys the texture and sets the property to <c>null</c>.
        /// </summary>
        void DestroyTexture()
        {
            if (_texture != null)
            {
                UnityObjectUtils.Destroy(_texture);
                _texture = null;
            }
        }

        /// <summary>
        /// Sets the current descriptor and creates/updates the associated texture as appropriate.
        /// </summary>
        /// <param name="textureInfo">The texture info to update.</param>
        /// <param name="descriptor">The texture descriptor wrapping a native texture object.</param>
        /// <returns>
        /// The updated texture information.
        /// </returns>
        public static ARTextureInfo GetUpdatedTextureInfo(ARTextureInfo textureInfo, XRTextureDescriptor descriptor)
        {
            // If the current and given descriptors are equal, exit early from this method.
            if (textureInfo._descriptor.Equals(descriptor))
            {
                return textureInfo;
            }

            // If the given descriptor is invalid, destroy any existing texture, and return the default texture
            // info.
            if (!descriptor.valid)
            {
                textureInfo.DestroyTexture();
                return default(ARTextureInfo);
            }

            //DebugWarn.WhenFalse(textureInfo._descriptor.dimension == TextureDimension.None || textureInfo._descriptor.dimension == descriptor.dimension)?.
            //    WithMessage($"Texture descriptor dimension should not change from {textureInfo._descriptor.dimension} to {descriptor.dimension}.");

            // If there is a texture already and if the descriptors have identical texture metadata, we only need
            // to update the existing texture with the given native texture object.
            if ((textureInfo._texture != null) && textureInfo._descriptor.hasIdenticalTextureMetadata(descriptor))
            {
                // Update the current descriptor with the given descriptor.
                textureInfo._descriptor = descriptor;

                // Update the current texture with the native texture object.
                switch(descriptor.dimension)
                {
                    case TextureDimension.Tex3D:
                        ((Texture3D)textureInfo._texture).UpdateExternalTexture(textureInfo._descriptor.nativeTexture);
                        break;
                    case TextureDimension.Tex2D:
                        ((Texture2D)textureInfo._texture).UpdateExternalTexture(textureInfo._descriptor.nativeTexture);
                        break;
                    case TextureDimension.Cube:
                        ((Cubemap)textureInfo._texture).UpdateExternalTexture(textureInfo._descriptor.nativeTexture);
                        break;
                    default:
                        throw new NotSupportedException($"'{descriptor.dimension.ToString()}' is not a supported texture type.");
                }
            }
            // Else, we need to destroy the existing texture object and create a new texture object.
            else
            {
                // Update the current descriptor with the given descriptor.
                textureInfo._descriptor = descriptor;

                // Replace the current texture with a newly created texture, and update the material.
                textureInfo.DestroyTexture();
                textureInfo._texture = CreateTexture(textureInfo._descriptor);
            }

            return textureInfo;
        }

        /// <summary>
        /// Create the texture object for the native texture wrapped by the valid descriptor.
        /// </summary>
        /// <param name="descriptor">The texture descriptor wrapping a native texture object.</param>
        /// <returns>
        /// If the descriptor is valid, the <c>Texture</c> object created from the texture descriptor. Otherwise,
        /// <c>null</c>.
        /// </returns>
        static Texture CreateTexture(XRTextureDescriptor descriptor)
        {
            if (!descriptor.valid)
            {
                return null;
            }

            switch(descriptor.dimension)
            {
                case TextureDimension.Tex3D:
                    return Texture3D.CreateExternalTexture(descriptor.width, descriptor.height,
                                                        descriptor.depth, descriptor.format,
                                                        descriptor.mipmapCount > 1, descriptor.nativeTexture);
                case TextureDimension.Tex2D:
                    var texture = Texture2D.CreateExternalTexture(descriptor.width, descriptor.height,
                                                        descriptor.format, (descriptor.mipmapCount > 1),
                                                        _textureHasLinearColorSpace,
                                                        descriptor.nativeTexture);
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
                case TextureDimension.Cube:
                    return Cubemap.CreateExternalTexture(descriptor.width,
                                                            descriptor.format,
                                                            descriptor.mipmapCount > 1,
                                                            descriptor.nativeTexture);
                default:
                    return null;
            }
        }

        public static bool IsSupported(XRTextureDescriptor descriptor)
        {
            switch (descriptor.dimension)
            {
                case TextureDimension.Tex3D:
                case TextureDimension.Tex2D:
                case TextureDimension.Cube:
                    return true;
                default:
                    return false;
            }
        }

        public void Dispose()
        {
            DestroyTexture();
        }

        public override int GetHashCode()
        {
            var hash = 486187739;
            unchecked
            {
                hash = hash * 486187739 + _descriptor.GetHashCode();
                hash = hash * 486187739 + ((_texture == null) ? 0 : _texture.GetHashCode());
            }
            return hash;
        }

        public bool Equals(ARTextureInfo other)
        {
            return _descriptor.Equals(other.Descriptor) && (_texture == other._texture);
        }

        public override bool Equals(object obj)
        {
            return (obj is ARTextureInfo) && Equals((ARTextureInfo)obj);
        }

        public static bool operator ==(ARTextureInfo lhs, ARTextureInfo rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(ARTextureInfo lhs, ARTextureInfo rhs)
        {
            return !lhs.Equals(rhs);
        }
    }
}
