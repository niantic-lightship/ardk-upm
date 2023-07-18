using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    public static class _CameraMath
    {
        /// Returns an affine transform for converting between normalized image coordinates and a
        /// coordinate space appropriate for rendering the camera image onscreen.
        /// @note The width and height arguments must conform with the specified viewport orientation.
        /// @param imageWidth The width of the raw AR background image in pixels.
        /// @param imageHeight The height of the raw AR background image in pixels.
        /// @param viewportWidth The width of the viewport in pixels.
        /// @param viewportHeight The height of the viewport in pixels.
        /// @param viewportOrientation The orientation of the viewport.
        /// @param invertVertically
        ///     Mirror the image across the X axis. This reverses the order of the horizontal rows,
        ///     flipping the image upside down.
        /// @returns An affine 4x4 transformation matrix.
        public static Matrix4x4 CalculateDisplayMatrix
        (
            int imageWidth,
            int imageHeight,
            int viewportWidth,
            int viewportHeight,
            ScreenOrientation viewportOrientation,
            bool invertVertically = true,
            MatrixLayout layout = MatrixLayout.ColumnMajor,
            bool reverseRotation = false
        )
        {
            // Infer image orientation
            var imageOrientation = imageWidth > imageHeight
                ? ScreenOrientation.LandscapeLeft
                : ScreenOrientation.Portrait;

            // Calculate the matrix that fits the source to the viewport
            var fit = _AffineMath.Fit
            (
                imageWidth,
                imageHeight,
                imageOrientation,
                viewportWidth,
                viewportHeight,
                viewportOrientation,
                reverseRotation
            );

            // We invert the y coordinate because Unity's 2D coordinate system is
            // upside-down compared to the native systems.
            var result = invertVertically ? fit * _AffineMath.InvertVertical : fit;

            // Matrices in Unity are column major. The matrix is used from the left
            // when multiplying on the CPU, i.e. displayTransform * uv.
            // However, the built-in ARFoundation background shaders employ different
            // layouts. They use the column major format on Android, while on iOS
            // the matrix is row major and uv coordinates are multiplied from the right.
            return layout == MatrixLayout.ColumnMajor ? result.transpose : result;
        }

        /// Returns a transform matrix appropriate for rendering 3D content to match the image
        /// captured by the camera, using the specified parameters.
        /// @param intrinsics The intrinsics of the physical camera.
        /// @param cameraParams Presentation params of the virtual camera.
        /// @param useOpenGLConvention Whether to construct an OpenGL-like projection matrix.
        public static Matrix4x4 CalculateProjectionMatrix
        (
            XRCameraIntrinsics intrinsics,
            XRCameraParams cameraParams,
            bool useOpenGLConvention = true
        )
        {
            // Get the viewport resolution in landscape
            var viewportWidthLandscape = cameraParams.screenWidth;
            var viewportHeightLandscape = cameraParams.screenHeight;
            if (cameraParams.screenOrientation == ScreenOrientation.Portrait)
                (viewportWidthLandscape, viewportHeightLandscape) = (viewportHeightLandscape, viewportWidthLandscape);

            // Extract image resolution
            float imageWidth = intrinsics.resolution.x;
            float imageHeight = intrinsics.resolution.y;

            // Calculate scaling
            var scale = viewportHeightLandscape / (viewportWidthLandscape / imageWidth * imageHeight);

            // Calculate the cropped resolution of the image in landscapes
            var croppedFrame = new Vector2
            (
                // The image fills the longer axis of the viewport
                x: imageWidth,

                // The image is cropped on the shorter axis of the viewport
                y: imageHeight * scale
            );

            // Get the corners of the captured image
            var right = imageWidth - 1;
            var top = imageHeight - 1;
            var left = right - 2.0f * intrinsics.principalPoint.x;
            var bottom = top - 2.0f * intrinsics.principalPoint.y;

            // Calculate the image origin in landscape
            var origin = new Vector2
            (
                x: left / croppedFrame.x,
                y: -bottom / croppedFrame.y
            );

            // Rotate the image origin to the specified orientation
            origin = RotateVector(origin,
                (float) _AffineMath.GetRadians(cameraParams.screenOrientation, ScreenOrientation.LandscapeLeft));

            // Fx and Fy are identical for square pixels
            var focalLength = intrinsics.focalLength.x;

            var f = new Vector2
            (
                x: 1.0f / (croppedFrame.x * 0.5f / focalLength),
                y: 1.0f / (croppedFrame.y * 0.5f / focalLength)
            );

            // Swap for portrait
            if (cameraParams.screenOrientation == ScreenOrientation.Portrait ||
                cameraParams.screenOrientation == ScreenOrientation.PortraitUpsideDown)
            {
                (f.x, f.y) = (f.y, f.x);
            }

            var projection = Matrix4x4.zero;
            projection[0, 0] = f.x;
            projection[1, 1] = f.y;
            projection[0, 2] = origin.x;
            projection[1, 2] = origin.y;
            projection[3, 2] = -1.0f;

            // Direct3D-like: The coordinate is 0 at the top and increases downward.
            // This applies to Direct3D, Metal and consoles.
            // OpenGL-like: The coordinate is 0 at the bottom and increases upward.
            // This applies to OpenGL and OpenGL ES.
            if (useOpenGLConvention)
            {
                projection[2, 2] = -(cameraParams.zFar + cameraParams.zNear) / (cameraParams.zFar - cameraParams.zNear);
                projection[2, 3] = -2.0f * (cameraParams.zFar * cameraParams.zNear) / (cameraParams.zFar - cameraParams.zNear);
            }
            else
            {
                projection[2, 2] = cameraParams.zFar / (cameraParams.zNear - cameraParams.zFar);
                projection[2, 3] = cameraParams.zFar * cameraParams.zNear / (cameraParams.zNear - cameraParams.zFar);
            }

            return projection;
        }

        /// Rotates a Vector2 by the specified angle in radians.
        private static Vector2 RotateVector(Vector2 vector, float radians)
        {
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);

            float x = vector.x;
            float y = vector.y;
            vector.x = cos * x - sin * y;
            vector.y = sin * x + cos * y;

            return vector;
        }

        /// Options to specify the expected layout of a matrix.
        public enum MatrixLayout
        {
            ColumnMajor = 0,
            RowMajor = 1
        }

        // Camera is always considered LandscapeLeft
        public static Quaternion DisplayToCameraRotation(ScreenOrientation displayOrientation)
        {
            switch (displayOrientation)
            {
                case ScreenOrientation.Portrait:
                    return Quaternion.Euler(0, 0, 90);
                case ScreenOrientation.LandscapeLeft:
                    return Quaternion.Euler(0, 0, 0);
                case ScreenOrientation.PortraitUpsideDown:
                    return Quaternion.Euler(0, 0, -90);
                case ScreenOrientation.LandscapeRight:
                    return Quaternion.Euler(0, 0, 180);

                default:
                    throw new Exception("Orientation value out of bounds");
            }
        }

        // Camera is always considered LandscapeLeft
        public static Quaternion CameraToDisplayRotation(ScreenOrientation displayOrientation)
        {
            switch (displayOrientation)
            {
                case ScreenOrientation.Portrait:
                    return Quaternion.Euler(0, 0, -90);
                case ScreenOrientation.LandscapeLeft:
                    return Quaternion.Euler(0, 0, 0);
                case ScreenOrientation.PortraitUpsideDown:
                    return Quaternion.Euler(0, 0, 90);
                case ScreenOrientation.LandscapeRight:
                    return Quaternion.Euler(0, 0, 180);

                // In the case of an Unknown orientation, default to Portrait
                default:
                    Debug.LogWarning($"Got a ScreenOrientation {displayOrientation}, defaulting to Portrait");
                    return Quaternion.Euler(0, 0, -90);
            }
        }
    }
}
