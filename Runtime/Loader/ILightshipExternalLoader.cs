// Copyright 2022-2024 Niantic.

using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Loader
{
    /// <summary>
    /// [Experimental] An interface to be implemented by packages to provide similar functionality to a Loader.
    /// Classes implementing this interface can be registered through the ILightshipLoader interface
    /// so that their Initialize and Deinitialize methods get called at the appropriate times for
    /// creating and destroying subsystems.
    ///
    /// This Interface is experimental so may change or be removed from future versions without warning.
    /// </summary>
    public interface ILightshipExternalLoader
    {
        public bool Initialize(ILightshipLoader loader);
        public void Deinitialize(ILightshipLoader loader);
    }
}
