// Copyright 2022-2024 Niantic.

using System;
using System.Diagnostics.CodeAnalysis;
using Niantic.Lightship.AR.Utilities.Textures;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.ARFoundation.Unity
{
    /// <summary>
    /// COPY PASTED FROM UNITY
    /// Container that packages a <see cref="UnityEngine.XR.ARSubsystems.XRTextureDescriptor"/> wrapping a native
    /// texture object with a <c>Texture</c> that is created for the native texture object and a sampler matrix.
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
        public readonly XRTextureDescriptor Descriptor
        {
            get { return _descriptor; }
        }
        private XRTextureDescriptor _descriptor;

        /// <summary>
        /// The matrix that converts from normalized viewport coordinates to normalized texture coordinates.
        /// </summary>
        /// <value>
        /// The matrix that converts from normalized viewport coordinates to normalized texture coordinates.
        /// </value>
        public readonly Matrix4x4 SamplerMatrix
        {
            get { return _samplerMatrix; }
        }
        private Matrix4x4 _samplerMatrix;

        /// <summary>
        /// The dimensions of the viewport that were correspond the cached sampler matrix.
        /// </summary>
        /// <value>
        /// The matrix that converts from normalized viewport coordinates to normalized texture coordinates.
        /// </value>
        public readonly XRCameraParams CameraParams
        {
            get { return _cameraParams; }
        }
        private XRCameraParams _cameraParams;

        /// <summary>
        /// The Unity <c>Texture</c> object for the native texture.
        /// </summary>
        /// <value>
        /// The Unity <c>Texture</c> object for the native texture.
        /// </value>
        public readonly Texture Texture
        {
            get { return _texture; }
        }
        private Texture _texture;

        /// <summary>
        /// True, if this texture info was not updated in the current Unity frame.
        /// </summary>
        public readonly bool IsDirty
        {
            get { return _lastUpdatedUnityFrameId != Time.frameCount; }
        }
        private int _lastUpdatedUnityFrameId;

        /// <summary>
        /// Constructs the texture info with the given descriptor and material.
        /// </summary>
        /// <param name="descriptor">The texture descriptor wrapping a native texture object.</param>
        /// <param name="samplerMatrix">The transformation matrix that converts from viewport coordinates to texture coordinates.</param>
        /// <param name="cameraParams">The corresponding viewport dimensions used in the sampler matrix</param>
        public ARTextureInfo(XRTextureDescriptor descriptor, Matrix4x4 samplerMatrix, XRCameraParams cameraParams)
        {
            _descriptor = descriptor;
            _samplerMatrix = samplerMatrix;
            _cameraParams = cameraParams;
            _texture = ExternalTextureUtils.CreateExternalTexture(_descriptor);
            _lastUpdatedUnityFrameId = Time.frameCount;
        }

        /// <summary>
        /// Resets the texture info back to the default state destroying the texture GameObject, if one exists.
        /// </summary>
        public void Reset()
        {
            _descriptor.Reset();
            _samplerMatrix = Matrix4x4.identity;
            _cameraParams = default;
            DestroyTexture();
        }

        /// <summary>
        /// Destroys the texture and sets the property to <c>null</c>.
        /// </summary>
        private void DestroyTexture()
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
            return GetUpdatedTextureInfo(textureInfo, descriptor, Matrix4x4.identity, default);
        }

        /// <summary>
        /// Sets the current descriptor and creates/updates the associated texture as appropriate.
        /// </summary>
        /// <param name="textureInfo">The texture info to update.</param>
        /// <param name="descriptor">The texture descriptor wrapping a native texture object.</param>
        /// <param name="samplerMatrix">The transform that converts from viewport to texture.</param>
        /// <param name="cameraParams">The corresponding viewport dimensions used in the sampler matrix</param>
        /// <returns>
        /// The updated texture information.
        /// </returns>
        public static ARTextureInfo GetUpdatedTextureInfo(ARTextureInfo textureInfo, XRTextureDescriptor descriptor,
            Matrix4x4 samplerMatrix, XRCameraParams cameraParams)
        {
            // Mark updated for the current frame
            textureInfo._lastUpdatedUnityFrameId = Time.frameCount;

            // Update the sampler matrix first
            textureInfo._samplerMatrix = samplerMatrix;
            textureInfo._cameraParams = cameraParams;

            // If the current and given descriptors are equal, exit early from this method.
            if (textureInfo._descriptor.Equals(descriptor))
            {
                return textureInfo;
            }

            // If the given descriptor is invalid, destroy any existing texture, and return the default texture info.
            if (!descriptor.valid)
            {
                textureInfo.DestroyTexture();
                return default;
            }

            // Update the current descriptor with the given descriptor.
            textureInfo._descriptor = descriptor;

            // Update the external texture object
            return !ExternalTextureUtils.UpdateExternalTexture(ref textureInfo._texture, textureInfo._descriptor,
                _textureHasLinearColorSpace) ? default : textureInfo;
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

        [SuppressMessage("Performance", "EPS12:A struct member can be made readonly")]
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
