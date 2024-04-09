// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems.Playback
{
    public static class ScreenOrientationFromPose
    {
        const int QuantisationLevels = 4;
        const float HysteresisThreshold = 1.0f / QuantisationLevels; // Set so that max is always used

        private static readonly IReadOnlyList<Vector2> DirectionVectors = new Vector2[]
        {
            new ((float)Math.Cos(Math.PI / 2.0f - Angle(0)), (float)Math.Sin(Math.PI / 2.0f - Angle(0))),
            new ((float)Math.Cos(Math.PI / 2.0f - Angle(1)), (float)Math.Sin(Math.PI / 2.0f - Angle(1))),
            new ((float)Math.Cos(Math.PI / 2.0f - Angle(2)), (float)Math.Sin(Math.PI / 2.0f - Angle(2))),
            new ((float)Math.Cos(Math.PI / 2.0f - Angle(3)), (float)Math.Sin(Math.PI / 2.0f - Angle(3)))
        };

        private static float Angle(int i) => (float)Math.PI * 2.0f * i / QuantisationLevels;

        /// <summary>
        /// Calculates the ScreenOrientation from a camera pose. It is assumed that LandscapeLeft is the natural camera
        /// orientation, so for example in identity pose. For the edge case screen facing up it will return Portrait and for
        /// screen facing down it will return PortraitUpsideDown.
        /// </summary>
        /// <param name="cameraPose">The camera pose in Unity coordinate convention</param>
        /// <returns>ScreenOrientation in Unity convention</returns>
        public static ScreenOrientation GetScreenOrientation(this Matrix4x4 cameraPose)
        {
#pragma warning disable CS0618
            // Don't corrupt the weights with an invalid matrix.
            if (!IsValidMatrix(cameraPose))
            {
                return ScreenOrientation.Unknown;
            }
#pragma warning restore CS0618

            var initialWeight = 1.0f / QuantisationLevels;
            var directionWeights = new [] {initialWeight, initialWeight, initialWeight, initialWeight};

            Matrix4x4 tracker2Camera = cameraPose.inverse;
            Vector3 trackerUpVector = new Vector3(0.0f, -1.0f, 0.0f);

            Vector3 cameraUpVector = tracker2Camera * trackerUpVector;
            Vector2 imageUpVector =
                new Vector2(cameraUpVector.x, -cameraUpVector.y); // The vector is intentionally not normalised here

            float newWeight = 1.0f;

            // Calculate weights in all directions:
            for (int i = 0; i < QuantisationLevels; i++)
            {
                // CurrentDirectionWeight is a linear interpolation between the quantised classes on
                // either side of the current direction. For classes that are not adjacent the weight will be 0.
                float currentDirectionWeight = (float)Math.Max(0.0f,
                    Vector3.Dot(imageUpVector, DirectionVectors[i]) -
                    Math.Cos(Math.PI * 2.0f / (float)QuantisationLevels));
                directionWeights[i] = (1.0f - newWeight) * directionWeights[i] + newWeight * currentDirectionWeight;
            }

            // Normalise:
            float sum = 0.0f;
            for (int i = 0; i < QuantisationLevels; i++)
                sum += directionWeights[i];
            for (int i = 0; i < QuantisationLevels; i++)
                directionWeights[i] /= sum;

            var quantisedDirection = 0;

            // Check whether any directions have exceeded the hysterisis threshold:
            for (int i = 0; i < QuantisationLevels; i++)
                if (directionWeights[i] > directionWeights[quantisedDirection])
                    if (directionWeights[i] > HysteresisThreshold)
                        quantisedDirection = i;

            switch (quantisedDirection)
            {
                case 0:
                    return ScreenOrientation.LandscapeLeft;
                case 1:
                    return ScreenOrientation.Portrait;
                case 2:
                    return ScreenOrientation.LandscapeRight;
                case 3:
                    return ScreenOrientation.PortraitUpsideDown;
                default:
#pragma warning disable CS0618
                    return ScreenOrientation.Unknown;
#pragma warning restore CS0618
            }
        }

        private static bool IsValidMatrix(Matrix4x4 matrix)
        {
            for (var y = 0; y < 4; y++)
                for (var x = 0; x < 4; x++)
                    if (float.IsNaN(matrix[x,y]))
                        return false;
            return true;
        }
    }
}
