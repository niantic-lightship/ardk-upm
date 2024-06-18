// Copyright 2022-2024 Niantic.

using System.Collections.Generic;

using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Subsystems.Meshing;
using Niantic.Lightship.AR.Subsystems.Playback;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;

using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Loader
{
    public class LightshipSimulationLoader : XRLoaderHelper, ILightshipInternalLoaderSupport
    {
        private static List<XRSessionSubsystemDescriptor> s_SessionSubsystemDescriptors = new ();
        private static List<XRInputSubsystemDescriptor> s_InputSubsystemDescriptors = new ();
        private static List<XRPlaneSubsystemDescriptor> s_PlaneSubsystemDescriptors = new ();
        private static List<XRPointCloudSubsystemDescriptor> s_PointCloudSubsystemDescriptors = new ();
        private static List<XRImageTrackingSubsystemDescriptor> s_ImageTrackingSubsystemDescriptors = new ();
        private static List<XRRaycastSubsystemDescriptor> s_RaycastSubsystemDescriptors = new ();
        private static List<XRMeshSubsystemDescriptor> s_MeshSubsystemDescriptors  = new ();
        private static List<XRCameraSubsystemDescriptor> s_CameraSubsystemDescriptors = new();
        private static List<XROcclusionSubsystemDescriptor> _occlusionSubsystemDescriptors = new();
        private static List<XRPersistentAnchorSubsystemDescriptor> s_persistentAnchorSubsystemDescriptors = new();

        public void InjectLightshipLoaderHelper(LightshipLoaderHelper lightshipLoaderHelper)
        {
            _lightshipLoaderHelper = lightshipLoaderHelper;
        }

        private LightshipLoaderHelper _lightshipLoaderHelper;
        private readonly List<ILightshipExternalLoader> _externalLoaders = new List<ILightshipExternalLoader>();
        private bool _useZBufferDepth = true;
        private bool _useSimulationPersistentAnchors = true;
        private XRPersistentAnchorSubsystem _lightshipPersistentAnchorSubsystem;

        /// <summary>
        /// Initializes the loader. This is called from Unity when starting an AR session.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public override bool Initialize()
        {
            var originalSettings = LightshipSettings.Instance;
            _useSimulationPersistentAnchors = LightshipSettings.Instance.LightshipSimulationParams.UseSimulationPersistentAnchor;

            _useZBufferDepth = LightshipSettings.Instance.LightshipSimulationParams.UseZBufferDepth;

            // we create new settings with playback disabled
            var settingsWithoutPlayback =
                LightshipSettings._CreateRuntimeInstance
                (
                    // Workaround for https://niantic.atlassian.net/browse/ARDK-3019
                    // we disable lightship depth if we're use z-buffer depth
                    enableDepth: originalSettings.UseLightshipDepth && !_useZBufferDepth,
                    enableMeshing: originalSettings.UseLightshipMeshing,
                    enablePersistentAnchors: !_useSimulationPersistentAnchors,
                    // Workaround for https://niantic.atlassian.net/browse/ARDK-1868
                    // we disable playback, can be removed once this is part of standalone loader
                    usePlayback: false,
                    playbackDataset: "",
                    runPlaybackManually: false,
                    loopPlaybackInfinitely: false,
                    numberOfPlaybackLoops: 1,
                    apiKey: originalSettings.ApiKey,
                    enableSemanticSegmentation: originalSettings.UseLightshipSemanticSegmentation,
                    preferLidarIfAvailable: originalSettings.PreferLidarIfAvailable,
                    enableObjectDetection: originalSettings.UseLightshipObjectDetection,
                    enableScanning: originalSettings.UseLightshipScanning,
                    unityLogLevel: originalSettings.UnityLightshipLogLevel,
                    ardkConfiguration: null,
                    stdoutLogLevel: originalSettings.StdOutLightshipLogLevel,
                    fileLogLevel: originalSettings.FileLightshipLogLevel,
                    tickPamOnUpdate: originalSettings.TestSettings.TickPamOnUpdate,
                    disableTelemetry: originalSettings.TestSettings.DisableTelemetry
                );
            _lightshipLoaderHelper ??= new LightshipLoaderHelper(settingsWithoutPlayback, _externalLoaders);

            return InitializeWithLightshipHelper(_lightshipLoaderHelper);
        }

        /// <summary>
        /// Initializes the loader with an injected LightshipLoaderHelper. This is a helper to initialize manually from tests.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public bool InitializeWithLightshipHelper(LightshipLoaderHelper lightshipLoaderHelper)
        {
            _lightshipLoaderHelper = lightshipLoaderHelper;
            _lightshipLoaderHelper.Initialize(this);

            if (_useSimulationPersistentAnchors)
            {
                CreateSubsystem<XRPersistentAnchorSubsystemDescriptor, XRPersistentAnchorSubsystem>
                (
                    s_persistentAnchorSubsystemDescriptors,
                    "Lightship-Simulation-PersistentAnchor"
                );

                _lightshipPersistentAnchorSubsystem = XRGeneralSettings.Instance.Manager.activeLoaders[0].GetLoadedSubsystem<XRPersistentAnchorSubsystem>();
            }

            return true;
        }

        /// <summary>
        /// Destroys each subsystem.
        /// </summary>
        /// <returns>Always returns `true`.</returns>
        public override bool Deinitialize()
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            return _lightshipLoaderHelper.Deinitialize();
#else
            return true;
#endif
        }

        public bool InitializePlatform()
        {
            var input = new LightshipInputProvider();

            CreateSubsystem<XRSessionSubsystemDescriptor, XRSessionSubsystem>(s_SessionSubsystemDescriptors,
                "XRSimulation-Session");
            CreateSubsystem<XRInputSubsystemDescriptor, XRInputSubsystem>(s_InputSubsystemDescriptors,
                "LightshipInput");
            CreateSubsystem<XRPlaneSubsystemDescriptor, XRPlaneSubsystem>(s_PlaneSubsystemDescriptors,
                "XRSimulation-Plane");
            CreateSubsystem<XRPointCloudSubsystemDescriptor, XRPointCloudSubsystem>(s_PointCloudSubsystemDescriptors,
                "XRSimulation-PointCloud");
            CreateSubsystem<XRImageTrackingSubsystemDescriptor, XRImageTrackingSubsystem>(
                s_ImageTrackingSubsystemDescriptors, "XRSimulation-ImageTracking");
            CreateSubsystem<XRRaycastSubsystemDescriptor, XRRaycastSubsystem>(s_RaycastSubsystemDescriptors,
                "XRSimulation-Raycast");
            CreateSubsystem<XRCameraSubsystemDescriptor, XRCameraSubsystem>(s_CameraSubsystemDescriptors,
                "Lightship-XRSimulation-Camera");
            if (_useZBufferDepth)
            {
                CreateSubsystem<XROcclusionSubsystemDescriptor, XROcclusionSubsystem>(_occlusionSubsystemDescriptors,
                    "Lightship-Simulation-Occlusion");
            }

            if (GetLoadedSubsystem<XRSessionSubsystem>() == null)
            {
                Log.Error("Failed to load session subsystem.");
                return false;
            }

            return true;
        }

        public bool DeinitializePlatform()
        {
            DestroySubsystem<XRRaycastSubsystem>();
            DestroySubsystem<XRImageTrackingSubsystem>();
            DestroySubsystem<XRPointCloudSubsystem>();
            DestroySubsystem<XRPlaneSubsystem>();
            DestroySubsystem<XRInputSubsystem>();
            DestroySubsystem<XRCameraSubsystem>();
            DestroySubsystem<XRSessionSubsystem>();
            DestroySubsystem<XRPersistentAnchorSubsystem>();

            return true;
        }

        public bool IsPlatformDepthAvailable()
        {
            return _useZBufferDepth;
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

        void ILightshipLoader.AddExternalLoader(ILightshipExternalLoader loader)
        {
            _externalLoaders.Add(loader);
        }
    }
}
