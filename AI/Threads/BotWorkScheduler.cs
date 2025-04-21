#nullable enable

using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
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
        #region Configuration

        private const int MaxWorkerThreads = 4;

        private const float TickLogicInterval = 1f / 30f;      // 30Hz
        private const float TickCombatInterval = 1f / 60f;     // 60Hz
        private const float TickPerceptionInterval = 1f / 60f; // 60Hz

        #endregion

        #region State

        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
        private static readonly List<BotBrain> _bots = new();
        private static readonly object _lock = new();

        private static Thread[]? _workers;
        private static int _nextBotIndex;

        private static bool _headlessInitialized;

        private static float _lastLogicTick;
        private static float _lastCombatTick;
        private static float _lastPerceptionTick;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (!FikaHeadlessDetector.IsHeadless || _headlessInitialized)
                return;

            _headlessInitialized = true;
            StartWorkerThreads();
            Logger.LogInfo("[AIRefactored-Scheduler] 💠 BotWorkScheduler started in Headless Mode.");
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
                    Logger.LogWarning($"[AIRefactored-Scheduler] ❌ Exception in main thread callback: {ex}");
                }
            }
        }

        #endregion

        #region Thread Management

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
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    bot = _bots[_nextBotIndex % _bots.Count];
                    _nextBotIndex++;
                }

                float now = Time.time;

                try
                {
                    if (now - _lastPerceptionTick >= TickPerceptionInterval)
                        bot?.TickPerception(now);

                    if (now - _lastCombatTick >= TickCombatInterval)
                        bot?.TickCombat(now);

                    if (now - _lastLogicTick >= TickLogicInterval)
                        bot?.TickLogic(now);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[BotWorkScheduler] ⚠️ Error in background tick: {ex}");
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

        #endregion

        #region Public API

        /// <summary>
        /// Registers a bot for threaded logic updates (only active in headless mode).
        /// </summary>
        /// <param name="brain">The bot brain to register.</param>
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

        /// <summary>
        /// Enqueues a Unity-thread-safe delegate to be executed on the main thread.
        /// </summary>
        /// <param name="action">The logic to run on the main thread.</param>
        public static void EnqueueToMainThread(Action? action)
        {
            if (action != null)
                _mainThreadQueue.Enqueue(action);
        }

        #endregion
    }
}
