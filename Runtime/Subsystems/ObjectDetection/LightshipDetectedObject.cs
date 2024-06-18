// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;

using CategoryDirectory = Niantic.Lightship.AR.Subsystems.ObjectDetection.LightshipObjectDetectionSubsystem.LightshipObjectDetectionProvider.CategoryDirectory;
using DisplayHelper = Niantic.Lightship.AR.Subsystems.ObjectDetection.LightshipObjectDetectionSubsystem.LightshipObjectDetectionProvider.DisplayHelper;

namespace Niantic.Lightship.AR.Subsystems.ObjectDetection
{
    internal class LightshipDetectedObject: XRDetectedObject
    {
        private readonly CategoryDirectory _categoryDirectory;
        private readonly DisplayHelper _displayHelper;

        // Describes the bounding box of the detection in the coordinate space described by _containerResolution
        private readonly Rect _rect;

        private readonly Vector2Int _containerResolution;

        private readonly float[] _confidences;
        public override float[] Confidences => _confidences;

        // Not exposing this in public API for now
        private readonly uint? _trackingId;

        public LightshipDetectedObject
        (
            uint? trackingId,
            Rect rect,
            float[] confidences,
            CategoryDirectory directory,
            DisplayHelper displayHelper,
            Vector2Int containerResolution
        )
        {
            _trackingId = trackingId;
            _rect = rect;
            _confidences = confidences;
            _categoryDirectory = directory;
            _displayHelper = displayHelper;
            _containerResolution = containerResolution;
        }

        public override float GetConfidence(string categoryName)
        {
            if (_categoryDirectory.TryGetIndex(categoryName, out var index))
            {
                return Confidences[index];
            }

            return 0f;
        }

        public override List<XRObjectCategorization> GetConfidentCategorizations(float threshold = 0.4f)
        {
            var categorizations = new List<XRObjectCategorization>();
            for (var i = 0; i < _confidences.Length; i++)
            {
                if (_confidences[i] > threshold)
                {
                    _categoryDirectory.TryGetCategory(i, out var name);
                    categorizations.Add
                    (
                        new XRObjectCategorization
                        (
                            name,
                            i,
                            Confidences[i]
                        )
                    );
                }
            }

            return categorizations;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="viewportWidth"></param>
        /// <param name="viewportHeight"></param>
        /// <param name="orientation"></param>
        /// <returns>A rectangle describing the bounding box of this detected object in the coordinate space
        /// of the specified viewport.</returns>
        public override Rect CalculateRect(int viewportWidth, int viewportHeight, ScreenOrientation orientation)
        {
            // Inspect the source rect
            var min = new Vector2Int((int)_rect.x, (int)_rect.y);
            var max = new Vector2Int((int)_rect.width + (int)_rect.x, (int)_rect.height + (int)_rect.y);

            // Transform the source rect to viewport
            var viewport = new Vector2Int(viewportWidth, viewportHeight);
            var gotViewportTransform =
                _displayHelper.TryCalculateViewportMapping
                (
                    viewportWidth,
                    viewportHeight,
                    orientation,
                    out var transform
                );

            if (!gotViewportTransform)
            {
                Log.Error("Failed to get object detection inference to viewport transform matrix.");
            }

            var minPrime = ImageSamplingUtils.TransformImageCoordinates(min, _containerResolution, viewport, transform);
            var maxPrime = ImageSamplingUtils.TransformImageCoordinates(max, _containerResolution, viewport, transform);

            return new Rect(minPrime.x, minPrime.y, maxPrime.x - minPrime.x, minPrime.y - maxPrime.y);  // height is inverted
        }
    }
}
