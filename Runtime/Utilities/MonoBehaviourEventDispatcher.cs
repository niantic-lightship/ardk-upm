// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Niantic.Lightship.AR.Utilities.Logging.Log;

namespace Niantic.Lightship.AR.Utilities
{
    /// <summary>
    /// Methods subscribed to the static events on this class will be invoked when the corresponding
    /// Unity event is invoked on this MonoBehaviour. Methods can only be subscribed and unsubscribed
    /// from the main Unity thread.
    /// <remarks>
    ///   Class is not entirely thread safe because this ref is only set in Awake (see comment in
    ///   PrioritizingEvent.AddListener for more). But this class is internal and Lightship will never do anything
    ///   multi-threaded during the initialization stage, so it's an acceptable solution.
    /// </remarks>
    /// </summary>
    [DefaultExecutionOrder(int.MinValue)] [AddComponentMenu("")]
    internal sealed class MonoBehaviourEventDispatcher : MonoBehaviour
    {
        internal static string s_gameObjectName = "__lightship_ar_monobehaviour_event_dispatcher__";
        private static MonoBehaviourEventDispatcher s_instance;

        // This component's DefaultExecutionOrder is set to int.minValue, which is the same as ARFoundation's
        // ARSession component and earlier than the other managers. This means that if a listener to the
        // Updating event accesses a manager’s property (ex. a method subscribed to Update accesses the
        // AROcclusionManager.environmentDepthTexture property), it won’t have the most up-to-date data. There are no
        // current use cases like this in ARDK so we're leaving this class as is for now. If such a use case does
        // appear, see if LateUpdating can be used instead of Updating.
        public static readonly PrioritizingEvent Updating = new ();
        public static readonly PrioritizingEvent LateUpdating = new ();
        public static readonly PrioritizingEvent OnApplicationFocusLost = new ();

        private static Thread s_mainThread;
        private static readonly bool s_staticConstructorWasInvoked;

        private bool _wasCreatedByInternalConstructor;

        // Instantiation of the MonoBehaviourEventDispatcher component must be delayed until after scenes load.
        // Therefore, if the static constructor is invoked before scenes are loaded, it'll mark itself as having
        // been invoked, and the CreateAfterSceneLoad method will check if the static constructor was invoked and
        // call Instantiate() if needed. If the static constructor is invoked after scenes are loaded, the
        // CreateAfterSceneLoad will have no-oped, and Instantiate() will be called directly from the constructor.
        static MonoBehaviourEventDispatcher()
        {
            s_staticConstructorWasInvoked = true;

            if (SceneManager.sceneCount > 0)
            {
                Instantiate();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateAfterSceneLoad()
        {
            if (s_staticConstructorWasInvoked && s_instance == null)
            {
                Instantiate();
            }
        }

        // Cache the current thread as the main thread for testing
        internal static void CacheThreadForTesting(Thread thread)
        {
            // If the main thread has already been cached, don't allow it to be overwritten
            // Unless we are resetting the thread to null
            if (s_mainThread != null && thread != null)
            {
                return;
            }

            s_mainThread = thread;
        }

        // Returns true if we can verify the caller is on the main thread
        // If no thread has been cached, return false
        internal static bool IsMainThread()
        {
            if (s_mainThread == null)
            {
                return false;
            }

            return s_mainThread == Thread.CurrentThread;
        }

        private static void Instantiate()
        {
#if NIANTIC_LIGHTSHIP_AR_LOADER_ENABLED
            if (s_instance != null)
            {
                return;
            }

            var go =
                new GameObject(s_gameObjectName, typeof(MonoBehaviourEventDispatcher));

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
                s_mainThread = Thread.CurrentThread;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            Updating.InvokeListeners();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                OnApplicationFocusLost.InvokeListeners();
            }
        }

        private void OnDestroy()
        {
            Updating.Clear();
            LateUpdating.Clear();
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

            private bool _isInvoking;
            private readonly List<Listener> _queuedAddedListeners = new();
            private readonly List<Listener> _queuedRemovedListeners = new();

            public void AddListener(Action callback, int priority = 999)
            {
                // If main thread reference has not yet been initialized, then the method was invoked
                // before the Awake frame, and no MonobehaviourEventDispatcher events happen before Awake,
                // so it's allowed
                // Cannot use IsMainThread because it returns false if the main thread has not yet been cached
                if (s_mainThread != null && Thread.CurrentThread != s_mainThread)
                {
                    Error("AddListener can only be called from the main thread.");
                    return;
                }

                if (_isInvoking)
                {
                    _queuedAddedListeners.Add(new Listener(callback, priority));
                }
                else
                {
                    AddListener(new Listener(callback, priority));
                    SortListenersByPriority();
                }
            }

            private void AddListener(Listener listener)
            {
                _listeners.Add(listener);
            }

            // If multiple instances of this callback are subscribed to this event, only one will be removed.
            // If those instances have different priorities, the only with the lowest priority will be removed.
            // Note:
            //   Removing a listener has a time complexity of O(n) where n is the number of subscribers.
            //   This is fine for now because it's called only a few times and n is very small (< 10).
            public void RemoveListener(Action callback)
            {
                // If main thread reference has not yet been initialized, then the method was invoked
                // before the Awake frame, and no MonobehaviourEventDispatcher events happen before Awake,
                // so it's allowed
                // Cannot use IsMainThread because it returns false if the main thread has not yet been cached
                if (s_mainThread != null && Thread.CurrentThread != s_mainThread)
                {
                    Error("RemoveListener can only be called from the main thread.");
                    return;
                }

                // Will silently no-op if a callback was removed that was not present
                // in the listeners collection, same as C# events.
                var listener = _listeners.Find(e => e.CallbackFunction == callback);
                if (listener != null)
                {
                    if (_isInvoking)
                    {
                        _queuedRemovedListeners.Add(listener);
                    }
                    else
                    {
                        RemoveListener(listener);
                        SortListenersByPriority();
                    }
                }
            }

            private void RemoveListener(Listener listener)
            {
                _listeners.Remove(listener);
            }

            public void InvokeListeners()
            {
                // If main thread reference has not yet been initialized, then the method was invoked
                // before the Awake frame, and no MonobehaviourEventDispatcher events happen before Awake,
                // so it's allowed
                // Cannot use IsMainThread because it returns false if the main thread has not yet been cached
                if (s_mainThread != null && Thread.CurrentThread != s_mainThread)
                {
                    Error("InvokeListeners can only be called from the main thread.");
                    return;
                }

                _isInvoking = true;
                foreach (var listener in _listeners)
                    listener.CallbackFunction.Invoke();

                _isInvoking = false;
                foreach (var listener in _queuedAddedListeners)
                    AddListener(listener);

                foreach (var listener in _queuedRemovedListeners)
                    RemoveListener(listener);

                _queuedAddedListeners.Clear();
                _queuedRemovedListeners.Clear();

                SortListenersByPriority();
            }

            public void Clear()
            {
                _listeners.Clear();
            }

            private void SortListenersByPriority()
            {
                _listeners.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
        }

        private void LateUpdate()
        {
            LateUpdating?.InvokeListeners();
        }
    }
}
