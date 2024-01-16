// Copyright 2022-2024 Niantic.
// Copyright 2022 - 2023 Niantic.

using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Loader
{
    /// <summary>
    /// An interface to be implemented by packages to provide similar functionality to a Loader.
    /// Classes implementing this interface can be registered through the ILightshipLoader interface
    /// so that their Initialize and Deinitialize methods get called at the appropriate times for
    /// creating and destroying subsystems.
    /// </summary>
    internal interface ILightshipExternalLoader
    {
        internal bool Initialize(ILightshipLoader loader);
        internal void Deinitialize(ILightshipLoader loader);
    }
}
