// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;

namespace Niantic.Lightship.AR.PlatformAdapterManager
{
    internal interface ITexturesSetter : IDisposable
    {
        void InvalidateCachedTextures();

        // Get the timestamp associated with the current textures
        double GetCurrentTimestampMs();

        void SetRgba256x144Image();
        void SetJpeg720x540Image();
        void SetJpegFullResImage();
        void SetPlatformDepthBuffer();
    }
}
