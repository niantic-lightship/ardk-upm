using System.Collections.Generic;
using Niantic.Lightship.AR.Playback;
using Niantic.Lightship.AR.Subsystems;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Loader
{
    internal class NativeLoaderHelper
    {
        private readonly List<XROcclusionSubsystemDescriptor> _occlusionSubsystemDescriptors = new();
        private readonly List<XRPersistentAnchorSubsystemDescriptor> _persistentAnchorSubsystemDescriptors = new();
        private readonly List<XRSemanticsSubsystemDescriptor> _semanticsSubsystemDescriptors = new();
        private readonly List<XRInputSubsystemDescriptor> _inputSubsystemDescriptors = new();
        private readonly List<XRScanningSubsystemDescriptor> _scanningSubsystemDescriptors = new();
        private readonly List<XRMeshSubsystemDescriptor> _meshingSubsystemDescriptors = new();
        private LightshipPlaybackInputProvider _inputProvider;

        public bool Initialize(XRLoaderHelper loader, LightshipSettings settings, bool isLidarSupported, bool isTest)
        {
            LightshipUnityContext.Initialize(settings, isLidarSupported, isTest);

            Debug.Log("Initialize native subsystems");

            // Create Lightship Occlusion subsystem
            if (settings.UseLightshipDepth && (!settings.PreferLidarIfAvailable || !isLidarSupported))
            {
                // Destroy the platform's depth subsystem before creating our own.
                // The native platform's loader will destroy our subsystem for us during Deinitialize.
                loader.DestroySubsystem<XROcclusionSubsystem>();

                loader.CreateSubsystem<XROcclusionSubsystemDescriptor, XROcclusionSubsystem>
                (
                    _occlusionSubsystemDescriptors,
                    "Lightship-Occlusion"
                );
            }

            // Create Lightship Persistent Anchor subsystem
            if (settings.UseLightshipPersistentAnchor)
            {
                Debug.Log("Creating " + nameof(LightshipPersistentAnchorSubsystem));
                loader.CreateSubsystem<XRPersistentAnchorSubsystemDescriptor, XRPersistentAnchorSubsystem>
                (
                    _persistentAnchorSubsystemDescriptors,
                    "Lightship-PersistentAnchor"
                );
            }

            // Create Lightship Semantics subsystem
            if (settings.UseLightshipSemanticSegmentation)
            {
                Debug.Log("Creating " + nameof(LightshipSemanticsSubsystem));
                loader.CreateSubsystem<XRSemanticsSubsystemDescriptor, XRSemanticsSubsystem>
                (
                    _semanticsSubsystemDescriptors,
                    "Lightship-Semantics"
                );
            }

            // Create Lightship Scanning subsystem
            if (settings.UseLightshipScanning)
            {
                Debug.Log("Creating " + nameof(LightshipScanningSubsystem));
                loader.CreateSubsystem<XRScanningSubsystemDescriptor, XRScanningSubsystem>
                (
                    _scanningSubsystemDescriptors,
                    "Lightship-Scanning"
                );
            }

            // Create Lightship Playback subsystem
            if ((settings.EditorPlaybackSettings.UsePlayback && Application.isEditor) ||
                (settings.DevicePlaybackSettings.UsePlayback && !Application.isEditor))
            {
                Debug.Log("Setting up PAM for Playback");
                var reader = ((ILightshipLoader)loader).PlaybackDatasetReader;
                LightshipUnityContext.PlatformAdapterManager.SetPlaybackDatasetReader(reader);

                // Input is an integrated subsystem that must be created after the LightshipUnityContext is initialized,
                // which is why it's done here instead of in the PlaybackLoaderHelper
                Debug.Log("Creating " + nameof(LightshipPlaybackInputProvider));
                _inputProvider = new LightshipPlaybackInputProvider();
                _inputProvider.SetPlaybackDatasetReader(reader);

                loader.DestroySubsystem<XRInputSubsystem>();
                loader.CreateSubsystem<XRInputSubsystemDescriptor, XRInputSubsystem>
                (
                    _inputSubsystemDescriptors,
                    "LightshipInput"
                );
            }

            if (settings.UseLightshipMeshing)
            {
                // our C# "ghost" creates our meshing module to listen to Unity meshing lifecycle callbacks
                loader.DestroySubsystem<XRMeshSubsystem>();
                var meshingProvider = new LightshipMeshingProvider(LightshipUnityContext.UnityContextHandle);
                // Create Unity integrated subsystem
                loader.CreateSubsystem<XRMeshSubsystemDescriptor, XRMeshSubsystem>(_meshingSubsystemDescriptors,
                    "LightshipMeshing");
            }

            return true;
        }

        /// <summary>
        /// Destroys each initialized subsystem.
        /// </summary>
        /// <returns>Always returns `true`.</returns>
        public bool Deinitialize(XRLoaderHelper loader)
        {
            Debug.Log("Destroying lightship subsystems");

            // Destroy subsystem does a null check, so will just no-op if these subsystems were not created or already destroyed
            loader.DestroySubsystem<XRSemanticsSubsystem>();
            loader.DestroySubsystem<XRPersistentAnchorSubsystem>();
            loader.DestroySubsystem<XROcclusionSubsystem>();
            loader.DestroySubsystem<XRScanningSubsystem>();
            loader.DestroySubsystem<XRMeshSubsystem>();

            _inputProvider?.Dispose();

            // Unity's native lifecycle handler for integrated subsystems does call Stop() before Shutdown() if
            // the subsystem is running when the latter is called. However, for the XRInputSubsystem, this causes
            // the below error to appear.
            //      "A device disconnection with the id 0 has been reported but no device with that id was connected."
            // Manually calling Stop() before Shutdown() eliminates the issue.
            var input = loader.GetLoadedSubsystem<XRInputSubsystem>();
            if (input != null && input.running)
            {
                input.Stop();
            }

            loader.DestroySubsystem<XRInputSubsystem>();

            LightshipUnityContext.Deinitialize();

            return true;
        }

        internal bool DetermineIfDeviceSupportsLidar()
        {
            var subsystems = new List<XRMeshSubsystem>();
            SubsystemManager.GetInstances(subsystems);
            return subsystems.Count > 0;
        }
    }
}
