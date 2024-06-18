// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Random = System.Random;
using static Niantic.Lightship.AR.Utilities.Logging.Log;

namespace Niantic.Lightship.AR.Utilities.Textures
{
    internal class NativeDataRepository
    {
        private const int MaxTextures = 16; // Same as ARKit
        private readonly Dictionary<int, NativeArray<byte>> _dataStore = new();
        private readonly Random _rand = new Random();

        private ulong _size;

        public ulong Size => _size;

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
                _size += (ulong)size;
            }

            return true;
        }

        /// <summary>
        /// Add data to be managed by this repository by copying it from the provided location.
        /// </summary>
        /// <param name="data">The array of data to copy into the repository.</param>
        /// <param name="size">The size of the data in bytes.</param>
        /// <param name="handle">A handle that can be used to get or remove this data from the repository.</param>
        /// <returns></returns>
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
                _size += (ulong)data.Length;
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
                _size -= (ulong)data.Length;
            }
        }
    }
}
