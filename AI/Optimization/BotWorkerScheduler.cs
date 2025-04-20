#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using System;
using System.Collections.Concurrent;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Provides a centralized, thread-safe system for enqueuing logic from worker threads back onto Unity's main thread.
    /// Used to safely execute Unity API calls after async logic in headless host environments.
    /// </summary>
    public static class BotWorkScheduler
    {
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        /// <summary>
        /// Enqueues a delegate to be run on the next Unity main thread frame.
        /// </summary>
        public static void EnqueueToMainThread(Action action)
        {
            if (action == null)
                return;

            _mainThreadQueue.Enqueue(action);
        }

        /// <summary>
        /// Called by Unity update loop. Flushes all queued actions to be run immediately.
        /// Should be hooked into a MonoBehaviour running per-frame.
        /// </summary>
        public static void Flush()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"[BotWorkScheduler] ❌ Error running scheduled action: {ex.Message}");
                }
            }
        }
    }
}
