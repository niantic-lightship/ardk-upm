// Copyright 2022-2025 Niantic.
// Restrict inclusion to Android builds to avoid symbol resolution error

#if UNITY_ANDROID || UNITY_EDITOR

#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED && UNITY_ANDROID && !UNITY_EDITOR
#define NIANTIC_LIGHTSHIP_ARCORE_LOADER_ENABLED
#endif

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.PAM;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.XRSubsystems;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARCore;
using UnityEngine.XR.ARFoundation;
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
        //
        // Note #1:
        //      Resetting the ARSession causes the camera configuration to be reset to the default. So this method
        //      has to continuously check for that.
        //
        // Note #2:
        //      If the camera configuration is changed while not all camera cpu images have been
        //      released (as can happen when the ARSession is reset), setting the camera configuration
        //      will fail with: CameraConfigurationResult.ErrorImagesNotDispose. ARDK only holds camera
        //      images in the PAM, and releases those in the SubsystemsDataAcquirer.OnSessionStateChanged
        //      method when it detects an ARSession reset. This method will then upgrade the camera configuration
        //      while the ARSession is in its Initializing state, and PAM won't acquire images again until the
        //      ARSession is in the Tracking state.
        //
        //      If the developer is holding any camera cpu images, we catch the exception thrown by the
        //      ARCoreCameraSubsystem and surface it with more context in the log.
        //
        // Note #3:
        //      The ARCore native call to set a new camera configuration is async -- it takes a frame to kick in.
        //      To avoid making duplicate calls, we set a flag to wait for the next frame before checking the
        //      current camera configuration again.
        private bool _waitForCameraConfigChangeToApply = true;
        private void UpgradeCameraConfigurationIfNeeded()
        {
            if (cameraSubsystem == null) { return; }

            var currentConfig = cameraSubsystem.currentConfiguration;
            if (!cameraSubsystem.running || currentConfig == null) { return; }

            if (_waitForCameraConfigChangeToApply)
            {
                _waitForCameraConfigChangeToApply = false;
                return;
            }

            var currResolution = currentConfig.Value.resolution;

            // First verify if the current camera configuration is viable, so we can exit early
            // TODO [ARDK-4995]: Don't upgrade ARCore camera resolution unless needed by an enabled module
            if (MeetsResolutionMinimums(currResolution, CameraResolutionMinWidth, CameraResolutionMinHeight))
            {
                return;
            }

            Log.Info
            (
                "Detected that the current XRCameraConfiguration does not meet Lightship's resolution requirements " +
                $"for an enabled feature (needs {CameraResolutionMinWidth}x{CameraResolutionMinHeight} has {currResolution.x}x{currResolution.y}). " +
                "Make sure to disable unneeded features in LightshipSettings to avoid using a higher camera " +
                "resolution than needed."
            );

            // If current camera configuration is not viable, attempt to set it to a viable configuration
            var configurations = cameraSubsystem.GetConfigurations(Allocator.Temp);

            // Pretty sure that GetConfigurations always returns a non-empty array if currentConfiguration is not null.
            // But we'll do the check anyway.
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
                        Log.Debug("Found first valid XRCameraConfiguration: " + configurations[i]);
                    }
                    else if (IsSecondConfigBetter(configurations[bestConfigIndex], configurations[i]))
                    {
                        bestConfigIndex = i;
                        Log.Debug("Found a better XRCameraConfiguration: " + configurations[i]);
                    }
                }
            }

            if (bestConfigIndex >= 0)
            {
                try
                {
                    cameraSubsystem.currentConfiguration = configurations[bestConfigIndex];
                    _waitForCameraConfigChangeToApply = true;
                    Log.Info("Upgraded to XRCameraConfiguration: " + configurations[bestConfigIndex]);
                }
                catch (InvalidOperationException e)
                {
                    Log.Error
                    (
                        "Failed to upgrade the camera configuration to the required minimum resolution. " +
                        "We only expect this to happen when the ARSession was reset when not all camera images " +
                        "were disposed. Check the exception from ARCoreCameraSubsystem to verify that is the case," +
                        "and if so, make sure to dispose all camera XRCpuImages before resetting the ARSession."
                    );

                    throw e;
                }
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
            // TODO: Check if this is needed when just using contextual awareness
            MonoBehaviourEventDispatcher.Updating.AddListener(UpgradeCameraConfigurationIfNeeded);
            return base.Initialize();
        }

        public bool DeinitializePlatform()
        {
            Debug.Log("Deinitialize Platform");
            MonoBehaviourEventDispatcher.Updating.RemoveListener(UpgradeCameraConfigurationIfNeeded);
            return base.Deinitialize();
        }
    }
}

#endif
