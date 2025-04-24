#nullable enable

using AIRefactored.AI.Memory;
using AIRefactored.AI.Navigation;
using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Caches NavMesh paths and fallback scoring for individual bots.
    /// Optimizes retreat and navigation behaviors with smart path reuse and avoidance heuristics.
    /// </summary>
    public class BotOwnerPathfindingCache
    {
        #region Fields

        private readonly Dictionary<string, List<Vector3>> _pathCache = new(64);
        private readonly Dictionary<string, float> _coverWeights = new(64);
        private readonly Dictionary<string, List<Vector3>> _fallbackCache = new(64);

        private BotTacticalMemory? _tacticalMemory;

        private const float BlockCheckHeight = 1.2f;
        private const float BlockCheckMargin = 0.5f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Initialization

        public void SetTacticalMemory(BotTacticalMemory memory) => _tacticalMemory = memory;

        #endregion

        #region Path Caching

        public List<Vector3> GetOptimizedPath(BotOwner botOwner, Vector3 destination)
        {
            if (!IsAIBot(botOwner))
                return new List<Vector3> { destination };

            string? botId = botOwner.Profile?.Id;
            if (string.IsNullOrEmpty(botId))
                return new List<Vector3> { destination };

            string key = $"{botId}_{destination:F2}";

            if (_pathCache.TryGetValue(key, out var cached) && !IsPathBlocked(cached))
                return cached;

            var path = BuildNavPath(botOwner.Position, destination);
            _pathCache[key] = path;
            return path;
        }

        public bool TryGetValidPath(BotOwner botOwner, Vector3 destination, out List<Vector3> path)
        {
            path = GetOptimizedPath(botOwner, destination);
            return path.Count >= 2 && !IsPathBlocked(path);
        }

        public void Clear()
        {
            _pathCache.Clear();
            _fallbackCache.Clear();
        }

        #endregion

        #region NavMesh Utilities

        private List<Vector3> BuildNavPath(Vector3 origin, Vector3 target)
        {
            var navPath = new NavMeshPath();
            bool valid = NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, navPath);

            return (valid && navPath.status == NavMeshPathStatus.PathComplete)
                ? new List<Vector3>(navPath.corners)
                : new List<Vector3> { origin, target };
        }

        private bool IsPathBlocked(List<Vector3> path)
        {
            if (path.Count < 2)
                return false;

            Vector3 origin = path[0] + Vector3.up * BlockCheckHeight;
            Vector3 next = path[1];
            Vector3 dir = (next - path[0]).normalized;
            float dist = Vector3.Distance(path[0], next);

            return Physics.Raycast(origin, dir, dist + BlockCheckMargin, LayerMaskClass.DoorLayer);
        }

        #endregion

        #region Fallback Caching

        public List<Vector3> GetFallbackPath(BotOwner bot, Vector3 direction)
        {
            string? id = bot.Profile?.Id;
            if (string.IsNullOrEmpty(id))
                return new List<Vector3>();

            Vector3 origin = bot.Position;
            Vector3 fallbackTarget = origin - direction.normalized * 8f;
            string key = $"{id}_fb_{HashVecDir(origin, direction)}";

            if (_fallbackCache.TryGetValue(key, out var cached) && cached.Count > 1 && !IsPathBlocked(cached))
                return cached;

            // Priority 1: Query fallback-tagged NavPoints
            List<Vector3> navFallbacks = NavPointRegistry.QueryNearby(origin, 25f,
                pos => NavPointRegistry.GetTag(pos) == "fallback");

            foreach (var candidate in navFallbacks)
            {
                if (IsPathInClearedZone(new List<Vector3> { candidate }))
                    continue;

                var navPath = BuildNavPath(origin, candidate);
                if (navPath.Count > 1 && !IsPathBlocked(navPath))
                {
                    _fallbackCache[key] = navPath;
                    return navPath;
                }
            }

            // Priority 2: Raw fallback vector
            var raw = BuildNavPath(origin, fallbackTarget);
            if (raw.Count > 1 && !IsPathBlocked(raw) && !IsPathInClearedZone(raw))
            {
                _fallbackCache[key] = raw;
                return raw;
            }

            return new List<Vector3>();
        }

        private bool IsPathInClearedZone(List<Vector3> path)
        {
            if (_tacticalMemory == null || path.Count == 0)
                return false;

            foreach (var point in path)
            {
                if (_tacticalMemory.WasRecentlyCleared(point))
                    return true;
            }

            return false;
        }

        private static string HashVecDir(Vector3 pos, Vector3 dir)
        {
            Vector3 hashVec = pos + dir.normalized * 2f;
            return $"{hashVec.x:F1}_{hashVec.y:F1}_{hashVec.z:F1}";
        }

        #endregion

        #region Cover Scoring

        public void RegisterCoverNode(string mapId, Vector3 pos, float score)
        {
            string key = $"{mapId}_{RoundVector3ToKey(pos)}";
            if (!_coverWeights.ContainsKey(key))
                _coverWeights[key] = Mathf.Clamp(score, 0.1f, 10f);
        }

        public float GetCoverWeight(string mapId, Vector3 pos)
        {
            string key = $"{mapId}_{RoundVector3ToKey(pos)}";
            return _coverWeights.TryGetValue(key, out float weight) ? weight : 1f;
        }

        #endregion

        #region Danger Zone Sync

        public void BroadcastRetreat(BotOwner botOwner, Vector3 point)
        {
            if (!IsAIBot(botOwner) || botOwner.BotsGroup == null || string.IsNullOrEmpty(botOwner.ProfileId))
                return;

            string map = GameWorldHandler.GetCurrentMapName();
            BotMemoryStore.AddDangerZone(map, point, DangerTriggerType.Panic, 5f);
        }

        #endregion

        #region Helpers

        private static bool IsAIBot(BotOwner? bot)
        {
            var player = bot?.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        private static string RoundVector3ToKey(Vector3 v)
        {
            return $"{v.x:F1}_{v.y:F1}_{v.z:F1}";
        }

        #endregion
    }
}
