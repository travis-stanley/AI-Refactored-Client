﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   All queue and tick logic is bulletproof, fully isolated, and supports realism-motivated deferred actions.
// </auto-generated>

namespace AIRefactored.AI.Optimization
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using AIRefactored.Core;
    using AIRefactored.Runtime;
    using BepInEx.Logging;

    /// <summary>
    /// Central dispatcher for safely queuing Unity API calls from background threads.
    /// Schedules Unity-related tasks and defers spawns to prevent frame spikes.
    /// Now supports realistic delayed actions for human-like AI reaction times.
    /// Manual Tick(float now) must be called every frame from host.
    /// All failures are fully isolated and logged.
    /// </summary>
    public static class BotWorkScheduler
    {
        #region Constants

        private const int MaxSpawnsPerSecond = 5;
        private const int MaxSpawnQueueSize = 256;
        private const float MinDelay = 0.035f;
        private const float MaxDelay = 0.5f;

        #endregion

        #region Fields

        private static readonly ConcurrentQueue<Action> MainThreadQueue = new ConcurrentQueue<Action>();
        private static readonly Queue<DelayedAction> DelayedQueue = new Queue<DelayedAction>(64);
        private static readonly Queue<Action> SpawnQueue = new Queue<Action>(MaxSpawnQueueSize);
        private static readonly object SpawnLock = new object();
        private static readonly object DelayedLock = new object();

        private static int _queuedCount;
        private static int _executedCount;
        private static int _errorCount;

        private static ManualLogSource _logger;

        #endregion

        #region Public API

        /// <summary>
        /// Schedules a Unity-safe action to run on the next main thread tick.
        /// Bulletproof: exceptions and overflows never break world update.
        /// </summary>
        public static void EnqueueToMainThread(Action action)
        {
            try
            {
                if (action == null || !GameWorldHandler.IsLocalHost())
                {
                    return;
                }
                MainThreadQueue.Enqueue(action);
                Interlocked.Increment(ref _queuedCount);
                EnsureLogger().LogDebug("[BotWorkScheduler] Main thread task queued.");
            }
            catch (Exception ex)
            {
                EnsureLogger().LogWarning("[BotWorkScheduler] EnqueueToMainThread failed: " + ex);
            }
        }

        /// <summary>
        /// Enqueues a Unity-safe action with a randomized delay for realism.
        /// Simulates human-like reaction time in AI actions.
        /// </summary>
        public static void EnqueueToMainThreadDelayed(Action action, float delaySeconds = -1f)
        {
            try
            {
                if (action == null || !GameWorldHandler.IsLocalHost())
                    return;

                float now = Time.unscaledTime;
                float delay = delaySeconds > 0f
                    ? delaySeconds
                    : UnityEngine.Random.Range(MinDelay, MaxDelay);

                lock (DelayedLock)
                {
                    DelayedQueue.Enqueue(new DelayedAction
                    {
                        Action = action,
                        ExecuteAfter = now + delay
                    });
                }
                Interlocked.Increment(ref _queuedCount);
                EnsureLogger().LogDebug("[BotWorkScheduler] Main thread delayed task queued (" + delay.ToString("F3") + "s).");
            }
            catch (Exception ex)
            {
                EnsureLogger().LogWarning("[BotWorkScheduler] EnqueueToMainThreadDelayed failed: " + ex);
            }
        }

        /// <summary>
        /// Enqueues spawn logic for deferred throttling.
        /// Bulletproof: overflow and exceptions never break update.
        /// </summary>
        public static void EnqueueSpawnSmoothed(Action action)
        {
            try
            {
                if (action == null || !GameWorldHandler.IsLocalHost())
                {
                    return;
                }
                lock (SpawnLock)
                {
                    if (SpawnQueue.Count < MaxSpawnQueueSize)
                    {
                        SpawnQueue.Enqueue(action);
                        EnsureLogger().LogDebug("[BotWorkScheduler] Spawn action queued.");
                    }
                    else
                    {
                        EnsureLogger().LogWarning("[BotWorkScheduler] Spawn queue full. Task dropped.");
                    }
                }
            }
            catch (Exception ex)
            {
                EnsureLogger().LogWarning("[BotWorkScheduler] EnqueueSpawnSmoothed failed: " + ex);
            }
        }

        /// <summary>
        /// Ticks the scheduler. Call once per frame from world update.
        /// All queue processing is bulletproof and fully isolated.
        /// </summary>
        public static void Tick(float now)
        {
            try
            {
                if (!GameWorldHandler.IsLocalHost())
                {
                    return;
                }

                ManualLogSource logger = EnsureLogger();

                // Process immediate main thread actions
                while (MainThreadQueue.TryDequeue(out Action task))
                {
                    try
                    {
                        task();
                        Interlocked.Increment(ref _executedCount);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _errorCount);
                        logger.LogWarning("[BotWorkScheduler] Task failed:\n" + ex);
                    }
                }

                // Process delayed main thread actions (simulate human reaction lag)
                lock (DelayedLock)
                {
                    int delayedProcessed = 0;
                    int maxPerFrame = 24;
                    float currentTime = Time.unscaledTime;
                    while (DelayedQueue.Count > 0 && delayedProcessed < maxPerFrame)
                    {
                        DelayedAction da = DelayedQueue.Peek();
                        if (currentTime < da.ExecuteAfter)
                            break;
                        DelayedQueue.Dequeue();
                        try
                        {
                            da.Action?.Invoke();
                            Interlocked.Increment(ref _executedCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref _errorCount);
                            logger.LogWarning("[BotWorkScheduler] Delayed task failed:\n" + ex);
                        }
                        delayedProcessed++;
                    }
                }

                // Process spawn queue, smoothing spikes
                int allowed = Mathf.Max(1, Mathf.FloorToInt(MaxSpawnsPerSecond * Time.unscaledDeltaTime));
                int executed = 0;

                lock (SpawnLock)
                {
                    while (executed < allowed && SpawnQueue.Count > 0)
                    {
                        try
                        {
                            SpawnQueue.Dequeue()?.Invoke();
                            executed++;
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning("[BotWorkScheduler] Spawn failed:\n" + ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EnsureLogger().LogWarning("[BotWorkScheduler] Tick() failed: " + ex);
            }
        }

        /// <summary>
        /// Returns internal scheduler stats for diagnostics.
        /// </summary>
        public static string GetStats()
        {
            try
            {
                return "[BotWorkScheduler] Queued=" + _queuedCount +
                       ", Executed=" + _executedCount +
                       ", Errors=" + _errorCount +
                       ", SpawnQueue=" + SpawnQueue.Count +
                       ", DelayedQueue=" + DelayedQueue.Count;
            }
            catch
            {
                return "[BotWorkScheduler] Stats unavailable.";
            }
        }

        #endregion

        #region Internals

        private static ManualLogSource EnsureLogger()
        {
            if (_logger == null)
            {
                _logger = Plugin.LoggerInstance;
            }
            return _logger;
        }

        private struct DelayedAction
        {
            public Action Action;
            public float ExecuteAfter;
        }

        #endregion
    }
}
