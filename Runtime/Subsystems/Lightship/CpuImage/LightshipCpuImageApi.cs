using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR
{
    class LightshipCpuImageApi : XRCpuImage.Api
    {
        /// <summary>
        /// Dictionary of textures that represent the XRCPUImages that we have obtained from a Lightship Provider
        /// and that we wish to manage using this API.
        /// </summary>
        private Dictionary<int, Texture2D> _managedXrCpuImages;

        /// <summary>
        /// The shared API instance.
        /// </summary>
        public static LightshipCpuImageApi instance { get; } = new LightshipCpuImageApi();

        private Texture2D convertedTexture;

        private LightshipCpuImageApi()
        {
            _managedXrCpuImages = new Dictionary<int, Texture2D>();
            convertedTexture = new Texture2D(16, 16);
        }

        /// <summary>
        /// Add a texture that represents an XRCpuImage along with the nativeHandle we will use to refer to it
        /// into the API so that it can manage any conversions and output to CPU.
        /// </summary>
        /// <param name="nativeHandle">A unique identifier for the texture used to reference it in this API</param>
        /// <param name="cpuImageTexture">A texture that represents an image that we might want to do conversions on
        /// or output to a CPU memory buffer</param>
        public void AddManagedXRCpuImage(int nativeHandle, Texture2D cpuImageTexture)
        {
            _managedXrCpuImages[nativeHandle] = cpuImageTexture;
        }

        /// <summary>
        /// Dispose an existing native image identified by <paramref name="nativeHandle"/>.
        /// </summary>
        /// <param name="nativeHandle">A unique identifier for this camera image.</param>
        public override void DisposeImage(int nativeHandle)
        {
            if (_managedXrCpuImages.ContainsKey(nativeHandle))
            {
                Object.Destroy(_managedXrCpuImages[nativeHandle]);
                _managedXrCpuImages.Remove(nativeHandle);
            }
        }

        public override bool TryGetPlane(int nativeHandle, int planeIndex, out XRCpuImage.Plane.Cinfo planeCinfo)
        {
            if (!NativeHandleValid(nativeHandle) || planeIndex != 0)
            {
                planeCinfo = default;
                return false;
            }

            var texture = _managedXrCpuImages[nativeHandle];
            var nativeArray = texture.GetRawTextureData<byte>();
            var dataLength = nativeArray.Length;
            var pixelStride = texture.format.BitsPerPixel() / 8;
            var rowStride = pixelStride * texture.width;

            unsafe
            {
                planeCinfo = new XRCpuImage.Plane.Cinfo((IntPtr)nativeArray.GetUnsafePtr(), dataLength, rowStride, pixelStride);
            }

            return true;
        }

        /// <summary>
        /// Determine whether a native image handle returned by
        /// <see cref="LightshipCameraSubsystem.Provider.TryAcquireLatestCpuImage"/> is currently valid. An image can
        /// become invalid if it has been disposed.
        /// </summary>
        /// <remarks>
        /// If a handle is valid, <see cref="TryConvert"/> and <see cref="TryGetConvertedDataSize"/> should not fail.
        /// </remarks>
        /// <param name="nativeHandle">A unique identifier for the camera image in question.</param>
        /// <returns><c>true</c>, if it is a valid handle. Otherwise, <c>false</c>.</returns>
        /// <seealso cref="DisposeImage"/>
        public override bool NativeHandleValid(int nativeHandle)
        {
            return _managedXrCpuImages.ContainsKey(nativeHandle);
        }

        /// <summary>
        /// Get the number of bytes required to store an image with the given dimensions and
        /// [TextureFormat](https://docs.unity3d.com/ScriptReference/TextureFormat.html).
        /// </summary>
        /// <param name="nativeHandle">A unique identifier for the camera image to convert.</param>
        /// <param name="dimensions">The dimensions of the output image.</param>
        /// <param name="format">The <c>TextureFormat</c> for the image.</param>
        /// <param name="size">The number of bytes required to store the converted image.</param>
        /// <returns><c>true</c> if the output <paramref name="size"/> was set.</returns>
        public override bool TryGetConvertedDataSize(
            int nativeHandle,
            Vector2Int dimensions,
            TextureFormat format,
            out int size)
        {
            var bitsPerPixel = format.BitsPerPixel();
            if (bitsPerPixel == 0)
            {
                size = 0;
                return false;
            }

            size = dimensions.x * dimensions.y * bitsPerPixel / 8;
            return size > 0;
        }

        /// <summary>
        /// Convert the image with handle <paramref name="nativeHandle"/> using the provided
        /// <paramref cref="conversionParams"/>.
        /// </summary>
        /// <param name="nativeHandle">A unique identifier for the camera image to convert.</param>
        /// <param name="conversionParams">The parameters to use during the conversion.</param>
        /// <param name="destinationBuffer">A buffer to write the converted image to.</param>
        /// <param name="bufferLength">The number of bytes available in the buffer.</param>
        /// <returns>
        /// <c>true</c> if the image was converted and stored in <paramref name="destinationBuffer"/>.
        /// </returns>
        public override bool TryConvert(
            int nativeHandle,
            XRCpuImage.ConversionParams conversionParams,
            IntPtr destinationBuffer,
            int bufferLength)
        {
            if (!NativeHandleValid(nativeHandle))
            {
                return false;
            }

            var texture = _managedXrCpuImages[nativeHandle];

            if (!s_SupportedConversions[texture.format].Contains(conversionParams.outputFormat))
            {
                return false;
            }

            if (convertedTexture.width != conversionParams.outputDimensions.x
                || convertedTexture.height != conversionParams.outputDimensions.y
                || convertedTexture.format != conversionParams.outputFormat)
            {
                convertedTexture.Reinitialize(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y,
                    conversionParams.outputFormat, false);
            }

            Graphics.CopyTexture(texture, 0, 0, conversionParams.inputRect.x, conversionParams.inputRect.y,
                conversionParams.inputRect.width, conversionParams.inputRect.height, convertedTexture,
                0, 0, 0, 0);
            var imageArray = convertedTexture.GetRawTextureData<byte>();
            unsafe
            {
                UnsafeUtility.MemCpy(destinationBuffer.ToPointer(), imageArray.GetUnsafeReadOnlyPtr(),
                    bufferLength);
            }

            return true;
        }

        private static readonly Dictionary<TextureFormat, HashSet<TextureFormat>> s_SupportedConversions =
            new Dictionary<TextureFormat, HashSet<TextureFormat>>
            {
                { TextureFormat.RFloat, new HashSet<TextureFormat> { TextureFormat.RFloat } },
                { TextureFormat.R8, new HashSet<TextureFormat> { TextureFormat.R8, TextureFormat.Alpha8 } }
            };

        /// <summary>
        /// Determines whether a given
        /// [TextureFormat](https://docs.unity3d.com/ScriptReference/TextureFormat.html) is supported for image
        /// conversion.
        /// </summary>
        /// <param name="image">The <see cref="XRCpuImage"/> to convert.</param>
        /// <param name="format">The [TextureFormat](https://docs.unity3d.com/ScriptReference/TextureFormat.html)
        ///  to test.</param>
        /// <returns>Returns `true` if <paramref name="image"/> can be converted to <paramref name="format"/>.
        ///  Returns `false` otherwise.</returns>
        public override bool FormatSupported(XRCpuImage image, TextureFormat format)
        {
            switch (image.format)
            {
                case XRCpuImage.Format.OneComponent8:
                    return
                        format == TextureFormat.R8 ||
                        format == TextureFormat.Alpha8;
                case XRCpuImage.Format.DepthFloat32:
                    return format == TextureFormat.RFloat;
                default:
                    return false;
            }
        }
    }
}
