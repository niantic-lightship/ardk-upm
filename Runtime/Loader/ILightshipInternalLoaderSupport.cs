// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    internal interface ILightshipInternalLoaderSupport : ILightshipLoader
    {
        void InjectLightshipLoaderHelper(LightshipLoaderHelper lightshipLoaderHelper);

        // this is so we can write tests
        public bool InitializeWithLightshipHelper(LightshipLoaderHelper lightshipLoaderHelper);

        bool InitializePlatform();

        bool DeinitializePlatform();

        bool IsPlatformDepthAvailable();
    }
}
