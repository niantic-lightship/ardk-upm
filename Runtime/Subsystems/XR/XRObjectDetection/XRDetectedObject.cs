// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;


namespace Niantic.Lightship.AR.XRSubsystems
{
    /// <summary>
    /// A structure representing an object detection categorization. The object detection algorithm surfaces
    /// at least one categorization per detected object, but usually there are multiple categorizations.
    /// </summary>
    /// <remarks>
    /// This is an experimental API. Experimental features are subject to breaking changes,
    /// not officially supported, and may be deprecated without notice.
    /// </remarks>
    [PublicAPI]
    public struct XRObjectCategorization
    {
        /// <summary>
        /// The category's name.
        /// </summary>
        public readonly string CategoryName;

        /// <summary>
        /// The category's index in the list of category names obtained from the
        /// <see cref="XRObjectDetectionSubsystem.TryGetCategoryNames"/> method.
        /// </summary>
        public readonly int CategoryIndex;

        /// <summary>
        /// The probability that the detected object this categorization belong to is actually of the class
        /// described by <see cref="CategoryName"/>.
        /// </summary>
        public readonly float Confidence;

        public XRObjectCategorization(string name, int index, float confidence)
        {
            CategoryName = name;
            CategoryIndex = index;
            Confidence = confidence;
        }
    }

    /// <summary>
    /// A class representing an object in the XR camera's view detected by the object detection model.
    /// </summary>
    /// <remarks>
    /// This is an experimental API. Experimental features are subject to breaking changes,
    /// not officially supported, and may be deprecated without notice.
    /// </remarks>
    [PublicAPI]
    public abstract class XRDetectedObject
    {
        /// <summary>
        /// The confidences of all the categories that can possibly be detected. The element at index i is the
        /// confidence for the category with index i.
        /// </summary>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public abstract float[] Confidences { get; }

        /// <summary>
        /// Gets the confidence value between 0 and 1.0 for the specified category for this detected object.
        /// </summary>
        /// <param name="categoryName">The name of the category to query. This collection of valid names can be
        /// obtained from the <see cref="XRObjectDetectionSubsystem.TryGetCategoryNames"/> method</param>
        /// <returns>The confidence value for the specified category for this detected object.</returns>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public abstract float GetConfidence(string categoryName);

        /// <summary>
        /// Gets the object categorizations for this detected object.
        /// </summary>
        /// <param name="threshold">The minimum confidence value needed for a categorization to be included
        /// in the returned list. Defaults to 0.4 if not provided.</param>
        /// <returns>A list of all the confident categorizations for this detected object.</returns>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public abstract List<XRObjectCategorization> GetConfidentCategorizations(float threshold = 0.4f);

        /// <summary>
        /// The 2D bounding box of the detected object, transformed to be displayed in the given viewport.
        /// Usually this will be the same viewport the XR camera image is being rendered to.
        /// </summary>
        /// <param name="viewportWidth">The pixel width of the viewport.</param>
        /// <param name="viewportHeight">The pixel height of the viewport.</param>
        /// <param name="orientation">The orientation of the viewport.</param>
        /// <returns>The Rect describing the position and bounds of the detected object in the given viewport.</returns>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public abstract Rect CalculateRect(int viewportWidth, int viewportHeight, ScreenOrientation orientation);
    }

    public static class ObjectDetectionUtilities
    {
        public static Vector3[] GetPoints(this Rect rect)
        {
            var points = new Vector3[4];
            points[0] = new Vector3(rect.x, rect.y);
            points[1] = new Vector3(rect.x + rect.width, rect.y);
            points[2] = new Vector3(rect.x + rect.width, rect.y + rect.height);
            points[3] = new Vector3(rect.x, rect.y + rect.height);

            return points;
        }

        public static float GetArea(this Rect rect)
        {
            return rect.width * rect.height;
        }
    }
}
