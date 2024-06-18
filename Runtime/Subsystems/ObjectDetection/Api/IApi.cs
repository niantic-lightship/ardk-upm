// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems.ObjectDetection
{
    internal interface IApi
    {
        IntPtr Construct(IntPtr unityContext);

        void Destroy(IntPtr nativeProviderHandle);

        void Start(IntPtr nativeProviderHandle);

        void Stop(IntPtr nativeProviderHandle);

        void Configure(IntPtr nativeProviderHandle, uint targetFramerate, uint framesUntilSeen, uint framesUntilDiscarded);

        bool HasMetadata(IntPtr nativeProviderHandle);

        bool TryGetLatestFrameId(IntPtr nativeProviderHandle, out uint id);

        bool TryGetCategoryNames(IntPtr nativeProviderHandle, out List<string> names);

        /// <summary>
        ///
        /// </summary>
        /// <param name="nativeProviderHandle"></param>
        /// <param name="numDetections"></param>
        /// <param name="numClasses"></param>
        /// <param name="boundingBoxes"></param>
        /// <param name="probabilities">
        ///     Vector of floats, where the (n * numClasses + c)th element is the confidence
        ///     of the detection of the cth class for the box with ID n.
        /// </param>
        /// <param name="frameId"></param>
        /// <param name="frameTimestamp"></param>
        /// <param name="interpolate">
        ///     Whether the caller requires a transformation that aligns the results with the current device pose.
        /// </param>
        /// <param name="interpolationMatrix">The matrix that aligns the results with the current device pose, if requested.</param>
        /// <returns></returns>
        bool TryGetLatestDetections
        (
            IntPtr nativeProviderHandle,
            out uint numDetections,
            out uint numClasses,
            out float[] boundingBoxes,
            out float[] probabilities,
            out uint[] trackingIds,
            out uint frameId,
            out ulong frameTimestamp,
            bool interpolate,
            out Matrix4x4? interpolationMatrix
        );

        /// <summary>
        /// Calculates a matrix that transforms normalized coordinates from the object detection inference container to the viewport.
        /// </summary>
        /// <param name="nativeProviderHandle">Handle to the native provider instance.</param>
        /// <param name="viewportWidth">The width of the viewport.</param>
        /// <param name="viewportHeight">The height of the viewport.</param>
        /// <param name="orientation">The orientation of the viewport.</param>
        /// <param name="matrix">The resulting transformation matrix.</param>
        /// <returns>Whether the matrix could be calculated.</returns>
        public bool TryCalculateViewportMapping
        (
            IntPtr nativeProviderHandle,
            int viewportWidth,
            int viewportHeight,
            ScreenOrientation orientation,
            out Matrix4x4 matrix
        );
    }
}
