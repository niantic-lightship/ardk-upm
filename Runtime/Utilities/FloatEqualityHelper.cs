// Copyright 2022-2024 Niantic.

using System;

using UnityEngine;

namespace Niantic.Lightship.AR.Utilities
{
    internal static class FloatEqualityHelper
    {
        public static bool NearlyEquals(this float a, float b, float epsilon = 0.00001f)
        {
            float absA = Math.Abs(a);
            float absB = Math.Abs(b);
            float diff = Math.Abs(a - b);

            if (a == b) { // shortcut, handles infinities
                return true;
            } else if (a == 0 || b == 0 || absA + absB < Mathf.Epsilon) {
                // a or b is zero or both are extremely close to it
                // relative error is less meaningful here
                return diff < (epsilon * Mathf.Epsilon);
            } else { // use relative error
                return diff / (absA + absB) < epsilon;
            }
        }
    }
}
