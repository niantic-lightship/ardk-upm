// Copyright 2022-2024 Niantic.

using UnityEngine;

namespace Niantic.Lightship.AR
{
    /// A utility class used to convert to and from raw arrays and matrices.
    internal static class MatrixConversionHelper
    {
        /// Converts a flat, column-major, float array to a Matrix4x4
        internal static Matrix4x4 InternalToMatrix4x4(float[] internalArray)
        {
            var matrix = Matrix4x4.zero;

            for (int col = 0; col < 4; col++)
            {
                for (int row = 0; row < 4; row++)
                {
                    // internalArray is column-major.
                    matrix[row, col] = internalArray[row + (col * 4)];
                }
            }

            return matrix;
        }

        /// Converts a flat, column-major, 9-element, float array to a Matrix4x4
        internal static Matrix4x4 Internal3x3ToMatrix4x4(float[] internalArray)
        {
            var matrix = Matrix4x4.identity;

            matrix[0, 0] = internalArray[0];
            matrix[1, 0] = internalArray[1];
            matrix[3, 0] = internalArray[2];

            matrix[0, 1] = internalArray[3];
            matrix[1, 1] = internalArray[4];
            matrix[3, 1] = internalArray[5];

            matrix[0, 3] = internalArray[6];
            matrix[1, 3] = internalArray[7];
            matrix[3, 3] = internalArray[8];

            return matrix;
        }

        /// Converts a flat affine display transform to a Matrix4x4.
        /// @remarks Represents a transformation matrix meant to be applied to column vectors
        /// ```
        /// | a  c  0  tx |
        /// | b  d  0  ty |
        /// | 0  0  1  0  |
        /// | 0  0  0  1  |
        /// ```
        internal static Matrix4x4 DisplayAffineToMatrix4x4(float[] affine)
        {
            var matrix = Matrix4x4.identity;
            // [row, col]
            matrix[0, 0] = affine[0]; // a
            matrix[1, 0] = affine[1]; // b
            matrix[0, 1] = affine[2]; // c
            matrix[1, 1] = affine[3]; // d
            matrix[0, 3] = affine[4]; // tx
            matrix[1, 3] = affine[5]; // ty

            return matrix;
        }

        /// Converts a generic Matrix4x4 to a flat, column-major array.
        internal static float[] Matrix4x4ToInternalArray(Matrix4x4 matrix)
        {
            float[] internalArray = new float[16];

            for (int col = 0; col < 4; col++)
            {
                for (int row = 0; row < 4; row++)
                {
                    // internalArray is column-major
                    internalArray[row + (col * 4)] = matrix[row, col];
                }
            }

            return internalArray;
        }

        /// Scales the provided matrix by the scale.
        internal static void ApplyScale(ref Matrix4x4 matrix, float scale)
        {
            // Apply scale to translation
            // [row, col]
            matrix[0, 3] *= scale;
            matrix[1, 3] *= scale;
            matrix[2, 3] *= scale;
        }

        /// Applies the inverse of the scale to the provided matrix.
        internal static void ApplyInverseScale(ref Matrix4x4 matrix, float scale)
        {
            // invert the scale and scale!
            ApplyScale(ref matrix, 1 / scale);
        }
    }
}
