using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = System.Object;

namespace Niantic.Lightship.AR.Utilities
{
    [DefaultExecutionOrder(Int32.MinValue)]
    internal sealed class _MonoBehaviourEventDispatcher : MonoBehaviour
    {
        private static _MonoBehaviourEventDispatcher _instance;

        public static readonly PrioritizingEvent Updating = new ();
        public static readonly PrioritizingEvent LateUpdating = new ();

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
            Updating.InvokeListeners();
        }

        // Class to subscribe to Unity Monobehaviour events with a priority.
        // Lowest priority will be called first.
        private class Listener
        {
            private Action _callbackFunction;

            public Action CallbackFunction => _callbackFunction;

            private int _priority;

            public int Priority => _priority;

            public Listener(Action callback, int priority)
            {
                _callbackFunction = callback;
                _priority = priority;
            }
        }

        public class PrioritizingEvent
        {
            private List<Listener> _listeners = new ();

            public void AddListener(Action callback, int priority = 999)
            {
                _listeners.Add(new Listener(callback, priority));
                _listeners.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }

            public void RemoveListener(Action callback)
            {
                foreach (var listener in _listeners)
                {
                    if (listener.CallbackFunction == callback)
                    {
                        _listeners.Remove(listener);
                        return;
                    }
                }
            }

            public void InvokeListeners()
            {
                foreach (var listener in _listeners)
                    listener.CallbackFunction.Invoke();
            }
        }

        private void LateUpdate()
        {
            LateUpdating?.InvokeListeners();
        }
    }
}
