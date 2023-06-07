// Copyright 2022 Niantic Labs. All rights reserved.

namespace Niantic.Lightship.AR
{
    internal static class _LightshipPlugin
    {
#if UNITY_IOS && !UNITY_EDITOR
        public const string Name = "__Internal";
#elif (UNITY_EDITOR && !IN_ROSETTA) || UNITY_STANDALONE_OSX || UNITY_ANDROID
        public const string Name = "ARDK";
#else
        public const string Name = "PLATFORM_NOT_SUPPORTED";
#endif
    }
}
