// Copyright 2022-2024 Niantic.
using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    internal static class TextureFormatExtensions
    {
        /// <summary>
        /// Utility method that will give you the number of bits per pixel for a particular TextureFormat
        /// </summary>
        /// <param name="this">The <see cref="TextureFormat"/> being extended.</param>
        /// <returns></returns>
        ///
        public static int BitsPerPixel(this TextureFormat @this)
        {
            switch (@this)
            {
                case TextureFormat.RFloat:
                    return 32;
                case TextureFormat.R8:
                    return 8;
                case TextureFormat.ARGB32:
                    return 32;
            }

            return 0; //unimplemented conversion for this format
        }

        /// <summary>
        /// Utility method that will give you the number of bytes per pixel for a particular TextureFormat
        /// </summary>
        /// <param name="this">The <see cref="TextureFormat"/> being extended.</param>
        /// <returns></returns>
        ///
        public static int BytesPerPixel(this TextureFormat @this)
        {
            switch (@this)
            {
                case TextureFormat.RFloat:
                    return 4;
                case TextureFormat.R8:
                    return 1;
                case TextureFormat.ARGB32:
                    return 4;
            }

            return 0; //unimplemented conversion for this format
        }

        /// <summary>
        /// Utility method that will give you the number of bytes per pixel for a particular XRCpuImage.Format
        /// </summary>
        /// <param name="this">The <see cref="XRCpuImage.Format"/> being extended.</param>
        /// <returns></returns>
        ///
        public static int BytesPerPixel(this XRCpuImage.Format @this)
        {
            switch (@this)
            {
                case XRCpuImage.Format.DepthFloat32:
                    return 4;
                case XRCpuImage.Format.OneComponent8:
                    return 1;
                case XRCpuImage.Format.ARGB32:
                    return 4;
            }

            return 0; //unimplemented conversion for this format
        }

        /// <summary>
        /// Attempts to convert an `UnityEngine.TextureFormat` to a <see cref="XRCpuImage.Format"/>.
        /// </summary>
        /// <param name="this">The <see cref="TextureFormat"/> being extended.</param>
        /// <returns>Returns a <see cref="XRCpuImage.Format"/> that matches <paramref name="this"/> if possible. Returns 0 if there
        ///     is no matching <see cref="XRCpuImage.Format"/>.</returns>
        public static XRCpuImage.Format ConvertToXRCpuImageFormat(this TextureFormat @this)
        {
            switch (@this)
            {
                case TextureFormat.R8: return XRCpuImage.Format.OneComponent8;
                case TextureFormat.RFloat: return XRCpuImage.Format.DepthFloat32;
                case TextureFormat.ARGB32: return XRCpuImage.Format.ARGB32;
                default: return 0;
            }
        }

        public static TextureFormat ConvertToTextureFormat(this XRCpuImage.Format @this)
        {
            switch (@this)
            {
                case XRCpuImage.Format.OneComponent8: return TextureFormat.R8;
                case XRCpuImage.Format.DepthFloat32: return TextureFormat.RFloat;
                case XRCpuImage.Format.ARGB32: return TextureFormat.ARGB32;
                default: throw new NotImplementedException();
            }
        }
    }
}
