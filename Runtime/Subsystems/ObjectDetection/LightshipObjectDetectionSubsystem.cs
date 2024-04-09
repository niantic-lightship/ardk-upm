// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.ObjectDetection
{
    public class LightshipObjectDetectionSubsystem: XRObjectDetectionSubsystem, ISubsystemWithMutableApi<IApi>
    {
        internal const uint MaxRecommendedFrameRate = 20;

        /// <summary>
        /// Register the Lightship object detection subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            Log.Info(nameof(LightshipObjectDetectionSubsystem)+"."+nameof(Register));
            const string id = "Lightship-ObjectDetection";
            var cinfo = new XRObjectDetectionSubsystemCinfo()
            {
                id = id,
                providerType = typeof(LightshipObjectDetectionProvider),
                subsystemTypeOverride = typeof(LightshipObjectDetectionSubsystem),
                objectDetectionSupportedDelegate = () => Supported.Supported
            };

            XRObjectDetectionSubsystem.Register(cinfo);
        }

        void ISubsystemWithMutableApi<IApi>.SwitchApiImplementation(IApi api)
        {
            ((LightshipObjectDetectionProvider) provider).SwitchApiImplementation(api);
        }

        void ISubsystemWithMutableApi<IApi>.SwitchToInternalMockImplementation()
        {
            throw new NotImplementedException();
        }

        internal class LightshipObjectDetectionProvider : Provider
        {
            public class CategoryDirectory
            {
                public readonly List<string> CategoryNames;

                private readonly Dictionary<string, int> _categoryIndices;

                public CategoryDirectory(List<string> names)
                {
                    CategoryNames = names;
                    _categoryIndices = new Dictionary<string, int>();

                    for (int i = 0; i < names.Count; i++)
                    {
                        _categoryIndices.Add(CategoryNames[i], i);
                    }
                }

                public bool TryGetCategory(int index, out string name)
                {
                    if (index >= 0 && index < CategoryNames.Count)
                    {
                        name = CategoryNames[index];
                        return true;
                    }

                    Log.Error($"No category with id '{index}' exists.");
                    name = null;
                    return false;
                }

                public bool TryGetIndex(string category, out int index)
                {
                    if (_categoryIndices.TryGetValue(category, out index))
                    {
                        return true;
                    }

                    Log.Error($"No category named '{category} exists.");
                    return false;
                }
            }

            public class DisplayHelper
            {
                private readonly LightshipObjectDetectionProvider _provider;

                private int _lastSeenWidth;
                private int _lastSeenHeight;
                private ScreenOrientation _lastSeenOrientation;
                private Matrix4x4 _lastViewportMapping;

                public bool TryCalculateViewportMapping
                (
                    int viewportWidth,
                    int viewportHeight,
                    ScreenOrientation orientation,
                    out Matrix4x4 matrix
                )
                {
                    var viewportChanged =
                        _lastSeenWidth != viewportWidth || _lastSeenHeight != viewportHeight ||
                        _lastSeenOrientation != orientation;

                    if (!viewportChanged)
                    {
                        matrix = _lastViewportMapping;
                        return true;
                    }

                    var success =
                        _provider.TryCalculateViewportMapping(viewportWidth, viewportHeight, orientation, out matrix);

                    if (success)
                    {
                        _lastSeenWidth = viewportWidth;
                        _lastSeenHeight = viewportHeight;
                        _lastSeenOrientation = orientation;
                        _lastViewportMapping = matrix;
                    }

                    return success;
                }

                public DisplayHelper(LightshipObjectDetectionProvider provider)
                {
                    _provider = provider;
                }
            }

            /// <summary>
            /// The handle to the native version of the provider
            /// </summary>
            private IntPtr _nativeProviderHandle;
            private IApi _api;

            private CategoryDirectory _categoryDirectory;
            private DisplayHelper _displayHelper;

            private uint _targetFrameRate = MaxRecommendedFrameRate;
            private ulong _latestTimestamp = 0;

            /// <summary>
            /// Property to get or set the target frame rate for the semantic segmentation feature.
            /// </summary>
            /// <value>
            /// The requested target frame rate in frames per second.
            /// </value>
            public override uint TargetFrameRate
            {
                get => _targetFrameRate;
                set
                {
                    if (value <= 0)
                    {
                        Log.Error("Target frame rate value must be greater than zero.");
                        return;
                    }

                    if (_targetFrameRate != value)
                    {
                        _targetFrameRate = value;
                        Configure();
                    }
                }
            }

            public override bool IsMetadataAvailable
            {
                get
                {
                    if (_nativeProviderHandle.IsValidHandle())
                    {
                        return _api.HasMetadata(_nativeProviderHandle);
                    }

                    return false;
                }
            }

            public override uint? LatestFrameId
            {
                get
                {
                    if (_nativeProviderHandle.IsValidHandle())
                    {
                        if (_api.TryGetLatestFrameId(_nativeProviderHandle, out uint id))
                        {
                            return id;
                        }
                    }

                    return null;
                }
            }
            
            public ulong LatestTimestamp
            {
                get
                {
                    return _latestTimestamp;
                }
            }

            public LightshipObjectDetectionProvider() : this(new NativeApi()) { }

            /// <summary>
            /// Construct the implementation provider.
            /// </summary>
            public LightshipObjectDetectionProvider(IApi api)
            {
                Log.Debug("Constructing LightshipObjectDetectionSubsystem.LightshipObjectDetectionProvider...");

                _api = api;
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
                _nativeProviderHandle = _api.Construct(LightshipUnityContext.UnityContextHandle);
#endif
                Log.Debug("LightshipObjectDetectionProvider constructed with nativeProviderHandle: " + _nativeProviderHandle);

                _displayHelper = new DisplayHelper(this);
            }

            public override void Start()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                Configure();
                _api.Start(_nativeProviderHandle);
            }

            public override void Stop()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Stop(_nativeProviderHandle);
            }

            public override void Destroy()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _categoryDirectory = null;
                _api.Destroy(_nativeProviderHandle);
                _nativeProviderHandle = IntPtr.Zero;
            }

            /// <summary>
            /// Method to be implemented by the provider to get a list of the object detection category names
            /// for the current model.
            /// </summary>
            /// <param name="names">A list of category labels. It will be empty if the method returns false.</param>
            /// <returns>True if channel names are available. False if not.</returns>
            /// <exception cref="System.NotSupportedException">Thrown when reading the channel names is not supported
            /// by the implementation.</exception>
            public override bool TryGetCategoryNames(out IReadOnlyList<string> names)
            {
                if (_categoryDirectory != null)
                {
                    names = _categoryDirectory.CategoryNames.AsReadOnly();
                    return true;
                }

                if (_nativeProviderHandle.IsValidHandle() && _api.TryGetCategoryNames(_nativeProviderHandle, out var categories))
                {
                    names = categories.AsReadOnly();
                    _categoryDirectory = new CategoryDirectory(categories);
                    return true;
                }

                names = new List<string>().AsReadOnly();
                return false;
            }

            private bool TryCalculateViewportMapping
            (
                int viewportWidth,
                int viewportHeight,
                ScreenOrientation orientation,
                out Matrix4x4 matrix
            )
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    matrix = Matrix4x4.identity;
                    return false;
                }

                return _api.TryCalculateViewportMapping
                (
                    _nativeProviderHandle,
                    viewportWidth,
                    viewportHeight,
                    orientation,
                    out matrix
                );
            }

            /// <summary>
            /// Tries to extract the latest object detection results from the camera image.
            /// </summary>
            /// <param name="results">An array of object detection instances.</param>
            /// <returns>Whether any object detection instances could be retrieved.</returns>
            public override bool TryGetDetectedObjects
            (
                out XRDetectedObject[] results
            )
            {
                // Defaults
                results = Array.Empty<XRDetectedObject>();

                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return false;
                }

                if (_categoryDirectory == null && !TryGetCategoryNames(out _))
                {
                    return false;
                }

                var gotDetections =
                    _api.TryGetLatestDetections
                    (
                        _nativeProviderHandle,
                        out uint numDetections,
                        out uint numClasses,
                        out float[] boxLocationsList,
                        out float[] probabilitiesList,
                        out uint _,
                        out _latestTimestamp,
                        true,
                        out var interpolationMatrix
                    );

                if (!gotDetections)
                {
                    return false;
                }

                // TODO: Acquire from native
                var container = new Vector2Int(256, 256);

                // Populate the results array
                results = new XRDetectedObject[numDetections];
                for (int i = 0; i < numDetections; i++)
                {
                    var vertexIndex = i * 4;

                    // Extract coordinates
                    // From Native: left, top, right, bottom to left, top, width, height for Unity Rect Struct
                    var topLeft =
                        new Vector2Int((int)boxLocationsList[vertexIndex], (int)boxLocationsList[vertexIndex + 1]);

                    var bottomRight =
                        new Vector2Int((int)boxLocationsList[vertexIndex + 2], (int)boxLocationsList[vertexIndex + 3]);

                    var topRight =
                        new Vector2Int((int)boxLocationsList[vertexIndex + 2], (int)boxLocationsList[vertexIndex + 1]);

                    var bottomLeft =
                        new Vector2Int((int)boxLocationsList[vertexIndex], (int)boxLocationsList[vertexIndex + 3]);

                    // Interpolate coordinates, if requested
                    if (interpolationMatrix.HasValue)
                    {
                        topLeft =
                            ImageSamplingUtils.TransformImageCoordinates
                            (
                                topLeft,
                                container,
                                container,
                                interpolationMatrix.Value
                            );

                        bottomRight =
                            ImageSamplingUtils.TransformImageCoordinates
                            (
                                bottomRight,
                                container,
                                container,
                                interpolationMatrix.Value
                            );

                        topRight =
                            ImageSamplingUtils.TransformImageCoordinates
                            (
                                topRight,
                                container,
                                container,
                                interpolationMatrix.Value
                            );

                        bottomLeft =
                            ImageSamplingUtils.TransformImageCoordinates
                            (
                                bottomLeft,
                                container,
                                container,
                                interpolationMatrix.Value
                            );
                    }

                    // compute the axis-aligned bounding box for the warped corners
                    var newLeft = Math.Min(topLeft.x, bottomLeft.x);
                    var newTop = Math.Min(topLeft.y, topRight.y);
                    var newRight = Math.Max(topRight.x, bottomRight.x);
                    var newBottom = Math.Max(bottomLeft.y, bottomRight.y);

                    topLeft = new Vector2Int(newLeft, newTop);
                    bottomRight = new Vector2Int(newRight, newBottom);

                    // Construct the UI rect
                    var rect = new Rect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y);

                    var boxProbIndex = i * (int)numClasses;
                    var confidences = probabilitiesList[boxProbIndex..(boxProbIndex + (int)numClasses)];
                    results[i] = new LightshipDetectedObject(rect, confidences, _categoryDirectory, _displayHelper);
                }

                return true;
            }

            private void Configure()
            {
                if (!_nativeProviderHandle.IsValidHandle())
                {
                    return;
                }

                _api.Configure(_nativeProviderHandle, TargetFrameRate);
            }

            // Destroy the native provider and replace it with the provided (or default mock) provider
            // Used for testing and mocking
            internal void SwitchApiImplementation(IApi api)
            {
                if (_nativeProviderHandle != IntPtr.Zero)
                {
                    _api.Stop(_nativeProviderHandle);
                    _api.Destroy(_nativeProviderHandle);
                }

                _api = api;
                _nativeProviderHandle = api.Construct(LightshipUnityContext.UnityContextHandle);

                _categoryDirectory = null;
            }
        }
    }
}
