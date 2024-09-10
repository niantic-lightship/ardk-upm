// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Loader
{
    public class LightshipStandaloneLoader : XRLoaderHelper, ILightshipInternalLoaderSupport
    {
        private LightshipLoaderHelper _lightshipLoaderHelper;
        private List<ILightshipExternalLoader> _externalLoaders = new();

        /// <summary>
        /// The `XROcclusionSubsystem` whose lifecycle is managed by this loader.
        /// </summary>
        public XROcclusionSubsystem LightshipOcclusionSubsystem => base.GetLoadedSubsystem<XROcclusionSubsystem>();

        /// <summary>
        /// The `XRPersistentAnchorSubsystem` whose lifecycle is managed by this loader.
        /// </summary>
        public XRPersistentAnchorSubsystem LightshipPersistentAnchorSubsystem =>
            base.GetLoadedSubsystem<XRPersistentAnchorSubsystem>();

        /// <summary>
        /// The `XRMeshingSubsystem` whose lifecycle is managed by this loader.
        /// </summary>
        public XRMeshSubsystem LightshipMeshSubsystem => base.GetLoadedSubsystem<XRMeshSubsystem>();

        /// <summary>
        /// Initializes the loader. This is called from Unity when starting an AR session.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public override bool Initialize()
        {
            _lightshipLoaderHelper = new LightshipLoaderHelper(_externalLoaders);
            return InitializeWithLightshipHelper(_lightshipLoaderHelper);
        }

        public bool InitializeWithLightshipHelper(LightshipLoaderHelper lightshipLoaderHelper)
        {
            _lightshipLoaderHelper = lightshipLoaderHelper;
            return _lightshipLoaderHelper.Initialize(this);
        }

        // There is no platform implementation for standalone.
        public bool IsPlatformDepthAvailable()
        {
            Log.Warning("Standalone currently has no platform implementation. You have to run with Playback enabled.");
            return false;
        }

        public new void CreateSubsystem<TDescriptor, TSubsystem>(List<TDescriptor> descriptors, string id) where TDescriptor : ISubsystemDescriptor where TSubsystem : ISubsystem
        {
            base.CreateSubsystem<TDescriptor, TSubsystem>(descriptors, id);
        }

        public new void DestroySubsystem<T>() where T : class, ISubsystem
        {
            base.DestroySubsystem<T>();
        }

        public new T GetLoadedSubsystem<T>() where T : class, ISubsystem
        {
            return base.GetLoadedSubsystem<T>();
        }

        /// <summary>
        /// Destroys each subsystem.
        /// </summary>
        /// <returns>Always returns `true`.</returns>
        public override bool Deinitialize()
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            _lightshipLoaderHelper?.Deinitialize();
#endif
            return true;
        }

        public bool InitializePlatform()
        {
            Log.Warning("Standalone currently has no platform implementation. You have to run with Playback enabled.");
            return true;
        }

        public bool DeinitializePlatform()
        {
            Log.Warning("Standalone currently has no platform implementation. You have to run with Playback enabled.");
            return true;
        }

        void ILightshipLoader.AddExternalLoader(ILightshipExternalLoader loader)
        {
            _externalLoaders.Add(loader);
        }
    }
}
