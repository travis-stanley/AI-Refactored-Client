#nullable enable

using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Central dispatcher for safely queuing Unity API calls from background threads.
    /// Schedules Unity-related tasks and defers smoothed spawns to prevent frame spikes.
    /// Headless-safe. Flushes must be invoked from main thread once per frame.
    /// </summary>
    public static class BotWorkScheduler
    {
        #region Queues and Limits

        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
        private static readonly Queue<Action> _spawnQueue = new(64);

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static int _queuedCount;
        private static int _executedCount;
        private static int _errorCount;

        private static int _maxSpawnsPerSecond = 5;
        private static bool _initialized;

        #endregion

        #region Public API

        /// <summary>
        /// Schedules a Unity-safe action from a background thread to be executed on main thread.
        /// </summary>
        public static void EnqueueToMainThread(Action? action)
        {
            if (action == null || FikaHeadlessDetector.IsHeadless)
                return;

            _mainThreadQueue.Enqueue(action);
            Interlocked.Increment(ref _queuedCount);
        }

        /// <summary>
        /// Enqueues GameObject.Instantiate or similar load-heavy logic for deferred spawn throttling.
        /// </summary>
        public static void EnqueueSpawnSmoothed(Action? spawnAction)
        {
            if (spawnAction == null || FikaHeadlessDetector.IsHeadless)
                return;

            lock (_spawnQueue)
            {
                _spawnQueue.Enqueue(spawnAction);
            }
        }

        /// <summary>
        /// Executes pending Unity-safe calls and batched spawns.
        /// Must be called from Unity's main thread once per frame.
        /// </summary>
        public static void Flush()
        {
            if (FikaHeadlessDetector.IsHeadless)
                return;

            // === Main Thread Execution ===
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke();
                    Interlocked.Increment(ref _executedCount);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _errorCount);
                    Logger.LogWarning($"[BotWorkScheduler] ❌ Main-thread task failed: {ex}");
                }
            }

            // === Smoothed Spawn Execution ===
            int allowedThisFrame = Mathf.Max(1, Mathf.FloorToInt(_maxSpawnsPerSecond * Time.unscaledDeltaTime));
            int executed = 0;

            lock (_spawnQueue)
            {
                while (executed < allowedThisFrame && _spawnQueue.Count > 0)
                {
                    var next = _spawnQueue.Dequeue();
                    try
                    {
                        next.Invoke();
                        executed++;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[BotWorkScheduler] ❌ Spawn task failed: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Injects an invisible MonoBehaviour to run Flush() automatically each frame.
        /// Used in client environments only. Safe for multiplayer or modded servers.
        /// </summary>
        public static void AutoInjectFlushHost()
        {
            if (_initialized || FikaHeadlessDetector.IsHeadless)
                return;

            if (GameObject.Find("AIRefactored.FlushHost") != null)
                return;

            var go = new GameObject("AIRefactored.FlushHost");
            go.AddComponent<FlushRunner>();
            GameObject.DontDestroyOnLoad(go);

            Logger.LogInfo("[BotWorkScheduler] ✅ Flush host injected into scene.");
            _initialized = true;
        }

        /// <summary>
        /// Returns current diagnostic snapshot of workload and execution metrics.
        /// </summary>
        public static string GetStats()
        {
            return $"[BotWorkScheduler] Queued: {_queuedCount}, Executed: {_executedCount}, Errors: {_errorCount}, SpawnQueue: {_spawnQueue.Count}";
        }

        #endregion

        #region MonoBehaviour Hook

        private sealed class FlushRunner : MonoBehaviour
        {
            private void Update()
            {
                Flush();
            }
        }

        #endregion
    }
}
