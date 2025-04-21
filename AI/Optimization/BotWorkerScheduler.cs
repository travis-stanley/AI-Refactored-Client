#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Provides a centralized, thread-safe system for enqueuing logic from worker threads
    /// back onto Unity's main thread. Used to safely execute Unity API calls after async logic
    /// in headless or parallel environments.
    /// </summary>
    public static class BotWorkScheduler
    {
        #region Fields

        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static int _queuedCount = 0;
        private static int _executedCount = 0;
        private static int _errorCount = 0;

        #endregion

        #region Public API

        /// <summary>
        /// Enqueues a delegate to be executed on the next Unity main thread frame.
        /// </summary>
        /// <param name="action">The action to run on the main thread. Ignored if null.</param>
        public static void EnqueueToMainThread(Action? action)
        {
            if (action == null)
                return;

            _mainThreadQueue.Enqueue(action);
            Interlocked.Increment(ref _queuedCount);
        }

        /// <summary>
        /// Called by Unity's update loop. Executes all queued main-thread actions.
        /// Should be called from a MonoBehaviour during each frame on the main thread.
        /// </summary>
        public static void Flush()
        {
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
                    Logger.LogWarning($"[BotWorkScheduler] ❌ Scheduled action failed: {ex}");
                }
            }
        }

        /// <summary>
        /// Optional diagnostic method to get runtime stats on queued and executed actions.
        /// </summary>
        /// <returns>A formatted string containing queue statistics.</returns>
        public static string GetStats()
        {
            return $"[BotWorkScheduler] Queued: {_queuedCount}, Executed: {_executedCount}, Errors: {_errorCount}";
        }

        #endregion
    }
}
