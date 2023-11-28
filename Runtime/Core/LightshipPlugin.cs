// Copyright 2022-2023 Niantic.

namespace Niantic.Lightship.AR.Core
{
    internal static class LightshipPlugin
    {
#if UNITY_IOS && !UNITY_EDITOR
        public const string Name = "__Internal";
#elif (UNITY_EDITOR && !IN_ROSETTA) || UNITY_STANDALONE_OSX || UNITY_ANDROID
        public const string Name = "LightshipARDK";
#else
        public const string Name = "PLATFORM_NOT_SUPPORTED";
#endif
    }
}
