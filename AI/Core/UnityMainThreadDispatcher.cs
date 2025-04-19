#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.Core
{
    /// <summary>
    /// Allows background threads to safely schedule logic to run on Unity’s main thread.
    /// Used by async logic like <see cref="AI.Threads.BotAsyncThinker"/> to queue main-thread Unity API calls.
    /// Automatically disabled in FIKA Headless environments.
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        #region Static Fields

        private static readonly Queue<Action> _queue = new Queue<Action>();
        private static UnityMainThreadDispatcher? _instance;

        #endregion

        #region Public API

        public static void Enqueue(Action action)
        {
            if (action == null || FikaHeadlessDetector.IsHeadless)
                return;

            lock (_queue)
            {
                _queue.Enqueue(action);
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (FikaHeadlessDetector.IsHeadless)
                return;

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

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (!FikaHeadlessDetector.IsHeadless)
                DontDestroyOnLoad(gameObject);
        }

        #endregion

        #region Static Bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitOnLoad()
        {
            if (FikaHeadlessDetector.IsHeadless || _instance != null)
                return;

            GameObject dispatcherObject = new GameObject("UnityMainThreadDispatcher");
            dispatcherObject.AddComponent<UnityMainThreadDispatcher>();
        }

        #endregion
    }
}
