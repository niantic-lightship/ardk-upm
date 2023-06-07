using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Niantic.Lightship.AR.Utilities
{
    [DefaultExecutionOrder(Int32.MinValue)]
    internal sealed class _MonoBehaviourEventDispatcher : MonoBehaviour
    {
        private static _MonoBehaviourEventDispatcher _instance;

        // Add other Actions for other MonoBehaviour events as needed.
        public static Action Updating;
        public static Action LateUpdating;

        private static bool _staticConstructorWasInvoked;

        private bool _wasCreatedByInternalConstructor;

        static _MonoBehaviourEventDispatcher()
        {
            Debug.Log("_MonoBehaviourEventDispatcher static constructor invoked");
            _staticConstructorWasInvoked = true;

            if (SceneManager.sceneCount > 0)
            {
                Instantiate();
            }
            else
            {
                Debug.Log("Delaying _MonoBehaviourEventDispatcher construction until after scene load.");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateAfterSceneLoad()
        {
            Debug.Log("_MonoBehaviourEventDispatcher.AfterSceneLoad");
            if (_staticConstructorWasInvoked && _instance == null)
            {
                Instantiate();
            }
        }

        // Instantiation of the _MonoBehaviourEventDispatcher component must be delayed until after scenes load.
        // Therefore, if the static constructor is invoked before scenes are loaded, it'll mark itself as having
        // been invoked, and the CreateAfterSceneLoad method will check if the static constructor was invoked and
        // call Instantiate() if needed. If the static constructor is invoked after scenes are loaded, the
        // CreateAfterSceneLoad will have no-oped, and Instantiate() will be called directly from the constructor.
        private static void Instantiate()
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            if (_instance != null)
                return;

            var go = new GameObject("__lightship_ar_monobehaviour_event_dispatcher__",
                typeof(_MonoBehaviourEventDispatcher));
            go.hideFlags = HideFlags.HideInHierarchy;

            if (Application.isPlaying)
                DontDestroyOnLoad(go);
#endif
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                Debug.Log("_MonoBehaviourEventDispatcher Awake (instance set)");
            }
            else
            {
                Debug.Log("_MonoBehaviourEventDispatcher Awake (instance destroyed)");
                Destroy(this.gameObject);
            }
        }

        private void Update()
        {
            Updating?.Invoke();
        }

        private void LateUpdate()
        {
            LateUpdating?.Invoke();
        }
    }
}
