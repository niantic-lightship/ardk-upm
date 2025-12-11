// Copyright 2022-2025 Niantic.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Niantic.Lightship.AR.Common
{
    [BurstCompile]
    internal static class BurstImageUtils
    {
        // Delegate signatures
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void RepackDelegate(byte* src, byte* dst, int pixelCount);

        private static readonly unsafe FunctionPointer<RepackDelegate> s_reducePtr =
            BurstCompiler.CompileFunctionPointer<RepackDelegate>(ReduceRgbaToRgb);

        private static readonly unsafe FunctionPointer<RepackDelegate> s_expandPtr =
            BurstCompiler.CompileFunctionPointer<RepackDelegate>(ExpandRgbToRgba);

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(RepackDelegate))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void ReduceRgbaToRgb(byte* src, byte* dst, int pixelCount)
        {
            for (int i = 0; i < pixelCount; i++, src += 4, dst += 3)
            {
                dst[0] = src[0];
                dst[1] = src[1];
                dst[2] = src[2];
            }
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(RepackDelegate))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void ExpandRgbToRgba(byte* src, byte* dst, int pixelCount)
        {
            for (int i = 0; i < pixelCount; i++, src += 3, dst += 4)
            {
                dst[0] = src[0];
                dst[1] = src[1];
                dst[2] = src[2];
                dst[3] = 255;
            }
        }

        /// <summary>
        /// Invokes the Burst-compiled routine that copies an RGBA image into an RGB buffer,
        /// stripping the alpha channel from each pixel. The source must contain 4 bytes per pixel,
        /// and the destination must have space for 3 bytes per pixel.
        /// </summary>
        /// <param name="src">Native array containing RGBA pixel data (read-only).</param>
        /// <param name="dst">Pointer to the destination RGB buffer in memory.</param>
        public static unsafe void InvokeReduceRgbaToRgb(NativeArray<byte> src, IntPtr dst)
        {
            s_reducePtr.Invoke(
                (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(src),
                (byte*)dst.ToPointer(),
                src.Length / 4);
        }

        /// <summary>
        /// Invokes the Burst-compiled routine that copies an RGBA image into an RGB buffer,
        /// stripping the alpha channel from each pixel. The source must contain 4 bytes per pixel,
        /// and the destination must have space for 3 bytes per pixel.
        /// </summary>
        /// <param name="src">Native array containing RGBA pixel data (read-only).</param>
        /// <param name="dst">Native array that receives the packed RGB pixel data.</param>
        public static unsafe void InvokeReduceRgbaToRgb(NativeArray<byte> src, NativeArray<byte> dst)
        {
            s_reducePtr.Invoke(
                (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(src),
                (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(dst),
                src.Length / 4);
        }

        /// <summary>
        /// Invokes the Burst-compiled routine that expands an RGB image into an RGBA buffer,
        /// appending an opaque alpha value (255) to every pixel. The source must contain
        /// 3 bytes per pixel, and the destination must have space for 4 bytes per pixel.
        /// </summary>
        /// <param name="src">Native array containing RGB pixel data (read-only).</param>
        /// <param name="dst">Pointer to the destination RGBA buffer in memory.</param>
        public static unsafe void InvokeExpandRgbToRgba(NativeArray<byte> src, IntPtr dst)
        {
            s_expandPtr.Invoke(
                (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(src),
                (byte*)dst.ToPointer(),
                src.Length / 3);
        }

        /// <summary>
        /// Invokes the Burst-compiled routine that expands an RGB image into an RGBA buffer,
        /// appending an opaque alpha value (255) to every pixel. The source must contain
        /// 3 bytes per pixel, and the destination must have space for 4 bytes per pixel.
        /// </summary>
        /// <param name="src">Native array containing RGB pixel data (read-only).</param>
        /// <param name="dst">Native array that receives the expanded RGBA pixel data.</param>
        public static unsafe void InvokeExpandRgbToRgba(NativeArray<byte> src, NativeArray<byte> dst)
        {
            s_expandPtr.Invoke(
                (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(src),
                (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(dst),
                src.Length / 3);
        }
    }
}
