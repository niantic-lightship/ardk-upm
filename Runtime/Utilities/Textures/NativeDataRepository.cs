// Copyright 2022-2025 Niantic.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Random = System.Random;
using static Niantic.Lightship.AR.Utilities.Logging.Log;

namespace Niantic.Lightship.AR.Utilities.Textures
{
    internal class NativeDataRepository
    {
        private const int MaxTextures = 16; // Same as ARKit
        private readonly Dictionary<int, NativeArray<byte>> _dataStore = new();
        private readonly Random _rand = new();
        public ulong Size { get; private set; }

        /// <summary>
        /// Add data to be managed by this repository by copying it from the provided location.
        /// </summary>
        /// <param name="data">The array of data to copy into the repository.</param>
        /// <param name="size">The size of the data in bytes.</param>
        /// <param name="handle">A handle that can be used to get or remove this data from the repository.</param>
        /// <returns></returns>
        public bool TryCopyFrom(IntPtr data, int size, out int handle)
        {
            if (_dataStore.Count >= MaxTextures)
            {
                Error("Too many XRCpuImages have been acquired. Remember to dispose XRCpuImages after they are used.");
                handle = 0;
                return false;
            }

            unsafe
            {
                // TODO: The allocation and copy take ~0.1 ms max (usually less). No huge improvements to be
                // gained here, but it could be optimized by using memory pointed to by the data param directly,
                // but that requires native Lightship to promise that the buffer will not be changed
                // (it won't be disposed at least, as C# holds an ExternalHandle to the buffer).
                var dataDestination = new NativeArray<byte>(size, Allocator.Persistent);
                Buffer.MemoryCopy((void*)data, dataDestination.GetUnsafePtr(), size, size);

                handle = _rand.Next();
                _dataStore.Add(handle, dataDestination);
                Size += (ulong)size;
            }

            return true;
        }

        /// <summary>
        /// Add data to be managed by this repository by copying it from the provided location.
        /// </summary>
        /// <param name="data">The array of data to copy into the repository.</param>
        /// <param name="handle">A handle that can be used to get or remove this data from the repository.</param>
        /// <returns>Whether the data was successfully copied.</returns>
        public bool TryCopyFrom(byte[] data, out int handle)
        {
            if (_dataStore.Count >= MaxTextures)
            {
                Error("Too many XRCpuImages have been acquired. Remember to dispose XRCpuImages after they are used.");
                handle = 0;
                return false;
            }

            unsafe
            {
                var dataDestination = new NativeArray<byte>(data.Length, Allocator.Persistent);
                Marshal.Copy(data, 0, (IntPtr)dataDestination.GetUnsafePtr(), data.Length);

                handle = _rand.Next();
                _dataStore.Add(handle, dataDestination);
                Size += (ulong)data.Length;
            }

            return true;
        }

        /// <summary>
        /// Add data to be managed by this repository by copying it from the provided location.
        /// </summary>
        /// <param name="data">The array of data to copy into the repository.</param>
        /// <param name="handle">A handle that can be used to get or remove this data from the repository.</param>
        /// <returns>Whether the data was successfully copied.</returns>
        public bool TryCopyFrom<T>(T[] data, out int handle)
        {
            if (_dataStore.Count >= MaxTextures)
            {
                Error("Too many XRCpuImages have been acquired. Remember to dispose XRCpuImages after they are used.");
                handle = 0;
                return false;
            }

            // Allocate the destination container
            int sizeInBytes = Marshal.SizeOf(typeof(T)) * data.Length;
            var dataDestination = new NativeArray<byte>(sizeInBytes, Allocator.Persistent);
            var gcHandle = default(GCHandle);

            unsafe
            {
                try
                {
                    // Pin the source data
                    gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

                    // Get the source address
                    var srcPtr = gcHandle.AddrOfPinnedObject().ToPointer();

                    // Get the destination address
                    var destPtr = ((IntPtr)dataDestination.GetUnsafePtr()).ToPointer();

                    // Copy bytes
                    Buffer.MemoryCopy(srcPtr, destPtr, sizeInBytes, sizeInBytes);
                }
                finally
                {
                    // Free the pinned handle
                    if (gcHandle != default)
                    {
                        gcHandle.Free();
                    }
                }
            }

            // Create a handle to the data in the store
            handle = _rand.Next();
            _dataStore.Add(handle, dataDestination);
            Size += (ulong)sizeInBytes;

            return true;
        }

        /// <summary>
        /// Add data to be managed by this repository by copying it from the provided planes.
        /// Use this when working with multi-planar image with non-sequential memory layout.
        /// </summary>
        /// <param name="planes">Array of data pointers, one for each plane.</param>
        /// <param name="sizes">Array of sizes corresponding to each plane in bytes.</param>
        /// <param name="handle">A handle that can be used to get or remove this data from the repository.</param>
        /// <returns>Whether the data was successfully copied.</returns>
        public bool TryCopyFrom(IntPtr[] planes, int[] sizes, out int handle)
        {
            if (_dataStore.Count >= MaxTextures)
            {
                Error("Too many XRCpuImages have been acquired. Remember to dispose XRCpuImages after they are used.");
                handle = 0;
                return false;
            }

            if (planes == null || sizes == null || planes.Length != sizes.Length)
            {
                Error("Plane pointer array and size array must be non-null and of equal length.");
                handle = 0;
                return false;
            }

            // Aggregate the total size of all planes
            int totalSize = 0;
            for (int i = 0; i < sizes.Length; i++)
            {
                totalSize += sizes[i];
            }

            unsafe
            {
                var dataDestination = new NativeArray<byte>(totalSize, Allocator.Persistent);
                byte* destPtr = (byte*)dataDestination.GetUnsafePtr();
                int offset = 0;

                for (int i = 0; i < planes.Length; i++)
                {
                    Buffer.MemoryCopy((void*)planes[i], destPtr + offset, totalSize - offset, sizes[i]);
                    offset += sizes[i];
                }

                handle = _rand.Next();
                _dataStore.Add(handle, dataDestination);
                Size += (ulong)totalSize;
            }

            return true;
        }

        public bool TryGetData(int handle, out NativeArray<byte> data)
        {
            return _dataStore.TryGetValue(handle, out data);
        }

        public void Dispose(int handle)
        {
            if (_dataStore.Remove(handle, out NativeArray<byte> data))
            {
                data.Dispose();
                Size -= (ulong)data.Length;
            }
        }
    }
}
