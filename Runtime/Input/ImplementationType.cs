// Copyright 2022-2024 Niantic.

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
