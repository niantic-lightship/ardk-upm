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
            _lightshipLoaderHelper = new LightshipLoaderHelper(_externalLoaders);
            return InitializeWithLightshipHelper(_lightshipLoaderHelper);
        }

        public bool InitializeWithLightshipHelper(LightshipLoaderHelper lightshipLoaderHelper)
        {
            _lightshipLoaderHelper = lightshipLoaderHelper;
            return _lightshipLoaderHelper.Initialize(this);
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

            int bestConfigIndex = -1;
            for (int i = 0; i < configurations.Length; ++i)
            {
                // Select the "best" configuration that meets the resolution minimum.
                if (MeetsResolutionMinimums(configurations[i].resolution, CameraResolutionMinWidth, CameraResolutionMinHeight))
                {
                    if (bestConfigIndex == -1)
                    {
                        bestConfigIndex = i;
                        Log.Info("Found first valid XRCameraConfiguration: " + configurations[i]);
                    }
                    else if (IsSecondConfigBetter(configurations[bestConfigIndex], configurations[i]))
                    {
                        bestConfigIndex = i;
                        Log.Info("Found a better XRCameraConfiguration: " + configurations[i]);
                    }
                }
            }

            if (bestConfigIndex >= 0)
            {
                cameraSubsystem.currentConfiguration = configurations[bestConfigIndex];
                Log.Info("Using XRCameraConfiguration: " + configurations[bestConfigIndex]);
            }
            else
            {
                Log.Warning("No valid camera configurations found.");
            }
        }

        private bool MeetsResolutionMinimums(Vector2Int resolution, int minWidth, int minHeight)
        {
            return resolution.x >= minWidth && resolution.y >= minHeight;
        }

        // Returns true if the second argument is a "better" camera configuration than the first one.
        // Here, "better" means: lowest framerate within the range 25-59 Hz. In order to
        // maintain device compatibility, the resolution must exactly match the first resolution.
        // Some devices with multiple cameras (e.g. Samsung S10) will return the preferred AR
        // camera first. The other cameras will have a different resolution, so can be skipped.
        //
        // If one config is not clearly better, then the first config will be preferred. This
        // preserves the previous ARDK behavior, where the first config was always used.
        // Using a camera framerate below 60Hz is needed to prevent performance/thermal
        // issues on some devices.
        private bool IsSecondConfigBetter(XRCameraConfiguration first, XRCameraConfiguration second)
        {
            const int MinFrameRate = 25;
            const int MaxFrameRate = 60;

            if ((first.height != second.height) || (first.width != second.width))
            {
                // When resolution differs, always go with the first config.
                return false;
            }

            if (!first.framerate.HasValue)
            {
                // Always prefer a known framerate that is less than the max framerate.
                if (second.framerate.HasValue && second.framerate.Value < MaxFrameRate) return true;

                // Second framerate is either too fast or unknown.
                return false;
            }
            else if (first.framerate == second.framerate)
            {
                // When all else is equal, prefer the first framerate over the second.
                return false;
            }
            else if (second.framerate.HasValue)
            {
                // Both framerates are known...
                if (first.framerate.Value >= MinFrameRate && second.framerate.Value >= MinFrameRate)
                {
                    // Prefer the lowest framerate that is above the minimum.
                    return second.framerate.Value < first.framerate.Value;
                }
                else if (first.framerate.Value >= MinFrameRate)
                {
                    // Second framerate is below the minimum, so prefer the first.
                    return false;
                }
                else if (second.framerate.Value >= MinFrameRate)
                {
                    // First framerate is below the minimum, so prefer the second.
                    return true;
                }
                // Both first and second are below the minimum, so prefer the highest framerate.
                return second.framerate.Value > first.framerate.Value;
            }

            // Always fallback to the first config.
            return false;
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
