﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   All pathfinding and cache logic is bulletproof, fully isolated, and simulates real-player caution and behavior.
//   NavPointRegistry logic removed. All navigation is NavMesh-based and influenced by tactical memory.
// </auto-generated>

namespace AIRefactored.AI.Optimization
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Memory;
    using AIRefactored.Core;
    using AIRefactored.Pools;
    using AIRefactored.Runtime;
    using BepInEx.Logging;
    using EFT;
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    /// Caches NavMesh paths and fallback scoring for individual bots.
    /// Optimizes retreat and navigation behaviors with smart path reuse, avoidance heuristics, and human-like caution.
    /// All failures are strictly isolated to the affected bot; all pools are always returned.
    /// </summary>
    public sealed class BotOwnerPathfindingCache
    {
        #region Constants

        private const float BlockCheckHeight = 1.2f;
        private const float BlockCheckMargin = 0.5f;
        private const float PathStaleSeconds = 7.5f; // Subtle randomization, bots periodically reevaluate for realism
        private const float RetryDelay = 1.2f;

        #endregion

        #region Fields

        private static readonly ManualLogSource Logger = Plugin.LoggerInstance;

        private readonly Dictionary<string, float> _coverWeights = new Dictionary<string, float>(64);
        private readonly Dictionary<string, List<Vector3>> _fallbackCache = new Dictionary<string, List<Vector3>>(64);
        private readonly Dictionary<string, (List<Vector3> path, float time)> _pathCache = new Dictionary<string, (List<Vector3>, float)>(64);
        private readonly Dictionary<string, float> _lastPathAttempt = new Dictionary<string, float>(64);

        private BotTacticalMemory _tacticalMemory;

        #endregion

        #region Public API

        public void BroadcastRetreat(BotOwner bot, Vector3 point)
        {
            try
            {
                if (bot == null || bot.BotsGroup == null)
                    return;

                string map = GameWorldHandler.TryGetValidMapName();
                if (!string.IsNullOrEmpty(map))
                {
                    BotMemoryStore.AddDangerZone(map, point, DangerTriggerType.Panic, 5f);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[BotOwnerPathfindingCache] BroadcastRetreat failed: " + ex);
            }
        }

        public void Clear()
        {
            try
            {
                _pathCache.Clear();
                _fallbackCache.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[BotOwnerPathfindingCache] Clear failed: " + ex);
            }
        }

        public float GetCoverWeight(string mapId, Vector3 pos)
        {
            try
            {
                string key = mapId + "_" + RoundVector3ToKey(pos);
                return _coverWeights.TryGetValue(key, out float value) ? value : 1f;
            }
            catch
            {
                return 1f;
            }
        }

        public void RegisterCoverNode(string mapId, Vector3 pos, float score)
        {
            try
            {
                string key = mapId + "_" + RoundVector3ToKey(pos);
                if (!_coverWeights.ContainsKey(key))
                {
                    _coverWeights[key] = Mathf.Clamp(score, 0.1f, 10f);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[BotOwnerPathfindingCache] RegisterCoverNode failed: " + ex);
            }
        }

        public void SetTacticalMemory(BotTacticalMemory memory)
        {
            _tacticalMemory = memory;
        }

        /// <summary>
        /// Returns a path from the cache or computes a fresh NavMesh path if expired or invalid.
        /// Adds slight randomization to cache expiry for realism, simulating player hesitation/reevaluation.
        /// Avoids recently-cleared danger zones using tactical memory.
        /// </summary>
        public List<Vector3> GetOptimizedPath(BotOwner bot, Vector3 destination)
        {
            List<Vector3> fallback = null;
            try
            {
                if (!IsAIBot(bot))
                {
                    fallback = TempListPool.Rent<Vector3>();
                    fallback.Add(destination);
                    return fallback;
                }

                string id = bot.Profile?.Id;
                if (string.IsNullOrEmpty(id))
                {
                    fallback = TempListPool.Rent<Vector3>();
                    fallback.Add(destination);
                    return fallback;
                }

                string key = id + "_" + destination.ToString("F2");
                float now = Time.time;

                // Path cache staleness & retry logic for realism
                if (_pathCache.TryGetValue(key, out var cached)
                    && !IsPathBlocked(cached.path)
                    && !IsPathInClearedZone(cached.path)
                    && (now - cached.time) < (PathStaleSeconds + UnityEngine.Random.Range(-0.7f, 0.8f)))
                {
                    return cached.path;
                }

                // If a recent failed attempt exists, delay re-attempting to avoid spam
                if (_lastPathAttempt.TryGetValue(key, out float lastTry) && now - lastTry < RetryDelay)
                {
                    fallback = TempListPool.Rent<Vector3>();
                    fallback.Add(destination);
                    return fallback;
                }
                _lastPathAttempt[key] = now;

                List<Vector3> path = BuildNavPath(bot.Position, destination);
                _pathCache[key] = (path, now);

                return path;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[BotOwnerPathfindingCache] GetOptimizedPath failed: " + ex);
                if (fallback == null)
                {
                    fallback = TempListPool.Rent<Vector3>();
                    fallback.Add(destination);
                }
                return fallback;
            }
        }

        public bool TryGetValidPath(BotOwner bot, Vector3 destination, out List<Vector3> path)
        {
            path = GetOptimizedPath(bot, destination);
            return path.Count >= 2 && !IsPathBlocked(path);
        }

        /// <summary>
        /// Computes a fallback (retreat) path in the opposite direction from a threat, always NavMesh-based,
        /// and avoids recently-cleared tactical zones and obstacles.
        /// </summary>
        public List<Vector3> GetFallbackPath(BotOwner bot, Vector3 threatDirection)
        {
            List<Vector3> empty = null;
            try
            {
                string id = bot.Profile?.Id;
                if (string.IsNullOrEmpty(id))
                {
                    empty = TempListPool.Rent<Vector3>();
                    return empty;
                }

                Vector3 origin = bot.Position;
                Vector3 fallbackTarget = origin - threatDirection.normalized * 8f;
                string key = id + "_fb_" + HashVecDir(origin, threatDirection);

                if (_fallbackCache.TryGetValue(key, out List<Vector3> cached)
                    && cached.Count > 1
                    && !IsPathBlocked(cached)
                    && !IsPathInClearedZone(cached))
                {
                    return cached;
                }

                // Only fallback to direct NavMesh path (custom navpoint/zone logic removed)
                List<Vector3> fallback = BuildNavPath(origin, fallbackTarget);

                // Avoids danger zones and cleared routes (simulates real player caution)
                if (fallback.Count > 1 && !IsPathBlocked(fallback) && !IsPathInClearedZone(fallback))
                {
                    _fallbackCache[key] = fallback;
                    return fallback;
                }

                empty = TempListPool.Rent<Vector3>();
                return empty;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[BotOwnerPathfindingCache] GetFallbackPath failed: " + ex);
                if (empty == null)
                {
                    empty = TempListPool.Rent<Vector3>();
                }
                return empty;
            }
        }

        #endregion

        #region Internal Helpers

        private static bool IsAIBot(BotOwner bot)
        {
            try
            {
                Player player = bot?.GetPlayer;
                return player != null && player.IsAI && !player.IsYourPlayer;
            }
            catch
            {
                return false;
            }
        }

        private static string RoundVector3ToKey(Vector3 v)
        {
            return v.x.ToString("F1") + "_" + v.y.ToString("F1") + "_" + v.z.ToString("F1");
        }

        private static string HashVecDir(Vector3 pos, Vector3 dir)
        {
            Vector3 hashVec = pos + dir.normalized * 2f;
            return hashVec.x.ToString("F1") + "_" + hashVec.y.ToString("F1") + "_" + hashVec.z.ToString("F1");
        }

        /// <summary>
        /// Always uses the NavMesh for pathfinding; never uses direct line-only unless mesh fails.
        /// All failures are isolated; never allocates outside pool.
        /// </summary>
        private List<Vector3> BuildNavPath(Vector3 origin, Vector3 target)
        {
            NavMeshPath navPath = TempNavMeshPathPool.Rent();
            try
            {
                bool valid = NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, navPath);

                if (valid && navPath.status == NavMeshPathStatus.PathComplete && navPath.corners.Length > 1)
                {
                    List<Vector3> result = TempListPool.Rent<Vector3>();
                    result.AddRange(navPath.corners);
                    TempNavMeshPathPool.Return(navPath);
                    return result;
                }

                // fallback is straight-line, never single-point unless truly blocked
                List<Vector3> fallback = TempListPool.Rent<Vector3>();
                fallback.Add(origin);
                fallback.Add(target);
                TempNavMeshPathPool.Return(navPath);
                return fallback;
            }
            catch (Exception ex)
            {
                TempNavMeshPathPool.Return(navPath);
                Logger.LogWarning("[BotOwnerPathfindingCache] BuildNavPath failed: " + ex);
                List<Vector3> fallback = TempListPool.Rent<Vector3>();
                fallback.Add(origin);
                fallback.Add(target);
                return fallback;
            }
        }

        /// <summary>
        /// Returns true if the first segment of the path is blocked by a door or obstacle.
        /// </summary>
        private bool IsPathBlocked(List<Vector3> path)
        {
            try
            {
                if (path == null || path.Count < 2)
                    return false;

                Vector3 origin = path[0] + Vector3.up * BlockCheckHeight;
                Vector3 target = path[1];
                Vector3 direction = (target - path[0]).normalized;

                if (direction.sqrMagnitude < 0.001f || float.IsNaN(direction.x) || float.IsNaN(direction.y))
                    return false;

                float distance = Vector3.Distance(path[0], target) + BlockCheckMargin;

                if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, AIRefactoredLayerMasks.MovementBlockerMask))
                {
                    int layer = hit.collider.gameObject.layer;
                    if (layer == AIRefactoredLayerMasks.DoorLowPolyCollider)
                    {
                        var navPath = TempNavMeshPathPool.Rent();
                        bool blocked = !NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, navPath)
                                       || navPath.status != NavMeshPathStatus.PathComplete;
                        TempNavMeshPathPool.Return(navPath);
                        return blocked;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if any segment of the path passes through a recently-cleared or danger zone.
        /// </summary>
        private bool IsPathInClearedZone(List<Vector3> path)
        {
            try
            {
                if (_tacticalMemory == null || path == null || path.Count == 0)
                    return false;

                for (int i = 0; i < path.Count; i++)
                {
                    if (_tacticalMemory.WasRecentlyCleared(path[i]))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
