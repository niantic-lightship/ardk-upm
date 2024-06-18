// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.Utilities.Logging;
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
        public static int BitsPerPixel(this TextureFormat @this)
        {
            switch (@this)
            {
                case TextureFormat.RFloat:
                    return 32;
                case TextureFormat.R8:
                    return 8;
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                    return 32;
                case TextureFormat.RGB24:
                    return 24;
            }

            Log.Error($"Bits per pixel method not implemented for the provided format:{@this}");
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
            return @this.BitsPerPixel()/8;
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
                case XRCpuImage.Format.RGBA32:
                case XRCpuImage.Format.ARGB32:
                case XRCpuImage.Format.BGRA32:
                    return 4;
                case XRCpuImage.Format.RGB24:
                    return 3;
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
                case TextureFormat.RGBA32: return XRCpuImage.Format.RGBA32;
                case TextureFormat.BGRA32: return XRCpuImage.Format.BGRA32;
                case TextureFormat.RGB24: return XRCpuImage.Format.RGB24;
                default: throw new NotImplementedException($"Not implemented convertor for this format: {@this}");
            }
        }

        public static TextureFormat ConvertToTextureFormat(this XRCpuImage.Format @this)
        {
            switch (@this)
            {
                case XRCpuImage.Format.OneComponent8: return TextureFormat.R8;
                case XRCpuImage.Format.DepthFloat32: return TextureFormat.RFloat;
                case XRCpuImage.Format.ARGB32: return TextureFormat.ARGB32;
                case XRCpuImage.Format.RGBA32: return TextureFormat.RGBA32;
                case XRCpuImage.Format.BGRA32: return TextureFormat.BGRA32;
                case XRCpuImage.Format.RGB24: return TextureFormat.RGB24;
                default: throw new NotImplementedException($"Not implemented convertor for this format: {@this}");
            }
        }
    }
}
