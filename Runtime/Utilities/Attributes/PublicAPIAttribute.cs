// Copyright 2022-2024 Niantic.

using System;

namespace Niantic.Lightship.AR.Utilities
{
    // Used for Doxygen generation
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    public class PublicAPIAttribute : UnityEngine.HelpURLAttribute
    {
        public PublicAPIAttribute(string helpUrl = "")
            : base($"https://lightship.dev/docs/ardk/{helpUrl}")
        { }
    }
}
