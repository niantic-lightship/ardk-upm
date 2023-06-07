using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Playback
{
    // Manages PlaybackDatasetReader
    [Preserve]
    public class LightshipPlaybackSessionSubsystem : XRSessionSubsystem, _IPlaybackDatasetUser, _ILightshipSettingsUser
    {
        /// <summary>
        /// Register the Lightship playback session subsystem.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Debug.Log("LightshipPlaybackSessionSubsystem.Register");
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

        void _IPlaybackDatasetUser.SetPlaybackDatasetReader(_PlaybackDatasetReader reader)
        {
            ((LightshipPlaybackProvider)provider).datasetReader = reader;
        }

        void _ILightshipSettingsUser.SetLightshipSettings(LightshipSettings settings)
        {
            ((LightshipPlaybackProvider)provider).lightshipSettings = settings;
        }

        public bool TryMoveToNextFrame()
        {
            return ((LightshipPlaybackProvider)provider).TryMoveToNextFrame();
        }

        private class LightshipPlaybackProvider : Provider
        {
            public _PlaybackDatasetReader datasetReader;
            public LightshipSettings lightshipSettings;

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
                if ((!lightshipSettings.RunManuallyEditor && Application.isEditor) || (!lightshipSettings.RunManuallyDevice && !Application.isEditor))
                    _MonoBehaviourEventDispatcher.Updating += MoveToNextFrame;
                else
                    _MonoBehaviourEventDispatcher.Updating += MoveToNextFrameIfKeyDown;
            }

            // pause
            public override void Stop()
            {
                _MonoBehaviourEventDispatcher.Updating -= MoveToNextFrame;
                _MonoBehaviourEventDispatcher.Updating -= MoveToNextFrameIfKeyDown;
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
                const KeyCode single = KeyCode.Space;
                const KeyCode continuous = KeyCode.RightArrow;

                if (Input.GetKeyUp(single))
                    datasetReader.TryMoveToNextFrame();
                else if (Input.GetKey(single) || Input.GetKeyDown(single))
                    return;

                if (Input.GetKey(continuous))
                    datasetReader.TryMoveToNextFrame();
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
