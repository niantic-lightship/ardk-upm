// Copyright 2022-2024 Niantic.
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;

namespace Niantic.Lightship.AR.Utilities
{
    internal static class Matrix4x4Extensions
    {
        public static float[] ToRowMajorArray(this Matrix4x4 matrix)
        {
            return new[]
            {
                matrix.m00, matrix.m01, matrix.m02, matrix.m03, matrix.m10, matrix.m11, matrix.m12, matrix.m13,
                matrix.m20, matrix.m21, matrix.m22, matrix.m23, matrix.m30, matrix.m31, matrix.m32, matrix.m33,
            };
        }

        public static float[] ToColumnMajorArray(this Matrix4x4 matrix)
        {
            return new[]
            {
                matrix.m00, matrix.m10, matrix.m20, matrix.m30, matrix.m01, matrix.m11, matrix.m21, matrix.m31,
                matrix.m02, matrix.m12, matrix.m22, matrix.m32, matrix.m03, matrix.m13, matrix.m23, matrix.m33
            };
        }

        public static Matrix4x4 FromColumnMajorArray(this float[] array)
        {
            return new Matrix4x4
            (
                new Vector4(array[0], array[1], array[2], array[3]),
                new Vector4(array[4], array[5], array[6], array[7]),
                new Vector4(array[8], array[9], array[10], array[11]),
                new Vector4(array[12], array[13], array[14], array[15])
            );
        }
    }
}
