// Copyright 2022-2025 Niantic.

using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// A utility class that wraps a native external texture (from an AR subsystem) in a Unity Texture2D object.
    /// </summary>
    internal sealed class LightshipExternalTexture2D : LightshipExternalTexture
    {
        /// <summary>
        /// The Unity <c>Texture</c> object wrapping the external (native) texture.
        /// </summary>
        public override Texture Texture => _texture;

        /// <summary>
        /// The format of the external texture.
        /// </summary>
        public override GraphicsFormat Format =>
            GraphicsFormatUtility.GetGraphicsFormat(Descriptor.format, !UseLinearColorSpace);

        // Resources
        private Texture2D _texture;

        /// <summary>
        /// Called when the texture needs to be created for the first time or recreated with new metadata.
        /// </summary>
        protected override bool OnCreateTexture(XRTextureDescriptor descriptor)
        {
            Debug.Assert(Texture == null, "OnCreateTexture should not be called when a texture already exists.");
            _texture = Texture2D.CreateExternalTexture(
                width: descriptor.width,
                height: descriptor.height,
                format: descriptor.format,
                mipChain: descriptor.mipmapCount > 1,
                linear: UseLinearColorSpace,
                nativeTex: descriptor.nativeTexture);

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
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.filterMode = FilterMode.Bilinear;
            _texture.hideFlags = HideFlags.HideAndDontSave;

            return true;
        }

        /// <summary>
        /// Called when the texture needs to be updated with new data but the metadata has not changed.
        /// </summary>
        /// <param name="descriptor">The descriptor containing the texture information.</param>
        /// <returns>True if the texture was updated successfully, false otherwise.</returns>
        protected override bool OnUpdateTexture(XRTextureDescriptor descriptor)
        {
            Debug.Assert(Texture != null);
            _texture.UpdateExternalTexture(descriptor.nativeTexture);
            return true;
        }

        /// <summary>
        /// Called when the texture needs to be destroyed and resources released.
        /// </summary>
        protected override void OnDestroyTexture()
        {
            if (_texture != null)
            {
                UnityObjectUtils.Destroy(_texture);
                _texture = null;
            }
        }
    }
}
