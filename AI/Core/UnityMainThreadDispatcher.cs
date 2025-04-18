#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.Core
{
    /// <summary>
    /// Allows background threads to safely schedule logic to run on Unity’s main thread.
    /// Used by async logic like <see cref="AI.Threads.BotAsyncThinker"/> to queue main-thread Unity API calls.
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        #region Static Fields

        /// <summary>
        /// Thread-safe queue of actions to run on the main thread.
        /// </summary>
        private static readonly Queue<Action> _queue = new Queue<Action>();

        /// <summary>
        /// Singleton instance.
        /// </summary>
        private static UnityMainThreadDispatcher? _instance;

        #endregion

        #region Public API

        /// <summary>
        /// Thread-safe method to enqueue actions to be run on the next Unity Update.
        /// </summary>
        /// <param name="action">The delegate to invoke on the main thread.</param>
        public static void Enqueue(Action action)
        {
            if (action == null)
                return;

            lock (_queue)
            {
                _queue.Enqueue(action);
            }
        }

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Executes all queued actions each frame on the main thread.
        /// </summary>
        private void Update()
        {
            lock (_queue)
            {
                while (_queue.Count > 0)
                {
                    var action = _queue.Dequeue();
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[UnityMainThreadDispatcher] ❌ Exception while executing action: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Sets singleton instance and prevents duplicates across scene loads.
        /// </summary>
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #endregion

        #region Static Bootstrap

        /// <summary>
        /// Ensures the dispatcher is created once after scene load.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitOnLoad()
        {
            if (_instance != null)
                return;

            GameObject dispatcherObject = new GameObject("UnityMainThreadDispatcher");
            dispatcherObject.AddComponent<UnityMainThreadDispatcher>();
        }

        #endregion
    }
}
