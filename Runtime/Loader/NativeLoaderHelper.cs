// Copyright 2022-2024 Niantic.
using System.Collections.Generic;

using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Subsystems.Meshing;
using Niantic.Lightship.AR.Subsystems.ObjectDetection;
using Niantic.Lightship.AR.Subsystems.Scanning;
using Niantic.Lightship.AR.Subsystems.Semantics;
using Niantic.Lightship.AR.Subsystems.PersistentAnchor;
using Niantic.Lightship.AR.Subsystems.WorldPositioning;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Loader
{
    public class NativeLoaderHelper
    {
        private readonly List<XROcclusionSubsystemDescriptor> _occlusionSubsystemDescriptors = new();
        private readonly List<XRPersistentAnchorSubsystemDescriptor> _persistentAnchorSubsystemDescriptors = new();
        private readonly List<XRSemanticsSubsystemDescriptor> _semanticsSubsystemDescriptors = new();
        private readonly List<XRScanningSubsystemDescriptor> _scanningSubsystemDescriptors = new();
        private readonly List<XRMeshSubsystemDescriptor> _meshingSubsystemDescriptors = new();
        private readonly List<XRObjectDetectionSubsystemDescriptor> _objectDetectionSubsystemDescriptors = new();
        private readonly List<XRWorldPositioningSubsystemDescriptor> _worldPositioningSubsystemDescriptors = new ();

        internal bool Initialize(ILightshipInternalLoaderSupport loader, bool isLidarSupported)
        {
            var settings = LightshipSettingsHelper.ActiveSettings;

            LightshipUnityContext.Initialize(isLidarSupported, settings.TestSettings.DisableTelemetry);

            Log.Info("Initialize native subsystems");

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
                Log.Info("Creating " + nameof(LightshipPersistentAnchorSubsystem));
                loader.CreateSubsystem<XRPersistentAnchorSubsystemDescriptor, XRPersistentAnchorSubsystem>
                (
                    _persistentAnchorSubsystemDescriptors,
                    "Lightship-PersistentAnchor"
                );
            }

            // Create Lightship Semantics subsystem
            if (settings.UseLightshipSemanticSegmentation)
            {
                Log.Info("Creating " + nameof(LightshipSemanticsSubsystem));
                loader.CreateSubsystem<XRSemanticsSubsystemDescriptor, XRSemanticsSubsystem>
                (
                    _semanticsSubsystemDescriptors,
                    "Lightship-Semantics"
                );
            }

            // Create Lightship Scanning subsystem
            if (settings.UseLightshipScanning)
            {
                Log.Info("Creating " + nameof(LightshipScanningSubsystem));
                loader.CreateSubsystem<XRScanningSubsystemDescriptor, XRScanningSubsystem>
                (
                    _scanningSubsystemDescriptors,
                    "Lightship-Scanning"
                );
            }

            if (settings.UseLightshipMeshing)
            {
                // our C# "ghost" creates our meshing module to listen to Unity meshing lifecycle callbacks
                loader.DestroySubsystem<XRMeshSubsystem>();
                var meshingProvider = new LightshipMeshingProvider(LightshipUnityContext.UnityContextHandle);

                // Create Unity integrated subsystem
                loader.CreateSubsystem<XRMeshSubsystemDescriptor, XRMeshSubsystem>
                (
                    _meshingSubsystemDescriptors,
                    "LightshipMeshing"
                );
            }

            if (settings.UseLightshipObjectDetection)
            {
                Log.Info("Creating " + nameof(LightshipObjectDetectionSubsystem));
                loader.CreateSubsystem<XRObjectDetectionSubsystemDescriptor, XRObjectDetectionSubsystem>
                (
                    _objectDetectionSubsystemDescriptors,
                    "Lightship-ObjectDetection"
                );
            }

            if (settings.UseLightshipWorldPositioning)
            {
                Log.Info("Creating " + nameof(LightshipWorldPositioningSubsystem));
                loader.CreateSubsystem<XRWorldPositioningSubsystemDescriptor, XRWorldPositioningSubsystem>
                    (
                        _worldPositioningSubsystemDescriptors,
                        "Lightship-WorldPositioning"
                    );
            }

            return true;
        }

        /// <summary>
        /// Destroys each initialized subsystem.
        /// </summary>
        /// <returns>Always returns `true`.</returns>
        internal bool Deinitialize(ILightshipInternalLoaderSupport loader)
        {
            Log.Info("Destroying lightship subsystems");
            if (loader == null)
            {
                Log.Warning("Loader is null. Assuming system is already deinitialized.");
                return true;
            }

            // Destroy subsystem does a null check, so will just no-op if these subsystems were not created or already destroyed
            loader.DestroySubsystem<XRSemanticsSubsystem>();
            loader.DestroySubsystem<XRPersistentAnchorSubsystem>();
            loader.DestroySubsystem<XROcclusionSubsystem>();
            loader.DestroySubsystem<XRScanningSubsystem>();
            loader.DestroySubsystem<XRMeshSubsystem>();
            loader.DestroySubsystem<XRObjectDetectionSubsystem>();
            loader.DestroySubsystem<XRWorldPositioningSubsystem>();

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
    }
}
