// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    /// <summary>
    /// [Experimental] <c>ILightshipLoader</c> defines an interface through which external subsystems can be 
    /// registered for loading through Lightship, and to all them to access XRLoaderHelper functionality.  The external
    /// subsystem loader must implement the ILightshipExternalLoader interface.
    /// 
    /// This Interface is experimental so may change or be removed from future versions without warning.
    /// </summary>
    public interface ILightshipLoader
    {
        /// <summary>
        /// Registers an external submodule loader
        /// </summary>
        /// <param name="loader">The external subsystem loader to be added</param>
        void AddExternalLoader(ILightshipExternalLoader loader);

        /// <summary>
        /// Allows external lightship modules to create subsystems.  Provides access to CreateSubsystem in the 
        /// implementation of XRLoaderHelper currently used by Lightship.  Refer to XRLoaderHelper documentation for 
        /// more details.
        /// </summary>
        void CreateSubsystem<TDescriptor, TSubsystem>(List<TDescriptor> descriptors, string id)
            where TDescriptor : ISubsystemDescriptor
            where TSubsystem : ISubsystem;

        /// <summary>
        /// Allows external lightship modules to destroy subsystems.  Provides access to DestroySubsystem in the 
        /// implementation of XRLoaderHelper currently used by Lightship.  Refer to XRLoaderHelper documentation for 
        /// more details.
        /// </summary>
        void DestroySubsystem<T>() where T : class, ISubsystem;


        /// <summary>
        /// Allows external lightship modules to access loaded subsystems.  Provides access to GetLoadedSubsystem in the 
        /// implementation of XRLoaderHelper currently used by Lightship.  Refer to XRLoaderHelper documentation for 
        /// more details.
        /// </summary>        
        T GetLoadedSubsystem<T>() where T : class, ISubsystem;
    }
}
