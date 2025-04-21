#nullable enable

using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Assigns and distributes CPU-heavy logic across all bots in a group using background threads.
    /// Ensures optimized scheduling and safe return to Unity main thread when needed.
    /// Only runs under FIKA Headless mode.
    /// </summary>
    public class BotWorkGroupDispatcher : MonoBehaviour
    {
        #region Configuration

        private const int MaxThreads = 8;

        #endregion

        #region State

        private static readonly List<IBotWorkload> _pendingWorkloads = new List<IBotWorkload>(128);
        private static readonly object _lock = new object();

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Processes queued workloads and dispatches them to background threads.
        /// Executed only on headless servers for AI optimization.
        /// </summary>
        private void Update()
        {
            if (!FikaHeadlessDetector.IsHeadless)
                return;

            List<IBotWorkload> snapshot;

            lock (_lock)
            {
                if (_pendingWorkloads.Count == 0)
                    return;

                snapshot = new List<IBotWorkload>(_pendingWorkloads);
                _pendingWorkloads.Clear();
            }

            int count = snapshot.Count;
            int batchSize = Mathf.Max(1, count / MaxThreads);

            for (int i = 0; i < count; i += batchSize)
            {
                int start = i;
                int end = Mathf.Min(i + batchSize, count);

                Task.Run(() =>
                {
                    for (int j = start; j < end; j++)
                    {
                        try
                        {
                            snapshot[j].RunBackgroundWork();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[BotWorkGroupDispatcher] ⚠ Exception in background workload: {ex}");
                        }
                    }
                });
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Queues a workload to be executed on the next available worker thread.
        /// </summary>
        /// <param name="workload">A background-compatible workload to be processed.</param>
        public static void Schedule(IBotWorkload? workload)
        {
            if (workload == null)
                return;

            lock (_lock)
            {
                _pendingWorkloads.Add(workload);
            }
        }

        #endregion
    }

    /// <summary>
    /// Interface for bot modules to implement multithreaded behavior logic.
    /// Only invoked on background threads. Must not touch Unity API directly.
    /// </summary>
    public interface IBotWorkload
    {
        /// <summary>
        /// Executes thread-safe logic outside the Unity main thread.
        /// </summary>
        void RunBackgroundWork();
    }
}
