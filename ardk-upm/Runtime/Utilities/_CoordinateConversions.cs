using UnityEngine;

namespace Niantic.Lightship.AR.Utilities
{
    /// Utility methods to convert between ARDK and Unity coordinate frames
    internal static class _CoordinateConversions
    {
        private static readonly Vector3 _signVector_y = new Vector3(1, -1, 1);

        private static readonly Vector3 _signVector_z = new Vector3(1, 1, -1);

        public static Vector3 FromOpenGLToUnity(this Vector3 point)
        {
            return Vector3.Scale(point, _signVector_z);
        }

        public static Vector3 FromUnityToOpenGL(this Vector3 point)
        {
            return point.FromOpenGLToUnity();
        }

        public static Vector3 FromArdkToUnity(this Vector3 point)
        {
            return Vector3.Scale(point, _signVector_y);
        }

        public static Vector3 FromUnityToArdk(this Vector3 point)
        {
            // Conversion is just switching signs, so the same vector scaling
            // works both ways
            return point.FromArdkToUnity();
        }

        private static readonly Matrix4x4 _signMatrix4x4_y =
            new Matrix4x4
            (
                new Vector4(1, 0, 0, 0),
                new Vector4(0, -1, 0, 0),
                new Vector4(0, 0, 1, 0),
                new Vector4(0, 0, 0, 1)
            );

        public static Matrix4x4 FromArdkToUnity(this Matrix4x4 matrix)
        {
            // Sy [R|T] Sy
            //    [0|1]
            return _signMatrix4x4_y * matrix * _signMatrix4x4_y;
        }

        public static Matrix4x4 FromUnityToArdk(this Matrix4x4 matrix)
        {
            return matrix.FromArdkToUnity();
        }

        private static readonly Matrix4x4 _signMatrix4x4_z =
            new Matrix4x4
            (
                new Vector4(1, 0, 0, 0),
                new Vector4(0, 1, 0, 0),
                new Vector4(0, 0, -1, 0),
                new Vector4(0, 0, 0, 1)
            );

        public static Matrix4x4 FromOpenGLToUnity(this Matrix4x4 matrix)
        {
            // Sz [R|T] Sz
            //    [0|1]
            return _signMatrix4x4_z * matrix * _signMatrix4x4_z;
        }

        public static Matrix4x4 FromUnityToOpenGL(this Matrix4x4 matrix)
        {
            return matrix.FromOpenGLToUnity();
        }
    }
}
