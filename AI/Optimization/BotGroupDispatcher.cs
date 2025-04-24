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
    /// Schedules and dispatches thread-safe bot workloads during headless server execution.
    /// Used for background AI tasks like group evaluation and noise scoring.
    /// </summary>
    public sealed class BotWorkGroupDispatcher : MonoBehaviour
    {
        #region Configuration

        /// <summary>Maximum worker threads allowed to prevent over-scheduling.</summary>
        private const int MaxThreadsCap = 16;

        /// <summary>Max number of workloads processed in a single frame.</summary>
        private const int MaxWorkPerFrame = 256;

        #endregion

        #region Static State

        private static readonly List<IBotWorkload> _pendingWorkloads = new(MaxWorkPerFrame);
        private static readonly object _lock = new();
        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static readonly int LogicalThreadCount = Mathf.Clamp(Environment.ProcessorCount, 1, MaxThreadsCap);

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (!FikaHeadlessDetector.IsHeadless)
                return;

            if (_pendingWorkloads.Count == 0)
                return;

            List<IBotWorkload> batch;

            lock (_lock)
            {
                int count = Mathf.Min(_pendingWorkloads.Count, MaxWorkPerFrame);
                batch = new List<IBotWorkload>(_pendingWorkloads.GetRange(0, count));
                _pendingWorkloads.RemoveRange(0, count);
            }

            DispatchBatch(batch);
        }

        #endregion

        #region Dispatch Logic

        private static void DispatchBatch(List<IBotWorkload> batch)
        {
            int total = batch.Count;
            if (total == 0)
                return;

            int threadCount = Mathf.Clamp(LogicalThreadCount, 1, total);
            int batchSize = Mathf.CeilToInt(total / (float)threadCount);

            for (int i = 0; i < total; i += batchSize)
            {
                int start = i;
                int end = Mathf.Min(start + batchSize, total);

                Task.Run(() =>
                {
                    for (int j = start; j < end; j++)
                    {
                        try
                        {
                            batch[j].RunBackgroundWork();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[AIRefactored] ❌ Exception in background task: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                });
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Queues a workload for background processing. Safe from any thread.
        /// </summary>
        public static void Schedule(IBotWorkload? workload)
        {
            if (workload == null)
                return;

            lock (_lock)
            {
                if (_pendingWorkloads.Count >= MaxWorkPerFrame)
                {
                    Logger.LogWarning("[AIRefactored] ⚠ BotWorkGroupDispatcher queue full. Task dropped.");
                    return;
                }

                _pendingWorkloads.Add(workload);
            }
        }

        #endregion
    }

    /// <summary>
    /// Interface for asynchronous background-safe bot workloads.
    /// </summary>
    public interface IBotWorkload
    {
        /// <summary>
        /// Called from a thread pool. Must not call Unity APIs.
        /// </summary>
        void RunBackgroundWork();
    }
}
