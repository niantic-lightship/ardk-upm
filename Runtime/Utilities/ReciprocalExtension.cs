// Copyright 2022-2024 Niantic.

namespace Niantic.Lightship.AR.Utilities
{
    public static class ReciprocalExtension
    {
        public static float ZeroOrReciprocal(this float value)
        {
            return value == 0f ? 0f : 1f / value;
        }

        public static double ZeroOrReciprocal(this double value)
        {
            return value == 0d ? 0d : 1d / value;
        }

        public static int ZeroOrReciprocal(this int value)
        {
            return value == 0 ? 0 : 1 / value;
        }
    }
}
