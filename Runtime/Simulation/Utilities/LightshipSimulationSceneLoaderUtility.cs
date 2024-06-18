using System.Collections;
using System.Collections.Generic;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.Simulation
{
    /*
     * Workaround for ARDK-2362.
     * Why this is necessary:
     *
     * According to Unity,
     * The reason for simulation failing when switching scenes is due to XR not being "properly de-initialized"
     * when switching scenes. They pointed to the ARF samples, where XR is re-initialized on scene change
     * (https://github.com/Unity-Technologies/arfoundation-samples/blob/main/Assets/Scripts/Runtime/SceneUtility.cs).
     *
     * When asked if this is a necessary step all the time, they said it is "only required when using Simulation".
     * The solution, then is to add this logic to every project, wrapped in #if UNITY_EDITOR.
     * However, since we provide both playback and simulation in-editor, it would be ideal in simulation only.
     * Rather than passing the work of detecting simulation to the users, just force re-init on scene load.
     */
    internal static class LightshipSimulationSceneLoaderUtility
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null &&
                XRGeneralSettings.Instance.Manager.activeLoader is LightshipSimulationLoader &&
                SceneManager.GetActiveScene().name == scene.name)
            {
                Log.Info("Deinitializing XR loader from LightshipSimulationSceneLoaderUtility...");
                XRGeneralSettings.Instance.Manager.DeinitializeLoader();

                Log.Info("Initializing XR loader from LightshipSimulationSceneLoaderUtility...");
                XRGeneralSettings.Instance.Manager.InitializeLoaderSync();
            }
        }
    }
}
