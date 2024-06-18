// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Occlusion;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems.ObjectDetection
{
    internal class NativeApi: IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            return Native.Construct(unityContext);
        }

        public void Start(IntPtr nativeProviderHandle)
        {
            Native.Start(nativeProviderHandle);
        }

        public void Stop(IntPtr nativeProviderHandle)
        {
            Native.Stop(nativeProviderHandle);
        }

        public void Destroy(IntPtr nativeProviderHandle)
        {
            Native.Destruct(nativeProviderHandle);
        }

        public void Configure(IntPtr nativeProviderHandle, uint targetFramerate, uint framesUntilSeen, uint framesUntilDiscarded)
        {
            Native.Configure(nativeProviderHandle, targetFramerate, framesUntilSeen, framesUntilDiscarded);
        }

        public bool HasMetadata(IntPtr nativeProviderHandle)
        {
            return Native.HasMetadata(nativeProviderHandle);
        }

        public bool TryGetCategoryNames(IntPtr nativeProviderHandle, out List<string> names)
        {
            var resourceHandle = Native.GetCategoryNames(nativeProviderHandle, out var arrayHandle, out var arrayLength);
            if (IntPtr.Zero == resourceHandle || IntPtr.Zero == arrayHandle)
            {
                names = new List<string>();
                return false;
            }

            names = new(arrayLength);

            unsafe
            {
                var categoryNa =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<NativeStringStruct>
                    (
                        arrayHandle.ToPointer(),
                        arrayLength,
                        Allocator.None
                    );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref categoryNa, AtomicSafetyHandle.GetTempMemoryHandle());
#endif

                foreach (var category in categoryNa)
                {
                    if (IntPtr.Zero == category.CharArrayIntPtr || category.ArrayLength <= 0)
                    {
                        Log.Error("Received invalid category name data from the native API.");
                        names = new List<string>();
                        return false;
                    }

                    var name = Marshal.PtrToStringAnsi(category.CharArrayIntPtr, (int) category.ArrayLength);
                    names.Add(name);
                }

                categoryNa.Dispose();
            }

            Native.DisposeResource(nativeProviderHandle, resourceHandle);

            return true;
        }

        public bool TryGetLatestFrameId(IntPtr nativeProviderHandle, out uint frameId)
        {
            return Native.TryGetLatestFrameId(nativeProviderHandle, out frameId);
        }

        public bool TryGetLatestDetections
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
        )
        {
            // Defaults
            interpolationMatrix = null;

            // Acquire detection results
            var success = Native.TryGetLatestDetections
            (
                nativeProviderHandle,
                out IntPtr resourceHandle,
                out numDetections,
                out numClasses,
                out IntPtr boundingBoxesPtr,
                out IntPtr probabilitiesPtr,
                out IntPtr trackingIdsPtr,
                out frameId,
                out frameTimestamp
            );

            if (!success || numDetections == 0)
            {
                boundingBoxes = Array.Empty<float>();
                probabilities = Array.Empty<float>();
                trackingIds = Array.Empty<uint>();
                return false;
            }

            if (boundingBoxesPtr == IntPtr.Zero || probabilitiesPtr == IntPtr.Zero)
            {
                Log.Error("Native returned invalid array pointer.");
                boundingBoxes = Array.Empty<float>();
                probabilities = Array.Empty<float>();
                trackingIds = Array.Empty<uint>();
                return false;
            }

            // Copy the results to managed memory
            unsafe
            {
                var boundingBoxesNa =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float>
                    (
                        (void*)boundingBoxesPtr,
                        (int)numDetections * 4,
                        Allocator.None
                    );

                var probabilitiesNa =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float>
                    (
                        (void*)probabilitiesPtr,
                        (int)numDetections * (int)numClasses,
                        Allocator.None
                    );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle
                (
                    ref boundingBoxesNa,
                    AtomicSafetyHandle.GetTempMemoryHandle()
                );

                NativeArrayUnsafeUtility.SetAtomicSafetyHandle
                (
                    ref probabilitiesNa,
                    AtomicSafetyHandle.GetTempMemoryHandle()
                );
#endif
                boundingBoxes = boundingBoxesNa.ToArray();
                probabilities = probabilitiesNa.ToArray();

                // Tracking ids will not be available if tracking is disabled
                if (trackingIdsPtr != IntPtr.Zero)
                {
                    var trackingIdsNa =
                        NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<uint>
                        (
                            (void*)trackingIdsPtr,
                            (int)numDetections,
                            Allocator.None
                        );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle
                    (
                        ref trackingIdsNa,
                        AtomicSafetyHandle.GetTempMemoryHandle()
                    );
#endif

                    trackingIds = trackingIdsNa.ToArray();
                }
                else
                {
                    trackingIds = Array.Empty<uint>();
                }
            }

            // Additional work with this native resource
            if (resourceHandle != IntPtr.Zero)
            {
                // Whether interpolation is requested
                if (interpolate)
                {
                    // Acquire the current device pose
                    var poseAcquired = InputReader.TryGetPose(out var poseMatrix);

                    var gotInterpolationMatrix = false;

                    if (poseAcquired)
                    {
                        // Convert the pose to native ARDK format
                        var poseArray = MatrixConversionHelper.Matrix4x4ToInternalArray(poseMatrix.FromUnityToArdk());

                        // Allocate the intermediate array
                        float[] outMatrix = new float[9];

                        // Calculate and copy the matrix
                        gotInterpolationMatrix = Native.TryCalculateInterpolationMatrix
                        (
                            nativeProviderHandle,
                            resourceHandle,
                            poseArray,
                            XRDisplayContext.OccludeeEyeDepth,
                            outMatrix
                        );

                        if (gotInterpolationMatrix)
                        {
                            // Convert the matrix to Unity
                            interpolationMatrix = new Matrix4x4
                            (
                                new Vector4(outMatrix[0], outMatrix[1], outMatrix[2], 0),
                                new Vector4(outMatrix[3], outMatrix[4], outMatrix[5], 0),
                                new Vector4(outMatrix[6], outMatrix[7], outMatrix[8], 0),
                                new Vector4(0, 0, 0, 1)
                            );
                        }
                    }

                    if (!gotInterpolationMatrix)
                    {
                        Log.Error("Could not calculate an appropriate interpolation matrix.");
                    }
                }

                // Release the resource
                Native.DisposeResource(nativeProviderHandle, resourceHandle);
            }

            return true;
        }

        public bool TryCalculateViewportMapping
        (
            IntPtr nativeProviderHandle,
            int viewportWidth,
            int viewportHeight,
            ScreenOrientation orientation,
            out Matrix4x4 matrix
        )
        {
            var outMatrix = new float[9];
            var gotMapping =
                Native.TryCalculateViewportMapping
                (
                    nativeProviderHandle,
                    viewportWidth,
                    viewportHeight,
                    (int)orientation.FromUnityToArdk(),
                    outMatrix
                );

            if (gotMapping)
            {
                matrix = new Matrix4x4
                (
                    new Vector4(outMatrix[0], outMatrix[1], outMatrix[2], 0),
                    new Vector4(outMatrix[3], outMatrix[4], outMatrix[5], 0),
                    new Vector4(outMatrix[6], outMatrix[7], outMatrix[8], 0),
                    new Vector4(0, 0, 0, 1)
                );

                return true;
            }

            matrix = Matrix4x4.identity;
            return false;
        }

        private static class Native
        {
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_Construct")]
            public static extern IntPtr Construct(IntPtr unityContext);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_Start")]
            public static extern void Start(IntPtr nativeProviderHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_Stop")]
            public static extern void Stop(IntPtr nativeProviderHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_Configure")]
            public static extern void Configure
            (
                IntPtr nativeProviderHandle,
                uint frameRate,
                uint framesUntilSeen,
                uint framesUntilDiscarded
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_Destruct")]
            public static extern void Destruct(IntPtr nativeProviderHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_HasMetadata")]
            public static extern bool HasMetadata(IntPtr nativeProviderHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_GetClassNames")]
            public static extern IntPtr GetCategoryNames(IntPtr nativeProviderHandle, out IntPtr categoryStructs, out int numCategories);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_ReleaseResource")]
            public static extern IntPtr DisposeResource(IntPtr nativeProviderHandle, IntPtr resourceHandle);

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_TryGetLatestFrameId")]
            public static extern bool TryGetLatestFrameId(IntPtr nativeProviderHandle, out uint frameId);

            /// Returns the ExternalHandle to the memoryBuffer
            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_TryGetLatestDetections")]
            public static extern bool TryGetLatestDetections
            (
                IntPtr objectDetectionApiHandle,
                out IntPtr resourceHandle,
                out uint numDetections,
                out uint numClasses,
                out IntPtr boxLocationsPtr,
                out IntPtr probabilitiesPtr,
                out IntPtr trackingIdsPtr,
                out uint frameId,
                out ulong frameTimestamp
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_TryCalculateInterpolationMatrix")]
            public static extern bool TryCalculateInterpolationMatrix
            (
                IntPtr objectDetectionApiHandle,
                IntPtr resourceHandle,
                float[] poseIn,
                float backProjectionPlane,
                float[] matrixOut
            );

            [DllImport(LightshipPlugin.Name, EntryPoint = "Lightship_ARDK_Unity_ObjectDetectionProvider_TryCalculateViewportMapping")]
            public static extern bool TryCalculateViewportMapping
            (
                IntPtr objectDetectionApiHandle,
                int viewportWidth,
                int viewportHeight,
                int orientation,
                float[] matrixOut
            );
        }
    }
}
