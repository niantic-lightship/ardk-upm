// Copyright 2022-2025 Niantic.

using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.API;

namespace Niantic.Lightship.AR
{
#if ARF_6_1_OR_NEWER
    /// <summary>
    /// A utility class that wraps a native external texture (from an AR subsystem) in a Unity Render Texture object.
    /// </summary>
    internal sealed class LightshipExternalRenderTexture : LightshipExternalTexture
    {
        /// <summary>
        /// The Unity <c>Texture</c> object wrapping the external (native) texture.
        /// </summary>
        public override Texture Texture => _texture;

        /// <summary>
        /// The format of the external texture.
        /// </summary>
        public override GraphicsFormat Format => GraphicsFormatUtility.GetGraphicsFormat(Descriptor.format,
            isSRGB: Descriptor.textureType is XRTextureType.ColorRenderTexture);

        // Resources
        private RenderTexture _texture;
        private uint _renderTextureId;

        /// <summary>
        /// Called when the texture needs to be created for the first time or recreated with new metadata.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        protected override bool OnCreateTexture(XRTextureDescriptor descriptor)
        {
            var displaySubsystem = DisplaySubsystem;
            if (displaySubsystem == null)
            {
                Debug.LogError("RenderTexture cannot be created because the XRDisplaySubsystem is not loaded.");
                return false;
            }

            if (UnityXRDisplay.CreateTexture(ToUnityXRRenderTextureDesc(descriptor), out _renderTextureId))
            {
                _texture = displaySubsystem.GetRenderTexture(_renderTextureId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called when the texture needs to be updated with new data but the metadata has not changed.
        /// </summary>
        /// <param name="descriptor">The descriptor containing the texture information.</param>
        /// <returns>True if the texture was updated successfully, false otherwise.</returns>
        protected override bool OnUpdateTexture(XRTextureDescriptor descriptor)
        {
            var newAllocation = Descriptor.nativeTexture != descriptor.nativeTexture;
            if (newAllocation)
            {
                OnDestroyTexture();
                if (!OnCreateTexture(descriptor))
                {
                    return false;
                }
            }

            var displaySubsystem = DisplaySubsystem;
            if (displaySubsystem == null)
            {
                Debug.LogError("RenderTexture cannot be retrieved because the XRDisplaySubsystem is not loaded.");
                return false;
            }

            _texture = displaySubsystem.GetRenderTexture(_renderTextureId);
            return _texture != null;
        }

        /// <summary>
        /// Called when the texture needs to be destroyed and resources released.
        /// </summary>
        protected override void OnDestroyTexture()
        {
            if (_texture != null)
            {
                UnityObjectUtils.Destroy(Texture);
                _texture = null;
            }
        }

        /// <summary>
        /// Retrieves the XRDisplaySubsystem instance, if available.
        /// </summary>
        private static XRDisplaySubsystem DisplaySubsystem
        {
            get
            {
                if (XRGeneralSettings.Instance == null || XRGeneralSettings.Instance.Manager == null)
                {
                    return null;
                }

                var loader = XRGeneralSettings.Instance.Manager.activeLoader;
                return loader != null ? loader.GetLoadedSubsystem<XRDisplaySubsystem>() : null;
            }
        }

        /// <summary>
        /// Converts a <c>TextureFormat</c> to a <c>UnityXRRenderTextureFormat</c>.
        /// </summary>
        /// <param name="textureFormat">The texture format to convert.</param>
        /// <exception cref="NotSupportedException">Thrown if the texture format cannot be converted.</exception>
        private static UnityXRRenderTextureFormat ToUnityXRRenderTextureFormat(TextureFormat textureFormat)
        {
            switch (textureFormat)
            {
                case TextureFormat.RGBA32:
                    return UnityXRRenderTextureFormat.kUnityXRRenderTextureFormatRGBA32;
                case TextureFormat.BGRA32:
                    return UnityXRRenderTextureFormat.kUnityXRRenderTextureFormatBGRA32;
                case TextureFormat.RGB565:
                    return UnityXRRenderTextureFormat.kUnityXRRenderTextureFormatRGB565;
                case TextureFormat.RGBAHalf:
                    return UnityXRRenderTextureFormat.kUnityXRRenderTextureFormatR16G16B16A16_SFloat;
                default:
                    throw new NotSupportedException(
                        $"Attempted to convert unsupported TextureFormat {textureFormat} to UnityXRRenderTextureFormat");
            }
        }

        /// <summary>
        /// Converts a <c>TextureFormat</c> to a <c>UnityXRDepthTextureFormat</c>.
        /// </summary>
        /// <param name="textureFormat">The texture format to convert.</param>
        /// <exception cref="NotSupportedException">Thrown if the texture format cannot be converted.</exception>
        private static UnityXRDepthTextureFormat ToUnityXRDepthTextureFormat(TextureFormat textureFormat)
        {
            switch (textureFormat)
            {
                case TextureFormat.RFloat:
                    return UnityXRDepthTextureFormat.kUnityXRDepthTextureFormat24bitOrGreater;
                case TextureFormat.R16:
                case TextureFormat.RHalf:
                    return UnityXRDepthTextureFormat.kUnityXRDepthTextureFormat16bit;
                default:
                    throw new NotSupportedException(
                        $"Attempted to convert unsupported TextureFormat {textureFormat} to UnityXRDepthTextureFormat");
            }
        }

        /// <summary>
        /// Converts an <c>XRTextureDescriptor</c> to a <c>UnityXRRenderTextureDesc</c>.
        /// </summary>
        /// <param name="descriptor">The XR texture descriptor to convert.</param>
        /// <exception cref="NotSupportedException">Thrown if the texture descriptor cannot be converted.</exception>
        private static UnityXRRenderTextureDesc ToUnityXRRenderTextureDesc(XRTextureDescriptor descriptor)
        {
            var renderTextureDescriptor = new UnityXRRenderTextureDesc
            {
                shadingRateFormat = UnityXRShadingRateFormat.kUnityXRShadingRateFormatNone,
                shadingRate = new UnityXRTextureData(),
                width = (uint)descriptor.width,
                height = (uint)descriptor.height,
                textureArrayLength = (uint)descriptor.depth,
                flags = 0,
                colorFormat = UnityXRRenderTextureFormat.kUnityXRRenderTextureFormatNone,
                depthFormat = UnityXRDepthTextureFormat.kUnityXRDepthTextureFormatNone
            };

            switch (descriptor.textureType)
            {
                case XRTextureType.DepthRenderTexture:
                    renderTextureDescriptor.depthFormat = ToUnityXRDepthTextureFormat(descriptor.format);
                    renderTextureDescriptor.depth = new UnityXRTextureData { nativePtr = descriptor.nativeTexture };
                    break;
                case XRTextureType.ColorRenderTexture:
                    renderTextureDescriptor.colorFormat = ToUnityXRRenderTextureFormat(descriptor.format);
                    renderTextureDescriptor.color = new UnityXRTextureData { nativePtr = descriptor.nativeTexture };
                    break;
                default:
                    throw new NotSupportedException($"Unsupported texture type: {descriptor.textureType}");
            }

            return renderTextureDescriptor;
        }
    }
#endif
}
