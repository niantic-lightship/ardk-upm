// Copyright 2022-2024 Niantic.
// Restrict inclusion to Android builds to avoid symbol resolution error

#if UNITY_ANDROID || UNITY_EDITOR

#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED && UNITY_ANDROID && !UNITY_EDITOR
#define NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
#endif

using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.PAM;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARCore;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Loader
{
    /// <summary>
    /// Manages the lifecycle of Lightship and ARCore subsystems.
    /// </summary>
    public class LightshipARCoreLoader : ARCoreLoader, ILightshipInternalLoaderSupport
    {
        private const int CameraResolutionMinWidth = DataFormatConstants.Jpeg_720_540_ImgWidth;
        private const int CameraResolutionMinHeight = DataFormatConstants.Jpeg_720_540_ImgHeight;

        public void InjectLightshipLoaderHelper(LightshipLoaderHelper lightshipLoaderHelper)
        {
            _lightshipLoaderHelper = lightshipLoaderHelper;
        }
        private LightshipLoaderHelper _lightshipLoaderHelper;
        private List<ILightshipExternalLoader> _externalLoaders = new();


        /// <summary>
        /// The `XROcclusionSubsystem` whose lifecycle is managed by this loader.
        /// </summary>
        public XROcclusionSubsystem LightshipOcclusionSubsystem => ((XRLoaderHelper) this).GetLoadedSubsystem<XROcclusionSubsystem>();

        /// <summary>
        /// The `XRPersistentAnchorSubsystem` whose lifecycle is managed by this loader.
        /// </summary>
        public XRPersistentAnchorSubsystem LightshipPersistentAnchorSubsystem =>
            ((XRLoaderHelper) this).GetLoadedSubsystem<XRPersistentAnchorSubsystem>();

        /// <summary>
        /// Initializes the loader. This is called from Unity when starting an AR session.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public override bool Initialize()
        {
            var initializationSettings = LightshipSettings.Instance;
            _lightshipLoaderHelper ??= new LightshipLoaderHelper(initializationSettings, _externalLoaders);

            return InitializeWithLightshipHelper(_lightshipLoaderHelper);
        }

        /// <summary>
        /// Initializes the loader with an injected LightshipLoaderHelper. This is a helper to initialize manually from tests.
        /// </summary>
        /// <returns>`True` if the session subsystems were successfully created, otherwise `false`.</returns>
        public bool InitializeWithLightshipHelper(LightshipLoaderHelper lightshipLoaderHelper)
        {
#if NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
            _lightshipLoaderHelper = lightshipLoaderHelper;
            return _lightshipLoaderHelper.Initialize(this);
#else
            return false;
#endif
        }

        // On Android there is no Lidar
        public bool IsPlatformDepthAvailable()
        {
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

        void ILightshipLoader.AddExternalLoader(ILightshipExternalLoader loader)
        {
            _externalLoaders.Add(loader);
        }

        // The default camera image resolution from ARCore is 640x480, which is not large enough for the image
        // input required by VPS. Thus we need to increase the resolution by changing the camera configuration.
        // We need to check on Update if configurations are available, because they aren't available until the
        // XRCameraSubsystem is running (and that's not controlled by Lightship). Fortunately, configurations
        // become available during the gap between when the subsystem starts running and the first frame is
        // surfaced, meaning there's no visible hitch when the configuration is changed.
        private void SelectCameraConfiguration()
        {
            var currentConfig = cameraSubsystem.currentConfiguration;

            if (cameraSubsystem == null || !cameraSubsystem.running || currentConfig == null)
            {
                return;
            }

            var currResolution = currentConfig.Value.resolution;

            // First verify if the current camera configuration is viable
            if (MeetsResolutionMinimums(currResolution, CameraResolutionMinWidth, CameraResolutionMinHeight))
            {
                return;
            }

            Log.Info("Detected current XRCameraConfiguration to not meet resolution requirements");

            // If current camera configuration is not viable, attempt to set it to a viable configuration
            var configurations = cameraSubsystem.GetConfigurations(Allocator.Temp);

            if (configurations.Length == 0)
            {
                return;
            }

            foreach (var config in configurations)
            {
                // Select the first configuration that meets the resolution minimum assuming
                // that the configuration will correspond with the default camera use by ARCore
                if (MeetsResolutionMinimums(config.resolution, CameraResolutionMinWidth, CameraResolutionMinHeight))
                {
                    Log.Info("Setting XRCameraConfiguration as: " + config);
                    cameraSubsystem.currentConfiguration = config;
                    break;
                }
            }
        }

        private bool MeetsResolutionMinimums(Vector2Int resolution, int minWidth, int minHeight)
        {
            return resolution.x >= minWidth && resolution.y >= minHeight;
        }

        /// <summary>
        /// This method does nothing. Subsystems must be started individually.
        /// </summary>
        /// <returns>Returns `true` on Android. Returns `false` otherwise.</returns>
        public override bool Start()
        {
#if NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
            return base.Start();
#else
            return false;
#endif
        }

        /// <summary>
        /// This method does nothing. Subsystems must be stopped individually.
        /// </summary>
        /// <returns>Returns `true` on Android. Returns `false` otherwise.</returns>
        public override bool Stop()
        {
#if NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
            return base.Stop();
#else
            return false;
#endif
        }

        /// <summary>
        /// Destroys each subsystem.
        /// </summary>
        /// <returns>Always returns `true`.</returns>
        public override bool Deinitialize()
        {
#if NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
            return _lightshipLoaderHelper.Deinitialize();
#else
            return true;
#endif
        }

        public bool InitializePlatform()
        {
            MonoBehaviourEventDispatcher.Updating.AddListener(SelectCameraConfiguration);
            return base.Initialize();
        }

        public bool DeinitializePlatform()
        {
            MonoBehaviourEventDispatcher.Updating.RemoveListener(SelectCameraConfiguration);
            return base.Deinitialize();
        }
    }
}

#endif
