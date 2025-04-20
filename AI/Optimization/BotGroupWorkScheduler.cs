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
    /// Ensures optimized scheduling and safe return to Unity main thread.
    /// </summary>
    public class BotWorkGroupDispatcher : MonoBehaviour
    {
        private static readonly List<IBotWorkload> _activeWorkloads = new();
        private static readonly object _lock = new();

        private const int MaxThreads = 8; // Cap usage to avoid oversaturation
        private static int _nextThreadIndex = 0;

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        private void Update()
        {
            if (!FikaHeadlessDetector.IsHeadless)
                return;

            lock (_lock)
            {
                if (_activeWorkloads.Count == 0)
                    return;

                var snapshot = new List<IBotWorkload>(_activeWorkloads);
                _activeWorkloads.Clear();

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
                                _log.LogWarning($"[BotWorkGroup] ⚠ Background work exception: {ex.Message}");
                            }
                        }
                    });
                }
            }
        }

        public static void Schedule(IBotWorkload workload)
        {
            if (workload == null)
                return;

            lock (_lock)
            {
                _activeWorkloads.Add(workload);
            }
        }
    }

    /// <summary>
    /// Interface for bot modules to implement multithreaded behavior logic.
    /// Only called on background threads.
    /// </summary>
    public interface IBotWorkload
    {
        void RunBackgroundWork();
    }
}
