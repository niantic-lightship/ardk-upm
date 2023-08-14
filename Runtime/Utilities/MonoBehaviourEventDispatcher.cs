using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = System.Object;

namespace Niantic.Lightship.AR.Utilities
{
    [DefaultExecutionOrder(int.MinValue)]
    internal sealed class MonoBehaviourEventDispatcher : MonoBehaviour
    {
        private static MonoBehaviourEventDispatcher s_instance;

        public static readonly PrioritizingEvent Updating = new ();
        public static readonly PrioritizingEvent LateUpdating = new ();

        private static readonly bool s_staticConstructorWasInvoked;

        private bool _wasCreatedByInternalConstructor;

        static MonoBehaviourEventDispatcher()
        {
            Debug.Log("MonoBehaviourEventDispatcher static constructor invoked");
            s_staticConstructorWasInvoked = true;

            if (SceneManager.sceneCount > 0)
            {
                Instantiate();
            }
            else
            {
                Debug.Log("Delaying MonoBehaviourEventDispatcher construction until after scene load.");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateAfterSceneLoad()
        {
            Debug.Log("MonoBehaviourEventDispatcher.AfterSceneLoad");
            if (s_staticConstructorWasInvoked && s_instance == null)
            {
                Instantiate();
            }
        }

        // Instantiation of the MonoBehaviourEventDispatcher component must be delayed until after scenes load.
        // Therefore, if the static constructor is invoked before scenes are loaded, it'll mark itself as having
        // been invoked, and the CreateAfterSceneLoad method will check if the static constructor was invoked and
        // call Instantiate() if needed. If the static constructor is invoked after scenes are loaded, the
        // CreateAfterSceneLoad will have no-oped, and Instantiate() will be called directly from the constructor.
        private static void Instantiate()
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            if (s_instance != null)
            {
                return;
            }

            var go = new GameObject("__lightship_ar_monobehaviour_event_dispatcher__",
                typeof(MonoBehaviourEventDispatcher));
            go.hideFlags = HideFlags.HideInHierarchy;

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
#endif
        }

        private void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this;
                Debug.Log("MonoBehaviourEventDispatcher Awake (instance set)");
            }
            else
            {
                Debug.Log("MonoBehaviourEventDispatcher Awake (instance destroyed)");
                Destroy(this.gameObject);
            }
        }

        private void Update()
        {
            Updating.InvokeListeners();
        }

        /// <summary>
        /// Class to subscribe to Unity Monobehaviour events with a priority.
        /// Lowest priority will be called first.
        /// </summary>
        private class Listener
        {
            public Action CallbackFunction { get; }

            public int Priority { get; }

            public Listener(Action callback, int priority)
            {
                CallbackFunction = callback;
                Priority = priority;
            }
        }

        public class PrioritizingEvent
        {
            private readonly List<Listener> _listeners = new ();

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
