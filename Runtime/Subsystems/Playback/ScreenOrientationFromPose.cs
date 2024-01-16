// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.Lightship.AR.Subsystems.Playback
{
    public static class ScreenOrientationFromPose
    {
        private static readonly List<Vector2> s_directionVectors = new();
        private static readonly List<float> s_directionWeights = new();

        /// <summary>
        /// Calculates the ScreenOrientation from a camera pose. It is assumed that LandscapeLeft is the natural camera
        /// orientation, so for example in identity pose. For the edge case screen facing up it will return Portrait and for
        /// screen facing down it will return PortraitUpsideDown.
        /// </summary>
        /// <param name="cameraPose">The camera pose in Unity coordinate convention</param>
        /// <returns>ScreenOrientation in Unity convention</returns>
        public static ScreenOrientation GetScreenOrientation(this Matrix4x4 cameraPose)
        {
            const int quantisationLevels = 4;

            // first init
            if (s_directionVectors.Count == 0)
            {
                for (int i = 0; i < quantisationLevels; i++)
                {
                    float angle = (float)Math.PI * 2.0f * i / quantisationLevels; // clockwise from vertical
                    s_directionVectors.Add(new Vector2((float)Math.Cos(Math.PI / 2.0f - angle),
                        (float)Math.Sin(Math.PI / 2.0f - angle))); // corresponding image vector
                    s_directionWeights.Add(1.0f / quantisationLevels);
                }
            }

            Matrix4x4 tracker2Camera = cameraPose.inverse;
            Vector3 trackerUpVector = new Vector3(0.0f, -1.0f, 0.0f);

            Vector3 cameraUpVector = tracker2Camera * trackerUpVector;
            Vector2 imageUpVector =
                new Vector2(cameraUpVector.x, -cameraUpVector.y); // The vector is intentionally not normalised here

            float hysteresisThreshold = 1.0f / quantisationLevels; // Set so that max is always used
            float newWeight = 1.0f;

            // Calculate weights in all directions:
            for (int i = 0; i < quantisationLevels; i++)
            {
                // CurrentDirectionWeight is a linear interpolation between the quantised classes on either side of the current direction.  For classes that are not adjacent the weight will be 0.
                float currentDirectionWeight = (float)Math.Max(0.0f,
                    Vector3.Dot(imageUpVector, s_directionVectors[i]) -
                    Math.Cos(Math.PI * 2.0f / (float)quantisationLevels));
                s_directionWeights[i] = (1.0f - newWeight) * s_directionWeights[i] + newWeight * currentDirectionWeight;
            }

            // Normalise:
            float sum = 0.0f;
            for (int i = 0; i < quantisationLevels; i++)
                sum += s_directionWeights[i];
            for (int i = 0; i < quantisationLevels; i++)
                s_directionWeights[i] /= sum;

            var quantisedDirection = 0;

            // Check whether any directions have exceeded the hysterisis threshold:
            for (int i = 0; i < quantisationLevels; i++)
                if (s_directionWeights[i] > s_directionWeights[quantisedDirection])
                    if (s_directionWeights[i] > hysteresisThreshold)
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
    }
}
