// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    internal interface ILightshipInternalLoaderSupport : ILightshipLoader
    {
        /// <summary>
        /// Initializes the loader with an injected LightshipLoaderHelper.
        /// This is a helper to initialize manually from tests.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public bool InitializeWithLightshipHelper(LightshipLoaderHelper lightshipLoaderHelper);

        bool InitializePlatform();

        bool DeinitializePlatform();

        bool IsPlatformDepthAvailable();
    }
}
