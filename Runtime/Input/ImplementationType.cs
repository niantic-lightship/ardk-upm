// Copyright 2023 Niantic, Inc. All Rights Reserved.

namespace Niantic.Lightship.AR
{
    // Exists to make code easier to read when switching input provider implementations.
    internal enum InputImplementationType
    {
        Unity,
        Mock,
        Playback
    }
}
