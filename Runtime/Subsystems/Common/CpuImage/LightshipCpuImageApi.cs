// Copyright 2022-2025 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Textures;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Common
{
    internal class LightshipCpuImageApi : XRCpuImage.Api
    {
        /// <summary>
        /// The shared API instance.
        /// </summary>
        public static LightshipCpuImageApi Instance { get; } = new();

        // Image allocations
        private NativeDataRepository _pool = new();
        private readonly Dictionary<int, NativeImage> _images = new();

        // Async conversion
        private int _nextAsyncConversionRequestId = 1;
        private readonly object _asyncConversionLock = new();
        private readonly HashSet<int> _asyncConversionRequests = new();
        private readonly Dictionary<int, (XRCpuImage.AsyncConversionStatus Status, NativeArray<byte>? Data)>
            _asyncConversionResults = new();

        private readonly struct NativeImage
        {
            public readonly int Handle;
            public readonly XRCpuImage.Format Format;
            public readonly Vector2Int Dimensions;
            public readonly int PlaneCount;
            public readonly double TimestampS;

            public int GetWidth(int planeIndex) => Mathf.Clamp(planeIndex, 0, 3) == 0
                ? Dimensions.x
                : (Dimensions.x + 1) / 2;

            public int GetHeight(int planeIndex) => Mathf.Clamp(planeIndex, 0, 3) == 0
                ? Dimensions.y
                : (Dimensions.y + 1) / 2;

            public int GetPixelStride(int planeIndex) => Mathf.Clamp(planeIndex, 0, 3) == 0
                ? Format.BytesPerPixel()
                : PlaneCount == 2
                    ? 2
                    : 1;

            public int GetRowStride(int planeIndex) => GetWidth(planeIndex) * GetPixelStride(planeIndex);

            public NativeImage(int handle, TextureFormat format, int width, int height, ulong timestampMs,
                int planeCount = 1)
            {
                Handle = handle;
                Format = format.ConvertToXRCpuImageFormat();
                Dimensions = new Vector2Int(width, height);
                TimestampS = timestampMs / 1000.0;
                PlaneCount = planeCount;
            }

            public NativeImage(int handle, XRCpuImage.Format format, int width, int height, ulong timestampMs,
                int planeCount = 1)
            {
                Handle = handle;
                Format = format;
                Dimensions = new Vector2Int(width, height);
                TimestampS = timestampMs / 1000.0;
                PlaneCount = planeCount;
            }
        }

        /// <summary>
        /// Utility method to reset the singleton instance in between tests.
        /// Not intended to actually be used in production.
        /// </summary>
        public void Reset()
        {
            _images.Clear();

            if (_pool.Size > 0)
            {
                Log.Warning("Failed to dispose memory allocated in the NativeDataRepository.");
            }

            _pool = new NativeDataRepository();
        }

        /// <summary>
        /// Copies the image data referenced by the specified pointer into the CPU image data store
        /// and creates a new <see cref="XRCpuImage.Cinfo"/> entry.
        /// </summary>
        /// <param name="data">A pointer to the raw image data to copy into the CPU image data store.</param>
        /// <param name="size">The size, in bytes, of the image data referenced by <paramref name="data"/>.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="format">The <see cref="TextureFormat"/> of the source image data.</param>
        /// <param name="timestampMs">The timestamp of the image in milliseconds.</param>
        /// <param name="cinfo">
        /// When this method returns <see langword="true"/>, contains the resulting <see cref="XRCpuImage.Cinfo"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the image data was successfully copied and added to the store;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryAddManagedXRCpuImage
        (
            IntPtr data,
            int size,
            int width,
            int height,
            TextureFormat format,
            ulong timestampMs,
            out XRCpuImage.Cinfo cinfo
        )
        {
            if (_pool.TryCopyFrom(data, size, out var handle))
            {
                var image = new NativeImage(handle, format, width, height, timestampMs);
                _images.Add(handle, image);
                cinfo = new XRCpuImage.Cinfo(handle, image.Dimensions, image.PlaneCount, image.TimestampS,
                    image.Format);

                return true;
            }

            cinfo = default;
            return false;
        }

        /// <summary>
        /// Copies the specified image data into the CPU image data store and creates a new <see cref="XRCpuImage.Cinfo"/> entry.
        /// </summary>
        /// <remarks>
        /// The resulting image is assumed to contain multiple planes (e.g., Y, U, and V).
        /// </remarks>
        /// <param name="dataPtrs">Pointers to the per-plane image data to copy into the CPU image data store.</param>
        /// <param name="dataSizes">The size in bytes of each plane in <paramref name="dataPtrs"/>.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="format">The <see cref="XRCpuImage.Format"/> of the image.</param>
        /// <param name="timestampMs">The timestamp of the image in milliseconds.</param>
        /// <param name="cinfo">When this method returns <see langword="true"/>, contains the resulting <see cref="XRCpuImage.Cinfo"/>.</param>
        /// <returns>
        /// <see langword="true"/> if the image data was successfully copied and added to the store; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryAddManagedXRCpuImage
        (
            IntPtr[] dataPtrs,
            int[] dataSizes,
            int width,
            int height,
            XRCpuImage.Format format,
            ulong timestampMs,
            out XRCpuImage.Cinfo cinfo
        )
        {
            if (_pool.TryCopyFrom(dataPtrs, dataSizes, out var handle))
            {
                var image = new NativeImage(handle, format, width, height, timestampMs, planeCount: dataPtrs.Length);
                _images.Add(handle, image);
                cinfo = new XRCpuImage.Cinfo(handle, image.Dimensions, image.PlaneCount, image.TimestampS,
                    image.Format);

                return true;
            }

            cinfo = default;
            return false;
        }

        /// <summary>
        /// Copies the provided image data into the CPU image data store and creates a new <see cref="XRCpuImage.Cinfo"/> entry.
        /// </summary>
        /// <remarks>
        /// The resulting image contains a single plane.
        /// </remarks>
        /// <param name="data">The raw image data to copy into the CPU image data store.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="format">The <see cref="XRCpuImage.Format"/> of the image.</param>
        /// <param name="timestampMs">The timestamp of the image in milliseconds.</param>
        /// <param name="cinfo">
        /// When this method returns <see langword="true"/>, contains the resulting <see cref="XRCpuImage.Cinfo"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the image data was successfully copied and added to the store; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryAddManagedXRCpuImage
        (
            byte[] data,
            int width,
            int height,
            XRCpuImage.Format format,
            ulong timestampMs,
            out XRCpuImage.Cinfo cinfo
        )
        {
            if (_pool.TryCopyFrom(data, out var handle))
            {
                var image = new NativeImage(handle, format, width, height, timestampMs, planeCount: 1);
                _images.Add(handle, image);
                cinfo = new XRCpuImage.Cinfo(handle, image.Dimensions, image.PlaneCount, image.TimestampS,
                    image.Format);

                return true;
            }

            cinfo = default;
            return false;
        }

        /// <summary>
        /// Copies the provided RGBA32 image data into the CPU image data store and creates a new <see cref="XRCpuImage.Cinfo"/> entry.
        /// </summary>
        /// <remarks>
        /// The resulting image contains a single plane stored in <see cref="XRCpuImage.Format.RGBA32"/> format.
        /// </remarks>
        /// <param name="data">The RGBA32 pixel data to copy into the CPU image data store.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="timestampMs">The timestamp of the image in milliseconds.</param>
        /// <param name="cinfo">
        /// When this method returns <see langword="true"/>, contains the resulting <see cref="XRCpuImage.Cinfo"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the image data was successfully copied and added to the store; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryAddManagedXRCpuImage
        (
            Color32[] data,
            int width,
            int height,
            ulong timestampMs,
            out XRCpuImage.Cinfo cinfo
        )
        {
            if (_pool.TryCopyFrom(data, out var handle))
            {
                var image = new NativeImage(handle, XRCpuImage.Format.RGBA32, width, height, timestampMs);
                _images.Add(handle, image);
                cinfo = new XRCpuImage.Cinfo(handle, image.Dimensions, image.PlaneCount, image.TimestampS,
                    image.Format);

                return true;
            }

            cinfo = default;
            return false;
        }

        /// <summary>
        /// Dispose an existing native image identified by <paramref name="nativeHandle"/>.
        /// </summary>
        /// <param name="nativeHandle">A unique identifier for this camera image.</param>
        public override void DisposeImage(int nativeHandle)
        {
            _images.Remove(nativeHandle);
            _pool.Dispose(nativeHandle);
        }

        /// <summary>
        /// Attempts to retrieve metadata and a data pointer for a specific plane
        /// within the image identified by the given native handle.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the plane exists and its data is available; otherwise, <see langword="false"/>.
        /// </returns>
        public override bool TryGetPlane(int nativeHandle, int planeIndex, out XRCpuImage.Plane.Cinfo planeCinfo)
        {
            // Does the image exist?
            var imageAvailable = _images.TryGetValue(nativeHandle, out var image);
            if (!imageAvailable)
            {
                planeCinfo = default;
                return false;
            }

            // Is the plane index valid?
            if (planeIndex < 0 || planeIndex >= image.PlaneCount)
            {
                planeCinfo = default;
                return false;
            }

            // Can access the image data from the handle?
            var dataAvailable = _pool.TryGetData(nativeHandle, out var nativeArray);
            if (!dataAvailable)
            {
                planeCinfo = default;
                return false;
            }

            // Get strides
            int pixelStride = image.GetPixelStride(planeIndex);
            int rowStride = image.GetRowStride(planeIndex);
            int dataLength = rowStride * image.GetHeight(planeIndex);

            // Get the offset to the start of the plane
            var bytesOffset = 0;
            for (var i = 0; i < planeIndex; i++)
            {
                bytesOffset += image.GetRowStride(i) * image.GetHeight(i);
            }

            // Get the pointer to the data
            IntPtr ptr;
            unsafe
            {
                ptr = (IntPtr)nativeArray.GetUnsafePtr() + bytesOffset;
            }

            planeCinfo = new XRCpuImage.Plane.Cinfo(ptr, dataLength, rowStride, pixelStride);
            return true;
        }

        /// <summary>
        /// Returns whether the given native image handle is currently valid.
        /// </summary>
        public override bool NativeHandleValid(int nativeHandle) => _images.ContainsKey(nativeHandle);

        /// <summary>
        /// Returns whether conversion from the image's source format to the specified
        /// <see cref="TextureFormat"/> is supported.
        /// </summary>
        public override bool FormatSupported(XRCpuImage image, TextureFormat format) =>
            ImageConversionUtils.IsGpuConversionSupported(image.format, format) ||
            ImageConversionUtils.IsCpuConversionSupported(image.format, format.ToXRCpuImageFormat());

        /// <summary>
        /// Get the number of bytes required to store an image with the given dimensions and <c>TextureFormat</c>.
        /// </summary>
        /// <param name="nativeHandle">A unique identifier for the camera image to convert.</param>
        /// <param name="dimensions">The dimensions of the output image.</param>
        /// <param name="format">The <c>TextureFormat</c> for the image.</param>
        /// <param name="size">The number of bytes required to store the converted image.</param>
        /// <returns><c>true</c> if the output <paramref name="size"/> was set.</returns>
        /// <exception cref="System.NotSupportedException">Thrown if the implementation does not support camera image conversion.</exception>
        public override bool TryGetConvertedDataSize
        (
            int nativeHandle,
            Vector2Int dimensions,
            TextureFormat format,
            out int size
        )
        {
            size = ImageConversionUtils.GetConvertedDataSize(format, dimensions.x, dimensions.y);
            return size > 0;
        }

        /// <summary>
        /// Performs a synchronous image conversion from a native camera image handle into a
        /// specified output buffer, using either a GPU-accelerated or CPU-based conversion path
        /// depending on the source image format.
        /// </summary>
        /// <param name="nativeHandle">The native handle of the camera image to convert.</param>
        /// <param name="conversionParams">
        /// The configuration specifying how the image should be converted, including the crop rectangle,
        /// output dimensions, target texture format, and any transformation flags (e.g., mirroring).
        /// </param>
        /// <param name="destinationBuffer">
        /// A pointer to a pre-allocated native memory buffer that will receive the converted pixel data.
        /// The buffer must be large enough to store all converted pixels as determined by
        /// <see cref="XRCpuImage.GetConvertedDataSize(XRCpuImage.ConversionParams)"/>.
        /// </param>
        /// <param name="bufferLength">
        /// The size, in bytes, of the provided <paramref name="destinationBuffer"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the image conversion succeeds; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// The conversion runs synchronously and writes the result directly into the provided
        /// <paramref name="destinationBuffer"/> before returning.
        /// </remarks>
        public override bool TryConvert(int nativeHandle, XRCpuImage.ConversionParams conversionParams,
            IntPtr destinationBuffer, int bufferLength)
        {
            if (_images.TryGetValue(nativeHandle, out var image))
            {
                if (_pool.TryGetData(image.Handle, out var sourceData))
                {
                    if (ImageConversionUtils.IsCpuConversionSupported(image.Format,
                            conversionParams.outputFormat.ToXRCpuImageFormat()))
                    {
                        TryGetPlane(nativeHandle, 0, out var plane0Info);
                        TryGetPlane(nativeHandle, 1, out var plane1Info);
                        TryGetPlane(nativeHandle, 2, out var plane2Info);

                        return ImageConversionUtils.TryConvertOnCpu(
                            plane0Info.dataPtr, plane0Info.rowStride,
                            plane1Info.dataPtr, plane1Info.rowStride,
                            plane2Info.dataPtr, plane2Info.rowStride,
                            image.PlaneCount, image.Dimensions.x,
                            image.Dimensions.y, image.Format,
                            destinationBuffer, conversionParams);
                    }

                    if (ImageConversionUtils.IsGpuConversionSupported(image.Format, conversionParams.outputFormat))
                    {
                        return ImageConversionUtils.TryConvertOnGpu(sourceData, image.Dimensions.x, image.Dimensions.y,
                            image.Format, destinationBuffer, conversionParams);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Performs an asynchronous GPU-based image conversion for the specified native camera image handle,
        /// invoking a completion callback when the operation finishes.
        /// </summary>
        /// <param name="nativeHandle">The native handle of the camera image to convert.</param>
        /// <param name="conversionParams">
        /// The conversion parameters that specify cropping, scaling, output dimensions, format, and transformation flags.
        /// </param>
        /// <param name="callback">
        /// A delegate invoked when the conversion completes or fails.
        /// The callback receives the conversion status, the same <paramref name="conversionParams"/>,
        /// a pointer to the converted image data (if successful), and the size of that data in bytes.
        /// </param>
        /// <param name="context">
        /// A user-defined context pointer passed through to the completion callback.
        /// This can be used to associate the callback with an external async request or job.
        /// </param>
        /// <remarks>
        /// Converting images with YUV formats such as <see cref="XRCpuImage.Format.AndroidYuv420_888"/> or
        /// <see cref="XRCpuImage.Format.IosYpCbCr420_8BiPlanarFullRange"/> is not currently supported by this method.
        /// Use the synchronous <see cref="TryConvert"/> API for those formats instead.
        /// </remarks>
        public override void ConvertAsync(int nativeHandle, XRCpuImage.ConversionParams conversionParams,
            OnImageRequestCompleteDelegate callback,
            IntPtr context)
        {
            if (_images.TryGetValue(nativeHandle, out var image))
            {
                if (!ImageConversionUtils.IsGpuConversionSupported(image.Format, conversionParams.outputFormat))
                {
                    Log.Warning($"[XRCpuImage] ConvertAsync is not supported for input={image.Format}, " +
                        $"output={conversionParams.outputFormat}");
                    callback.Invoke(XRCpuImage.AsyncConversionStatus.Failed, conversionParams, IntPtr.Zero, 0, context);
                    return;
                }

                if (_pool.TryGetData(image.Handle, out var sourceData))
                {
                    ImageConversionUtils.TryConvertOnGpuAsync(sourceData, image.Dimensions.x, image.Dimensions.y,
                        image.Format, conversionParams, onComplete: data =>
                        {
                            var success = data.HasValue;
                            var ptr = IntPtr.Zero;
                            if (success)
                            {
                                unsafe
                                {
                                    ptr = (IntPtr)data.Value.GetUnsafeReadOnlyPtr();
                                }
                            }

                            callback.Invoke(
                                success
                                    ? XRCpuImage.AsyncConversionStatus.Ready
                                    : XRCpuImage.AsyncConversionStatus.Failed,
                                conversionParams,
                                ptr,
                                success ? data.Value.Length : 0, context);
                        });

                    return;
                }
            }

            // Failed
            Log.Warning($"[XRCpuImage] ConvertAsync failed for handle {nativeHandle}, format={image.Format}, " +
                $"dims={image.Dimensions} output={conversionParams.outputFormat}");
            callback.Invoke(XRCpuImage.AsyncConversionStatus.Failed, conversionParams, IntPtr.Zero, 0, context);
        }

        /// <summary>
        /// Begins an asynchronous GPU-based image conversion and returns a unique request identifier
        /// that can later be queried for completion status or converted data.
        /// </summary>
        /// <param name="nativeHandle">The native handle of the source camera image to convert.</param>
        /// <param name="conversionParams">
        /// The configuration describing how the image should be converted, including crop rectangle,
        /// output dimensions, texture format, and transformation flags.
        /// </param>
        /// <returns>
        /// A unique integer request ID that can be used with
        /// <see cref="GetAsyncRequestStatus"/>, <see cref="TryGetAsyncRequestData"/>, and
        /// <see cref="DisposeAsyncRequest"/> to monitor and manage the conversion’s lifecycle.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method immediately schedules a GPU conversion by calling the callback-based
        /// <see cref="ConvertAsync(int, XRCpuImage.ConversionParams, OnImageRequestCompleteDelegate, IntPtr)"/>
        /// overload and returns a unique identifier for the request.
        /// </para>
        /// <para>
        /// When the GPU readback completes, the converted image data is copied into a persistent
        /// <see cref="NativeArray{T}"/> that is owned by this API until
        /// <see cref="DisposeAsyncRequest"/> is invoked.
        /// Call <see cref="GetAsyncRequestStatus"/> to poll for completion and
        /// <see cref="TryGetAsyncRequestData"/> to retrieve a pointer to the converted data.
        /// </para>
        /// <para>
        /// If the conversion fails, the result is recorded with a status of
        /// <see cref="XRCpuImage.AsyncConversionStatus.Failed"/>.
        /// </para>
        /// <para>
        /// Converting images with YUV formats such as <see cref="XRCpuImage.Format.AndroidYuv420_888"/> or
        /// <see cref="XRCpuImage.Format.IosYpCbCr420_8BiPlanarFullRange"/> is not currently supported by this method.
        /// Use the synchronous <see cref="TryConvert"/> API for those formats instead.
        /// </para>
        /// </remarks>
        public override int ConvertAsync(int nativeHandle, XRCpuImage.ConversionParams conversionParams)
        {
            var requestId = _nextAsyncConversionRequestId++;
            if (_nextAsyncConversionRequestId == int.MaxValue)
            {
                _nextAsyncConversionRequestId = 1;
            }

            lock (_asyncConversionLock)
            {
                _asyncConversionRequests.Add(requestId);
            }

            // Start the GPU conversion immediately
            ConvertAsync(nativeHandle, conversionParams, context: (IntPtr)requestId,
                callback: (status, _, ptr, length, ctx) =>
                {
                    NativeArray<byte>? copy = null;
                    if (status == XRCpuImage.AsyncConversionStatus.Ready && length > 0)
                    {
                        unsafe
                        {
                            copy = new NativeArray<byte>(length, Allocator.Persistent,
                                NativeArrayOptions.UninitializedMemory);
                            UnsafeUtility.MemCpy(
                                NativeArrayUnsafeUtility.GetUnsafePtr(copy.Value),
                                ptr.ToPointer(),
                                length);
                        }
                    }

                    if (status != XRCpuImage.AsyncConversionStatus.Ready)
                    {
                        Log.Warning($"[XRCpuImage] GPU conversion failed (status={status}, id={ctx})");
                    }

                    lock (_asyncConversionLock)
                    {
                        var id = (int)ctx;
                        _asyncConversionResults[id] = (status, copy);
                        _asyncConversionRequests.Remove(id);
                    }
                });

            return requestId;
        }

        /// <summary>
        /// Retrieves the current status of an asynchronous GPU image conversion request.
        /// </summary>
        /// <param name="requestId">The unique identifier of the async conversion request.</param>
        /// <returns>
        /// The current <see cref="XRCpuImage.AsyncConversionStatus"/> of the request:
        /// <list type="bullet">
        /// <item><term>Processing</term> – The request is still running on the GPU.</item>
        /// <item><term>Ready</term> – The conversion has completed successfully and the data can be retrieved.</item>
        /// <item><term>Failed</term> – The request ID is invalid or the conversion failed.</item>
        /// </list>
        /// </returns>
        public override XRCpuImage.AsyncConversionStatus GetAsyncRequestStatus(int requestId)
        {
            lock (_asyncConversionLock)
            {
                if (_asyncConversionResults.TryGetValue(requestId, out var result))
                {
                    return result.Status;
                }

                return _asyncConversionRequests.Contains(requestId)
                    ? XRCpuImage.AsyncConversionStatus.Processing
                    : XRCpuImage.AsyncConversionStatus.Failed;
            }
        }

        /// <summary>
        /// Releases all resources associated with an asynchronous GPU image conversion request.
        /// This includes disposing any persistent <see cref="NativeArray{T}"/> buffers
        /// allocated to store the converted image data.
        /// </summary>
        /// <param name="requestId">The unique identifier of the async conversion request to dispose.</param>
        public override void DisposeAsyncRequest(int requestId)
        {
            lock (_asyncConversionLock)
            {
                if (_asyncConversionResults.Remove(requestId, out var result))
                {
                    result.Data?.Dispose();
                }

                _asyncConversionRequests.Remove(requestId);
            }
        }

        /// <summary>
        /// Attempts to retrieve the converted image data for a completed asynchronous GPU conversion request.
        /// </summary>
        /// <param name="requestId">The unique identifier of the async conversion request.</param>
        /// <param name="dataPtr">
        /// When this method returns <see langword="true"/>, contains a pointer to the converted image data
        /// stored in native memory.
        /// </param>
        /// <param name="dataLength">
        /// When this method returns <see langword="true"/>, contains the number of bytes in the converted image data.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the request has completed successfully and the data is available;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// The returned pointer remains valid until <see cref="DisposeAsyncRequest"/> is called for the same request ID.
        /// Do not attempt to dispose or modify the underlying memory directly.
        /// </remarks>
        public override bool TryGetAsyncRequestData(
            int requestId, out IntPtr dataPtr, out int dataLength)
        {
            lock (_asyncConversionLock)
            {
                if (_asyncConversionResults.TryGetValue(requestId, out var result) &&
                    result is { Status: XRCpuImage.AsyncConversionStatus.Ready, Data: not null })
                {
                    unsafe
                    {
                        dataPtr = (IntPtr)result.Data.Value.GetUnsafeReadOnlyPtr();
                        dataLength = result.Data.Value.Length;
                    }

                    return true;
                }
            }

            dataPtr = IntPtr.Zero;
            dataLength = 0;
            return false;
        }
    }
}
