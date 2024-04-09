// Copyright 2022-2024 Niantic.

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// Contains a native voxel buffers for positions and colors.
    /// </summary>
    public struct XRScanningVoxelData: IEquatable<XRScanningVoxelData>
    {
        /// <summary>
        /// Contains the world position of each point in the voxel cloud.
        /// </summary>
        public readonly NativeArray<Vector3> Positions
        {
            get => _positions;
        }

        private NativeArray<Vector3> _positions;

        /// <summary>
        /// Continas the RGBA color of each point in the voxel cloud.
        /// </summary>
        public readonly NativeArray<Color32> Colors
        {
            get => _colors;
        }

        private NativeArray<Color32> _colors;

        internal IntPtr nativeHandle { get; private set; }

        public XRScanningVoxelData(IntPtr positionPtr, IntPtr colorsPtr, int pointCount, IntPtr nativeHandle)
        {
            unsafe
            {
                _positions = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Vector3>(positionPtr.ToPointer(),
                    pointCount, Allocator.None);
                _colors = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Color32>(colorsPtr.ToPointer(),
                    pointCount, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
// See https://forum.unity.com/threads/convertexistingdatatonativearray-and-atomicsafetyhandle.878575/#post-7975632
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref _positions, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref _colors, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif
            }

            this.nativeHandle = nativeHandle;
        }

        public readonly bool Equals(XRScanningVoxelData other)
        {
            return Positions.Equals(other.Positions) && Colors.Equals(other.Colors)
                && nativeHandle.Equals(other.nativeHandle);
        }

        public override readonly bool Equals(object obj)
        {
            return obj is XRScanningVoxelData other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Positions, Colors, nativeHandle);
        }
    }
}
