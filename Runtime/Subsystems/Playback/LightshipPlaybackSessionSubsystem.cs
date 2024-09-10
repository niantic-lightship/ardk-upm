// Copyright 2022-2024 Niantic.
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems.Playback
{
    // Manages PlaybackDatasetReader
    [Preserve]
    public class LightshipPlaybackSessionSubsystem : XRSessionSubsystem, IPlaybackDatasetUser
    {
        /// <summary>
        /// Register the Lightship playback session subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            Log.Info("LightshipPlaybackSessionSubsystem.Register");
            const string id = "Lightship-Playback-Session";
            var info = new XRSessionSubsystemDescriptor.Cinfo()
            {
                id = id,
                providerType = typeof(LightshipPlaybackProvider),
                subsystemTypeOverride = typeof(LightshipPlaybackSessionSubsystem),
                supportsInstall = false,
                supportsMatchFrameRate = true,
            };

            XRSessionSubsystemDescriptor.RegisterDescriptor(info);
        }

        void IPlaybackDatasetUser.SetPlaybackDatasetReader(PlaybackDatasetReader reader)
        {
            ((LightshipPlaybackProvider)provider).datasetReader = reader;
        }

        internal PlaybackDatasetReader GetPlaybackDatasetReader()
        {
            return ((LightshipPlaybackProvider)provider).datasetReader;
        }

        public bool TryMoveToNextFrame()
        {
            return ((LightshipPlaybackProvider)provider).TryMoveToNextFrame();
        }

        private class LightshipPlaybackProvider : Provider
        {
            public PlaybackDatasetReader datasetReader;

            private int m_initialApplicationFramerate;

            public override Promise<SessionAvailability> GetAvailabilityAsync()
            {
                var flag = SessionAvailability.Supported | SessionAvailability.Installed;
                return Promise<SessionAvailability>.CreateResolvedPromise(flag);
            }

            public override TrackingState trackingState
            {
                get => datasetReader?.GetCurrentTrackingState() ?? TrackingState.None;
            }

            public override bool matchFrameRateEnabled => matchFrameRateRequested;

            public override bool matchFrameRateRequested
            {
                get => true;
                set
                {
                    // TODO: investigate how this actually works, what should happen when value is set to false
                    if (value && datasetReader != null)
                    {
                        Application.targetFrameRate = datasetReader.GetFramerate();
                    }
                }
            }

            public override int frameRate => datasetReader?.GetFramerate() ?? 0;

            // start or resume
            public override void Start()
            {
                if (datasetReader == null)
                {
                    Log.Warning("Dataset reader is null, can't start LightshipPlaybackSessionSubsystem");
                    return;
                }

                datasetReader.Reset();
                if (!LightshipSettingsHelper.ActiveSettings.RunPlaybackManually)
                {
                    MonoBehaviourEventDispatcher.Updating.AddListener(MoveToNextFrame);
                }
                else
                {
                    MonoBehaviourEventDispatcher.Updating.AddListener(MoveToNextFrameIfKeyDown);
                }
            }

            // pause
            public override void Stop()
            {
                MonoBehaviourEventDispatcher.Updating.RemoveListener(MoveToNextFrame);
                MonoBehaviourEventDispatcher.Updating.RemoveListener(MoveToNextFrameIfKeyDown);
            }

            public override void Destroy()
            {
                datasetReader = null;
            }

            private void MoveToNextFrame()
            {
                datasetReader.TryMoveToNextFrame();
            }

            private void MoveToNextFrameIfKeyDown()
            {
#if UNITY_EDITOR
                // The Editor may be headless on CI
                if (null == Keyboard.current)
                    return;

                KeyControl space = InputSystem.GetDevice<Keyboard>().spaceKey;
                KeyControl forward = InputSystem.GetDevice<Keyboard>().rightArrowKey;
                KeyControl backward = InputSystem.GetDevice<Keyboard>().leftArrowKey;

                if (space.wasPressedThisFrame)
                    datasetReader.TryMoveToNextFrame();
                else if (space.isPressed)
                    return;

                if (forward.isPressed)
                    datasetReader.TryMoveToNextFrame();

                if (backward.isPressed)
                    datasetReader.TryMoveToPreviousFrame();
#else
                if (Input.touchCount == 2)
                    datasetReader.TryMoveToNextFrame();
#endif
            }

            public bool TryMoveToNextFrame()
            {
                if (running)
                    return datasetReader.TryMoveToNextFrame();

                return false;
            }
        }
    }
}
