#nullable enable

using AIRefactored.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace AIRefactored.AI.Threads
{
    /// <summary>
    /// Distributes non-Unity bot logic across multiple threads in headless mode.
    /// Handles task batching and safe callback routing to Unity’s main thread.
    /// </summary>
    public class BotWorkScheduler : MonoBehaviour
    {
        private const int MaxWorkerThreads = 4;

        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
        private static readonly List<BotBrain> _bots = new();
        private static readonly object _lock = new();

        private static Thread[]? _workers;
        private static int _nextBotIndex;

        private static bool _headlessInitialized;

        // === Headless Tick Rates ===
        private const float TickLogicInterval = 1f / 30f;     // 30Hz
        private const float TickCombatInterval = 1f / 60f;    // 60Hz
        private const float TickPerceptionInterval = 1f / 60f; // 60Hz

        private static float _lastLogicTick;
        private static float _lastCombatTick;
        private static float _lastPerceptionTick;

        private void Awake()
        {
            if (!FikaHeadlessDetector.IsHeadless || _headlessInitialized)
                return;

            _headlessInitialized = true;
            StartWorkerThreads();
            Debug.Log("[AIRefactored-Scheduler] 💠 BotWorkScheduler started in Headless Mode.");
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AIRefactored-Scheduler] ❌ Exception in main thread callback: {ex.Message}");
                }
            }
        }

        private static void StartWorkerThreads()
        {
            _workers = new Thread[MaxWorkerThreads];
            for (int i = 0; i < MaxWorkerThreads; i++)
            {
                _workers[i] = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = $"BotWorkerThread-{i}"
                };
                _workers[i].Start();
            }
        }

        private static void WorkerLoop()
        {
            while (true)
            {
                BotBrain? bot = null;

                lock (_lock)
                {
                    if (_bots.Count == 0)
                        continue;

                    bot = _bots[_nextBotIndex % _bots.Count];
                    _nextBotIndex++;
                }

                float now = Time.time;

                try
                {
                    if (now - _lastPerceptionTick >= TickPerceptionInterval)
                    {
                        bot?.TickPerception(now);
                    }

                    if (now - _lastCombatTick >= TickCombatInterval)
                    {
                        bot?.TickCombat(now);
                    }

                    if (now - _lastLogicTick >= TickLogicInterval)
                    {
                        bot?.TickLogic(now);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BotWorkScheduler] ⚠️ Error in background tick: {ex.Message}");
                }

                if (now - _lastPerceptionTick >= TickPerceptionInterval)
                    _lastPerceptionTick = now;
                if (now - _lastCombatTick >= TickCombatInterval)
                    _lastCombatTick = now;
                if (now - _lastLogicTick >= TickLogicInterval)
                    _lastLogicTick = now;

                Thread.Sleep(5); // modest throttle to reduce CPU usage
            }
        }

        public static void RegisterBot(BotBrain brain)
        {
            if (!FikaHeadlessDetector.IsHeadless)
                return;

            lock (_lock)
            {
                if (!_bots.Contains(brain))
                    _bots.Add(brain);
            }
        }

        public static void EnqueueToMainThread(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }
}
