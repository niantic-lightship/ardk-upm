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
            }

            return 0; //unimplemented conversion for this format
        }

        /// <summary>
        /// Attempts to convert an `UnityEngine.TextureFormat` to a <see cref="XRCpuImage.Format"/>.
        /// </summary>
        /// <param name="this">The <see cref="TextureFormat"/> being extended.</param>
        /// <returns>Returns a <see cref="XRCpuImage.Format"/> that matches <paramref name="this"/> if possible. Returns 0 if there
        ///     is no matching <see cref="XRCpuImage.Format"/>.</returns>   
        public static XRCpuImage.Format XRCpuImageFormat(this TextureFormat @this)
        {
            switch (@this)
            {
                case TextureFormat.R8: return XRCpuImage.Format.OneComponent8;
                case TextureFormat.RFloat: return XRCpuImage.Format.DepthFloat32;
                default: return 0;
            }
        }
    }
}
