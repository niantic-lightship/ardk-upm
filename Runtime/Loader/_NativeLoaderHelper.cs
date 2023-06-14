using System.Collections.Generic;
using Niantic.Lightship.AR.Playback;
using Niantic.Lightship.AR.Subsystems;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Loader
{
    internal class _NativeLoaderHelper
    {
        private static readonly List<XROcclusionSubsystemDescriptor> s_occlusionSubsystemDescriptors = new();

        private static readonly List<XRPersistentAnchorSubsystemDescriptor>
            s_persistentAnchorSubsystemDescriptors = new();

        private static readonly List<XRSemanticsSubsystemDescriptor> s_semanticsSubsystemDescriptors = new();

        private static readonly List<XRInputSubsystemDescriptor> s_inputSubsystemDescriptors = new();

        private static readonly List<XRScanningSubsystemDescriptor> s_scanningSubsystemDescriptors = new();

        private static readonly List<XRMeshSubsystemDescriptor> _meshingSubsystemDescriptors = new();

        private _LightshipPlaybackInputProvider _inputProvider;

        public bool Initialize(XRLoaderHelper loader, LightshipSettings settings, bool isLidarSupported)
        {
            LightshipUnityContext.Initialize(settings, isLidarSupported);

            Debug.Log("Initialize native subsystems");

            // Create Lightship Occlusion subsystem
            if (settings.UseLightshipDepth && (!settings.PreferLidarIfAvailable || !isLidarSupported))
            {
                // Destroy the platform's depth subsystem before creating our own.
                // The native platform's loader will destroy our subsystem for us during Deinitialize.
                loader.DestroySubsystem<XROcclusionSubsystem>();

                loader.CreateSubsystem<XROcclusionSubsystemDescriptor, XROcclusionSubsystem>
                (
                    s_occlusionSubsystemDescriptors,
                    "Lightship-Occlusion"
                );

                ((_ILightshipSettingsUser)loader
                    .GetLoadedSubsystem<XROcclusionSubsystem>())
                    .SetLightshipSettings(settings);
            }

            // Create Lightship Persistent Anchor subsystem
            if (settings.UseLightshipPersistentAnchor)
            {
                Debug.Log("Creating " + nameof(LightshipPersistentAnchorSubsystem));
                loader.CreateSubsystem<XRPersistentAnchorSubsystemDescriptor, XRPersistentAnchorSubsystem>
                (
                    s_persistentAnchorSubsystemDescriptors,
                    "Lightship-PersistentAnchor"
                );
            }

            // Create Lightship Semantics subsystem
            if (settings.UseLightshipSemanticSegmentation)
            {
                Debug.Log("Creating " + nameof(LightshipSemanticsSubsystem));
                loader.CreateSubsystem<XRSemanticsSubsystemDescriptor, XRSemanticsSubsystem>
                (
                    s_semanticsSubsystemDescriptors,
                    "Lightship-Semantics"
                );
            }

            // Create Lightship Scanning subsystem
            if (settings.UseLightshipScanning)
            {
                Debug.Log("Creating " + nameof(LightshipScanningSubsystem));
                loader.CreateSubsystem<XRScanningSubsystemDescriptor, XRScanningSubsystem>(
                    s_scanningSubsystemDescriptors,
                    "Lightship-Scanning");
            }

            // Create Lightship Playback subsystem
            if ((settings.UsePlaybackOnEditor && Application.isEditor) || (settings.UsePlaybackOnDevice && !Application.isEditor))
            {
                Debug.Log("Setting up PAM for Playback");
                var reader = ((_ILightshipLoader)loader).PlaybackDatasetReader;
                LightshipUnityContext.PlatformAdapterManager.SetPlaybackDatasetReader(reader);

                // Input is an integrated subsystem that must be created after the LightshipUnityContext is initialized,
                // which is why it's done here instead of in the _PlaybackLoaderHelper
                Debug.Log("Creating " + nameof(_LightshipPlaybackInputProvider));
                _inputProvider = new _LightshipPlaybackInputProvider();
                _inputProvider.SetPlaybackDatasetReader(reader);

                loader.DestroySubsystem<XRInputSubsystem>();
                loader.CreateSubsystem<XRInputSubsystemDescriptor, XRInputSubsystem>
                (
                    s_inputSubsystemDescriptors,
                    "LightshipInput"
                );
            }

            if (settings.UseLightshipMeshing)
            {
                // our C# "ghost" creates our meshing module to listen to Unity meshing lifecycle callbacks
                loader.DestroySubsystem<XRMeshSubsystem>();
                var meshingSubsystem = new LightshipMeshingProvider(LightshipUnityContext.UnityContextHandle);
                // Create Unity integrated subsystem
                loader.CreateSubsystem<XRMeshSubsystemDescriptor, XRMeshSubsystem>(_meshingSubsystemDescriptors, "LightshipMeshing");
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
                input.Stop();

            loader.DestroySubsystem<XRInputSubsystem>();

            LightshipUnityContext.Deinitialize();

            return true;
        }
    }
}
