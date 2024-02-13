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
        private readonly Rect _rect;
        private readonly float[] _confidences;
        public override float[] Confidences => _confidences;

        public LightshipDetectedObject
        (
            Rect rect,
            float[] confidences,
            CategoryDirectory directory,
            DisplayHelper displayHelper
        )
        {
            _rect = rect;
            _confidences = confidences;
            _categoryDirectory = directory;
            _displayHelper = displayHelper;
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

        public override Rect CalculateRect(int viewportWidth, int viewportHeight, ScreenOrientation orientation)
        {
            // Inspect the source rect
            var inferenceResolution = new Vector2Int(256, 256);
            var min = new Vector2Int((int)_rect.x, (int)_rect.y);
            var max = new Vector2Int((int)_rect.width + (int)_rect.x, (int)_rect.height + (int)_rect.y);

            // Transform the source rect to viewport
            var container = new Vector2Int(viewportWidth, viewportHeight);
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

            var minPrime = ImageSamplingUtils.TransformImageCoordinates(min, inferenceResolution, container, transform);
            var maxPrime = ImageSamplingUtils.TransformImageCoordinates(max, inferenceResolution, container, transform);

            return new Rect(minPrime.x, minPrime.y, maxPrime.x - minPrime.x, minPrime.y - maxPrime.y);  // height is inverted
        }
    }
}
